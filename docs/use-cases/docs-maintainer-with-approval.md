# Documentation maintainer with approval

A team keeps its product documentation in the same repository as the code. They
want an agent that reads the source, notices where the docs have drifted, and
proposes edits. The rules are firm: the agent may read anything, may write only
under `docs/`, and every write is shown to the maintainer running it before it
happens. A "no" must mean the file is untouched.

## What the agent needs

| Need                                | AgentKit part                                                                                                           |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Read source, write documentation    | `UseWorkspace(root)`: `read_file`, `glob`, `search`, `write_file`, `edit`                                               |
| Writes confined to one directory    | A policy that inspects `request.Resources` and denies paths outside `docs/`                                             |
| A human decision before every write | `RequireApproval` from the policy, `AddInMemoryApprovalStore`, an `IApprovalHandler`, an `IApprovalResponderAuthorizer` |
| A visible working plan              | `AddPlanTool` so the model records what it intends to change                                                            |

## Compose the engine

```csharp
static AgentEngine CreateDocsMaintainer(string repoRoot, ExecutionIdentity maintainer, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .UseWorkspace(repoRoot)
        .WithIdentity(maintainer)
        .WithInstructions(
            "You maintain the documentation under docs/. Read the code to verify " +
            "claims. Record a plan before editing. Never modify files outside docs/.")
        .WithMaxTurns(60);

    builder.Services.AddPlanTool();

    // 1. Only docs/ may change; everything else is denied.
    builder.Services.AddSingleton<ISecurityPolicy, DocsOnlyWritePolicy>();
    // 2. Every write under docs/ still needs a human.
    builder.Services.AddSingleton<ISecurityPolicy, ApproveWritesPolicy>();
    // 3. Who is asked, and whose answer counts.
    builder.Services.AddInMemoryApprovalStore();
    builder.Services.RemoveAll<IApprovalHandler>();
    builder.Services.AddSingleton<IApprovalHandler>(new ConsoleApprovalHandler(maintainer, TimeProvider.System));
    builder.Services.RemoveAll<IApprovalResponderAuthorizer>();
    builder.Services.AddSingleton<IApprovalResponderAuthorizer>(new SameUserMayApprove(maintainer));

    return builder.Build();
}
```

The two policies compose by the fixed algebra: any `Deny` wins, otherwise any
`RequireApproval` sends the request to a human. So a write to `src/Program.cs`
is denied before anyone is asked, and a write to `docs/guide.md` is asked about.

```csharp
sealed class DocsOnlyWritePolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind is not (SecurityOperationKind.FileWrite or SecurityOperationKind.DirectoryCreate))
        {
            return ValueTask.FromResult(new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
        }

        var allInsideDocs = request.Resources.All(resource =>
            resource.Kind is ProtectedResourceKind.File or ProtectedResourceKind.Directory
            && resource.Identifier.StartsWith("docs/", StringComparison.Ordinal));

        return ValueTask.FromResult(allInsideDocs
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null)
            : new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "docs.outside-scope", "Only files under docs/ may be changed."));
    }
}

sealed class ApproveWritesPolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(request.Kind is SecurityOperationKind.FileWrite or SecurityOperationKind.DirectoryCreate
            ? new SecurityPolicyResult(SecurityPolicyResultKind.RequireApproval, "docs.ask", "Documentation changes need your approval.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
}
```

Resource identifiers are workspace-relative paths, already normalized by the
sandbox, which is why a plain prefix check is enough. `ConsoleApprovalHandler`
and `SameUserMayApprove` are the classes shown in
[Permissions and approvals](../guides/permissions.md#ask-a-human-before-acting);
the handler prints `request.SafePresentation`, which names the operation and
path without content, and builds an `ApprovalResponse` bound to
`request.Binding`.

## Use it

```csharp
await using var engine = CreateDocsMaintainer(repoRoot, maintainer, apiKey);

var result = await engine.SendAsync(
    "The public API of src/AgentKit.Simple changed last week. Bring docs/getting-started.md up to date.",
    new ConsoleProgress(),
    cancellationToken);
```

```csharp
sealed class ConsoleProgress : IConversationEventObserver
{
    public ValueTask OnEventAsync(ConversationEvent conversationEvent, CancellationToken cancellationToken)
    {
        switch (conversationEvent)
        {
            case ConversationToolCallEvent call when call.ToolName is "write_file" or "edit":
                var path = call.Presentation?.Parts.FirstOrDefault(p => p.Path is not null)?.Path;
                Console.WriteLine($"-> {call.ToolName}: {path ?? call.ArgumentsJson}");
                break;
            case ConversationToolResultEvent { Succeeded: false } denied:
                Console.WriteLine($"   {denied.ToolName}: {denied.Summary}");
                break;
            case ConversationAssistantTextEvent text:
                Console.WriteLine(text.Text);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
```

When the model calls `write_file`, the approval prompt appears in the console
before the file system is touched. A `y` lets that one write proceed; anything
else, or waiting past the binding's expiry, produces a denied tool result the
model sees and can respond to, typically by explaining what it wanted to change.

## What the framework guarantees

- **Approval is per request.** The grant an approval produces is bound to the
  exact path, effect, identity, and a short expiry. Approving one write never
  approves the next, and a `write_file` that mutates its arguments after
  approval is re-evaluated.
- **The write itself is intent-checked.** `write_file` requires a disposition
  (`create_only`, `replace_existing`, `create_or_replace`, or `append`) and the
  file system holds the model to it, so "update the guide" cannot silently
  create a new file.
- **Three kinds of "no" are distinguishable.** A human denial, an expired
  prompt, and an unavailable approval service produce different denial codes in
  the audit trail.
- **Nobody else's answer counts.** The authorizer rejects an `ApprovalResponse`
  whose approver is not the requesting identity, and a rejected response is
  discarded rather than treated as denial.

## What lives where

| Concern                             | Package                                                                                                                          |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Policies, approval broker, grants   | [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)                                                                 |
| In-memory approval and grant stores | [AgentKit.Permissions.InMemory](../../src/AgentKit.Permissions.InMemory/README.md)                                               |
| Write and edit tools                | [AgentKit.Tools.Write](../../src/AgentKit.Tools.Write/README.md), [AgentKit.Tools.Edit](../../src/AgentKit.Tools.Edit/README.md) |
| `plan` and `todo` tools             | [AgentKit.Tools.Plan](../../src/AgentKit.Tools.Plan/README.md)                                                                   |
| Sandboxed file system               | [AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md)                                                                   |

Next: [Operations runbook executor](ops-runbook-executor.md) ·
[Permissions and approvals](../guides/permissions.md)
