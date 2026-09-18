# Customer support assistant in a web API

A SaaS product wants a support assistant inside its web app. A signed-in
customer opens a chat, asks about their account, comes back tomorrow and finds
the same conversation, and sees the answer appear as it is written. Nothing the
assistant does touches the host: it has no files, no shell, no network of its
own. What it does need is durable history, the customer's real identity on every
record, and a streaming path to the browser.

## What the agent needs

| Need                                 | AgentKit part                                                                                                            |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------ |
| Conversations that survive a restart | `UseSqliteSessions`, backed by `AgentKit.Session.Sqlite`                                                                 |
| Many customers on one engine         | `Agent.SendAsync(AgentSendRequest)`: the engine owns sessions and their lanes                                            |
| Each customer sees only their own    | `AgentSendRequest.Identity` from your authentication; the store partitions by it and the engine checks ownership on open |
| Resume a conversation by id          | `AgentSendRequest.SessionId` from the previous `AgentLoopResult.SessionId`                                               |
| Stream tokens to the browser         | `AgentSendRequest.Observer`, an `IAgentRunObserver` receiving `AgentRunModelResponseEvent` deltas                        |
| No host access at all                | A policy that allows session state and denies everything else                                                            |

## Compose the engine

Build one engine when the application starts and keep it for the life of the
process. The engine coordinates every customer's sessions: each request names
the customer's identity and, after the first turn, their session id.

```csharp
static AgentEngine CreateSupportEngine(string apiKey, ISecurityAuditSink auditSink)
{
    var builder = AgentEngine.CreateBuilder()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .UseSqliteSessions("/var/lib/support/sessions.db")
        .WithIdentity(ServiceIdentity)      // the host's own identity; customers arrive per request
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

// At startup:
builder.Services.AddSingleton(sp => CreateSupportEngine(config["OpenAI:ApiKey"]!, sp.GetRequiredService<ISecurityAuditSink>()));
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
static ExecutionIdentity IdentityFor(ClaimsPrincipal user, DateTimeOffset authenticatedAt, DateTimeOffset? expiresAt) =>
    ExecutionIdentity.ForHuman(
        new TenantId(user.FindFirstValue("tenant")!),
        new PrincipalId(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
        new IdentityIssuerId("acme-idp"),
        "oidc",
        authenticatedAt,
        expiresAt,
        assurance: IdentityAssuranceLevel.Strong);
```

`ForHuman` records who authenticated the customer, how, and when, and derives a
content-safe fingerprint from those facts; the token itself is never an input.

## Use it

A minimal API endpoint that starts or continues a conversation and streams
server-sent events. The engine is resolved once; each request drives the support
agent for one customer:

```csharp
app.MapPost("/support/{sessionId?}", async (Guid? sessionId, ChatRequest body, HttpContext http, AgentEngine engine, CancellationToken ct) =>
{
    var customer = IdentityFor(http.User, authenticatedAt, expiresAt);   // from the auth ticket
    var agent = (await engine.GetAgentsAsync(ct)).Single();
    var support = (await engine.GetAgentAsync(agent.Id, ct))!;

    http.Response.ContentType = "text/event-stream";
    var observer = new SseObserver(http.Response);
    AgentLoopResult result;
    try
    {
        result = await support.SendAsync(
            new AgentSendRequest(customer, body.Text, sessionId is { } id ? new SessionId(id) : null, observer: observer),
            ct);
    }
    catch (AgentAdmissionRejectedException)
    {
        return Results.NotFound();          // unknown session, or one that belongs to someone else
    }
    catch (AgentSessionBusyException)
    {
        return Results.Conflict();          // the customer double-submitted; the first turn is still running
    }

    await observer.CompleteAsync(result, ct);
    return Results.Empty;
});
```

The observer receives the loop's provisional events (text deltas, tool starts
and results) and the final result names the session, so the browser learns its
resume token from the `done` frame:

```csharp
sealed class SseObserver(HttpResponse response) : IAgentRunObserver
{
    public async ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken)
    {
        if (runEvent is AgentRunModelResponseEvent { ResponseEvent: ModelPartDelta { Delta: TextContentDelta text } })
        {
            await response.WriteAsync($"event: delta\ndata: {JsonSerializer.Serialize(text.Text)}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }

    public async ValueTask CompleteAsync(AgentLoopResult result, CancellationToken cancellationToken)
    {
        var answer = string.Concat(result.NewMessages.OfType<AssistantMessage>().SelectMany(m => m.Parts.OfType<TextPart>()).Select(p => p.Text));
        await response.WriteAsync($"event: done\ndata: {JsonSerializer.Serialize(new { session = result.SessionId.Value, outcome = result.Outcome.GetType().Name, answer })}\n\n", cancellationToken);
    }
}
```

Listing a customer's earlier conversations for a sidebar goes through the
session directory the engine composed; because the store partitions by tenant
and principal, a customer sees only their own sessions, and `SendAsync` with a
session id that belongs to someone else is rejected before anything is appended.

## What the framework guarantees

- **A session id from another tenant does not open.** The engine loads the
  session and checks its agent, tenant, principal, and state against the request
  before appending anything; a mismatch is an `AgentAdmissionRejectedException`
  with a safe reason.
- **One turn per session at a time.** The engine enters the session's lane
  before recording the message; the session profile decides whether a second
  turn waits or fails with `AgentSessionBusyException`. Other customers'
  sessions are unaffected.
- **Client disconnect is a clean cancellation.** Cancelling `ct` before the
  message is committed leaves no trace; afterwards the run settles with
  `AgentRunCancelled` and the next `SendAsync` continues from the committed
  history.
- **Audit is required, not hoped for.** With `SecurityAuditDelivery.Required`
  and a durable sink, an audited operation whose record cannot be delivered does
  not run.
- **Provider failures carry no secrets.** `SimpleAgentException` and the events
  never contain the key or raw provider diagnostics.

## Status

The engine path above is complete for in-process hosting: one engine, many
customers, concurrent sessions, per-session exclusion. Two refinements are
tracked in the
[implementation ledger](../implementation-progress.md#component-coverage):
queue-backed admission through `AgentKit.IO` (so a double-submit can be queued
as a follow-up instead of rejected) and attaching a second request to a run in
progress by `RunId`. Audit coverage is also still widening: the approval flow
and session-store enforcement are audited today, while ordinary allow and deny
decisions and file, process, and network enforcement are not yet, as
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
