# Customer support assistant in a web API

A SaaS product wants a support assistant inside its web app. A signed-in
customer opens a chat, asks about their account, comes back tomorrow and finds
the same conversation, and sees the answer appear as it is written. Nothing the
assistant does touches the host: it has no files, no shell, no network of its
own. What it does need is durable history, the customer's real identity on every
record, and a streaming path to the browser.

## What the agent needs

| Need                                 | AgentKit part                                                                           |
| ------------------------------------ | --------------------------------------------------------------------------------------- |
| Conversations that survive a restart | `UseSqliteSessions`, backed by `AgentKit.Session.Sqlite`                                |
| Each customer sees only their own    | `WithIdentity` with the identity your authentication produced; stores partition by it   |
| Resume a conversation by id          | `engine.Conversation.OpenAsync` and `ListAsync`                                         |
| Stream tokens to the browser         | `SendAsync(text, IConversationEventObserver)` and `ConversationAssistantTextDeltaEvent` |
| No host access at all                | A policy that allows session state and denies everything else                           |

## Compose the engine

Build one engine per active conversation. An `IConversationSession` serializes
its calls and holds one open session at a time, so a per-request factory keyed
by the customer's session is the shape that fits today; see **Status** below for
where the multi-run engine is heading.

```csharp
static AgentEngine CreateSupportEngine(ExecutionIdentity customer, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .UseSqliteSessions("/var/lib/support/sessions.db")
        .WithIdentity(customer)
        .WithInstructions(
            "You are the support assistant for Acme. Answer from the customer's " +
            "own account context. If you do not know, say so and offer a human agent.")
        .WithMaxTurns(6)
        .WithAttemptTimeout(TimeSpan.FromSeconds(45));

    // No UseLocalDevelopmentDefaults: a service supplies its own security state.
    builder.Services.AddSqliteSecurityGrantStore(new SqliteSecurityGrantStoreTarget(
        "/var/lib/support/grants.db", GrantStoreInstanceId,
        SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));
    builder.Services.AddSingleton<ISecurityPolicy, ConversationOnlyPolicy>();
    builder.Services.AddSecurityAuditSink(
        new SecurityAuditSinkRegistration(SupportedAuditKinds, SecurityAuditDelivery.Required, providesDurableAcceptance: true),
        auditSink);

    return builder.Build();
}
```

The policy is small because the agent is small. Session reads and appends are
allowed; anything that would touch a file, process, or network is denied even
though no such tool is registered, so a later `builder.Services.AddReadTool()`
cannot quietly widen the agent:

```csharp
sealed class ConversationOnlyPolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(request.Kind is SecurityOperationKind.StateRead or SecurityOperationKind.StateMutation
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "support.session", "Conversation state may be read and appended.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "support.no-host-access", "The support assistant has no host access."));
}
```

The identity comes from your authentication middleware, never from the request
body. Map the authenticated principal once, at the trusted ingress:

```csharp
static ExecutionIdentity IdentityFor(ClaimsPrincipal user, TimeProvider clock) => new(
    new TenantId(user.FindFirstValue("tenant")!),
    new PrincipalId(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
    ExecutionSubjectKind.Human,
    new AuthenticationEvidence(
        new AuthenticationEvidenceId(user.FindFirstValue("sid")!),
        new IdentityIssuerId("acme-idp"),
        "oidc",
        clock.GetUtcNow(),
        null,
        new AuthenticationEvidenceFingerprint(new ContentHash($"sha256:{sessionFingerprint}"))),
    claims: [],
    delegationChain: [],
    IdentityAssuranceLevel.Strong,
    new IdentityVersion(1));
```

## Use it

A minimal API endpoint that starts or continues a conversation and streams
server-sent events:

```csharp
app.MapPost("/support/{sessionId?}", async (Guid? sessionId, ChatRequest body, HttpContext http, CancellationToken ct) =>
{
    var customer = IdentityFor(http.User, TimeProvider.System);
    await using var engine = CreateSupportEngine(customer, apiKey);

    if (sessionId is { } existing)
    {
        var opened = await engine.Conversation.OpenAsync(new SessionId(existing), ct);
        if (opened is ConversationSessionOpenRejected rejected)
        {
            return Results.NotFound(rejected.SafeMessage);
        }
    }

    http.Response.ContentType = "text/event-stream";
    var observer = new SseObserver(http.Response);
    var result = await engine.SendAsync(body.Text, observer, ct);
    await observer.CompleteAsync(result.Succeeded, ct);
    return Results.Empty;
});
```

The browser learns which session it is in before the first token arrives: the
first observed turn begins with a `ConversationSessionBoundEvent`, and the
result carries the same `SessionId` for a non-streaming caller.

```csharp
sealed class SseObserver(HttpResponse response) : IConversationEventObserver
{
    public async ValueTask OnEventAsync(ConversationEvent conversationEvent, CancellationToken cancellationToken)
    {
        var payload = conversationEvent switch
        {
            ConversationSessionBoundEvent bound => $"event: session\ndata: {bound.SessionId}\n\n",
            ConversationAssistantTextDeltaEvent delta => $"event: delta\ndata: {JsonSerializer.Serialize(delta.Text)}\n\n",
            ConversationUsageEvent usage => $"event: usage\ndata: {usage.Usage.OutputTokens}\n\n",
            ConversationTurnCompletedEvent done => $"event: done\ndata: {done.Outcome}\n\n",
            _ => null,
        };

        if (payload is not null)
        {
            await response.WriteAsync(payload, cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}
```

Listing a customer's earlier conversations for a sidebar is one call on the same
session; because the store partitions by identity, the page contains only that
customer's sessions:

```csharp
var page = (ConversationSessionPage) await engine.Conversation.ListAsync(afterSessionId: null, maximumResults: 20, ct);
```

## What the framework guarantees

- **A session id from another tenant does not open.** `OpenAsync` checks the
  session against the identity the engine was built with and returns
  `ConversationSessionOpenRejected` with a safe message.
- **Client disconnect is a clean cancellation.** Cancelling `ct` ends the turn
  with a `ConversationTurnCompletedEvent` whose `Outcome` is `"cancelled"`; the
  session store is left consistent and the next `SendAsync` continues from the
  committed history.
- **Audit is required, not hoped for.** With `SecurityAuditDelivery.Required`
  and a durable sink, an audited operation whose record cannot be delivered does
  not run.
- **Provider failures carry no secrets.** `SimpleAgentException` and the events
  never contain the key or raw provider diagnostics.

## Status

The direct path above is complete. The `AgentEngine` facade is designed to host
one composition for many concurrent sessions with queue-backed input admission,
which would replace the per-conversation engine with one long-lived engine and
`agent.RunAsync(options)`; that runnable graph is tracked in the
[implementation ledger](../implementation-progress.md#component-coverage). Audit
coverage is also still widening: the approval flow and session-store enforcement
are audited today, while ordinary allow and deny decisions and file, process,
and network enforcement are not yet, as
[Permissions and approvals](../guides/permissions.md#audit) explains.

## What lives where

| Concern                     | Package                                                                        |
| --------------------------- | ------------------------------------------------------------------------------ |
| Builder sugar, `AskAsync`   | [AgentKit.Simple](../../src/AgentKit.Simple/README.md)                         |
| Session store and directory | [AgentKit.Session.Sqlite](../../src/AgentKit.Session.Sqlite/README.md)         |
| Durable grant store         | [AgentKit.Permissions.Sqlite](../../src/AgentKit.Permissions.Sqlite/README.md) |
| Policies, audit sinks       | [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)               |
| Identity values             | [AgentKit.Identity](../../src/AgentKit.Identity/README.md)                     |
| Events and observers        | [AgentKit.Conversations](../../src/AgentKit.Conversations/README.md)           |

Next: [Read-only code review in CI](code-review-in-ci.md) ·
[Storing conversations](../guides/storage.md)
