# Permissions and approvals

An agent that can read files, run commands, and call the network needs someone
to decide what it may do. In AgentKit that someone is you, through small
policies you write, and the framework makes sure the decision is enforced where
the effect actually happens.

## How a decision is made

Every protected operation follows the same four steps, whether it is a file
write, a process start, a network request, or a session mutation:

1. **Request.** The component about to act (a tool, the file system, the process
   runner) builds a `SecurityRequest`: what kind of operation (`FileWrite`,
   `Process`, `Network`, …), what effect (`Observe`, `Create`, `Replace`,
   `Execute`, …), on which resources (the exact paths, hosts, or executables),
   and for whom (the authenticated identity).
2. **Policies.** Every registered `ISecurityPolicy` sees the request and answers
   `Allow`, `Deny`, `RequireApproval`, or `Abstain`. The rules are fixed and
   simple: **any `Deny` wins; otherwise any `RequireApproval` sends the request
   to a human; otherwise at least one `Allow` is required; if every policy
   abstains, the answer is no.**
3. **Grant.** An allow produces a `SecurityGrant`, a single-use ticket bound to
   that exact request, identity, resource set, and a short expiry.
4. **Enforcement.** The component performing the effect validates and consumes
   the grant immediately before acting. The sandboxed file system, the process
   runner, and the network transport each do this themselves, so an allow at a
   higher level can never authorize a different concrete effect lower down.

Nothing in the model's messages, a tool's description, or a hook can grant
authority. Only policies can, and only the effecting component can spend a
grant.

## The default you start with

```csharp
.UseLocalDevelopmentDefaults()
```

registers one policy, `AllowAllSecurityPolicy`, which answers `Allow` to
everything. It also keeps grants in memory, sets audit delivery to best-effort,
and uses your OS user as the identity. This is the honest default for a tool you
run on your own machine against your own files. It is named so nobody ships it
by accident; for anything that serves other people or touches untrusted input,
the sections below replace it.

## Make the agent read-only

Policies are additive, and deny wins. So the smallest useful policy is one that
denies the effects you do not want and abstains on everything else:

```csharp
sealed class ReadOnlyWorkspacePolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        var mutates = request.Kind is SecurityOperationKind.FileWrite
            or SecurityOperationKind.DirectoryCreate
            or SecurityOperationKind.Process;

        return ValueTask.FromResult(mutates
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "read-only", "This agent may only read the workspace.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
    }
}
```

```csharp
var builder = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .UseWorkspace("/home/me/projects/website");

builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();

await using var engine = builder.Build();
```

The agent still has `write_file` and `edit` and may try to use them. When it
does, the request is denied before the file system is touched, the tool returns
a typed "denied" result, and the model sees that the change did not happen. The
`code` and message you supply are safe to show a user; they carry no paths or
content.

## Scope what files may be touched

`AgentKit.Permissions` ships one illustrative policy,
`WorkspaceScopedFileAccessPolicy`, which allows file and directory operations
only on non-rooted, traversal-free relative paths and abstains on everything
else. It is a second line of defence behind the sandbox, not a replacement for
your own rules:

```csharp
builder.Services.AddWorkspaceScopedFileAccessPolicy();
```

For sharper rules, inspect `request.Resources`: each `ProtectedResource` has a
`Kind` (`File`, `Directory`, `Executable`, `Host`, …) and an `Identifier` (the
relative path, host name, or executable path). A policy that allows writes only
under `docs/` looks at the identifiers, returns `Allow` when they all match, and
`Deny` otherwise.

## Ask a human before acting

Return `RequireApproval` from a policy and register three collaborators: a store
that remembers the pending approval, a handler that asks, and an authorizer that
checks who answered.

```csharp
sealed class ApproveWritesPolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(request.Kind is SecurityOperationKind.FileWrite or SecurityOperationKind.Process
            ? new SecurityPolicyResult(SecurityPolicyResultKind.RequireApproval, "ask", "Changes need your approval.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
}

sealed class ConsoleApprovalHandler(ExecutionIdentity me, TimeProvider clock) : IApprovalHandler
{
    public ValueTask<ApprovalHandlerResult> TryResolveAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        Console.Write($"{request.SafePresentation} [y/N] ");
        var approved = Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.NewGuid()), request.Id, request.Binding,
            approved ? ApprovalResolution.Approved : ApprovalResolution.Denied, me, clock.GetUtcNow());
        return ValueTask.FromResult<ApprovalHandlerResult>(new ApprovalHandlerResponded(response));
    }
}

sealed class SameUserMayApprove(ExecutionIdentity me) : IApprovalResponderAuthorizer
{
    public ValueTask<ApprovalResponderAuthorizationResult> AuthorizeAsync(ApprovalRequest request, ApprovalResponse response, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ApprovalResponderAuthorizationResult>(
            response.ApproverIdentity == me && request.Binding.Request.Identity == me
                ? new ApprovalResponderAuthorized()
                : new ApprovalResponderUnauthorized("Only the requesting user may approve."));
}
```

```csharp
builder.Services.AddSingleton<ISecurityPolicy, ApproveWritesPolicy>();
builder.Services.AddInMemoryApprovalStore();
builder.Services.RemoveAll<IApprovalHandler>();
builder.Services.AddSingleton<IApprovalHandler>(new ConsoleApprovalHandler(me, TimeProvider.System));
builder.Services.RemoveAll<IApprovalResponderAuthorizer>();
builder.Services.AddSingleton<IApprovalResponderAuthorizer>(new SameUserMayApprove(me));
```

`me` is the `ExecutionIdentity` you passed to `WithIdentity` (or, with the local
defaults, the process user). The defaults for the handler and authorizer both
deny, so a `RequireApproval` without these registrations is a safe no.

What the framework guarantees around your three classes: the approval is bound
to the exact request, so approving one write does not approve the next; the
grant it produces expires no later than what the human approved; a human "no",
an expired prompt, and an unavailable approval service are three different
denial codes in the audit trail; and an approval response from anyone the
authorizer rejects is discarded.

The [CodingAgent](../../examples/CodingAgent/README.md) example is a complete
version of this with a terminal UI, three permission modes, and a
`RequireApproval` policy that switches behaviour at runtime.

## Bring your own identity

The identity in every request comes from `WithIdentity`:

```csharp
.WithIdentity(ExecutionIdentity.ForHuman(
    new TenantId("acme"), new PrincipalId("alice"),
    new IdentityIssuerId("acme-idp"), "oidc", authenticatedAt, expiresAt,
    assurance: IdentityAssuranceLevel.Strong))
```

`ExecutionIdentity.ForHuman` and `ForService` (from `AgentKit.Identity`) record
who authenticated the subject, how, and when, and derive a content-safe
fingerprint; no token is an input. The full constructor remains for hosts that
carry claims and delegation chains.

Identity is authenticated at your trusted ingress and carried unchanged;
policies read it, and stores partition by it, but it never grants anything on
its own. The [identity guide](../architecture/identity.md) covers evidence,
claims, assurance, and delegation.

## Audit

Security transitions produce redacted audit records: identities, operation
kinds, resources, decision codes, and timestamps, never prompts, file contents,
or credentials. Today the approval flow (requests, outcomes, and the grants it
issues) and session-store enforcement are audited; ordinary allow/deny decisions
and file or process enforcement are not yet, and the
[permissions workstream](../workstreams/permissions-approvals-and-audit.md)
tracks closing that gap.

With `UseLocalDevelopmentDefaults` delivery is best-effort because no sink is
registered. A real host registers a sink and keeps delivery `Required`, which
means an audited operation whose record cannot be delivered does not run:

```csharp
builder.Services.AddSecurityAuditSink(
    new SecurityAuditSinkRegistration(supportedEventKinds, SecurityAuditDelivery.Required, providesDurableAcceptance: true),
    mySink);
builder.Services.Configure<AgentPermissionOptions>(o => o.AuditDelivery = SecurityAuditDelivery.Required);
```

## What lives where

| Concern                              | Package                                                                            |
| ------------------------------------ | ---------------------------------------------------------------------------------- |
| Authority, policies, approvals       | [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)                   |
| In-memory grant and approval stores  | [AgentKit.Permissions.InMemory](../../src/AgentKit.Permissions.InMemory/README.md) |
| Durable grant store                  | [AgentKit.Permissions.Sqlite](../../src/AgentKit.Permissions.Sqlite/README.md)     |
| Identity contracts and normalization | [AgentKit.Identity](../../src/AgentKit.Identity/README.md)                         |

The normative rules, including the policy algebra and grant semantics, are in
[permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md).

Next: [Working with files](file-system.md) · [Storing conversations](storage.md)
