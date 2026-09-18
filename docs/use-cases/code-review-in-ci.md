# Read-only code review in CI

A pipeline step checks out a pull request, points an agent at the repository,
and asks for a review against the team's guidelines. The step must be safe to
run on untrusted branches: the agent may read, search, and glob anything in the
checkout and nothing outside it, and it must be physically unable to write a
file or start a process no matter what the diff says. The job fails when the
review finds a blocking issue, and it reports how many tokens it spent.

## What the agent needs

| Need                                  | AgentKit part                                                                                    |
| ------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Read, search, and glob the checkout   | `UseWorkspace(root)`: sandboxed file system plus `read_file`, `list_directory`, `glob`, `search` |
| Never write, never execute            | An `ISecurityPolicy` that denies `FileWrite`, `DirectoryCreate`, `Process`, `Network`            |
| Team guidelines the model can consult | `AddSkillTool` with a `SkillDefinition` pointing at a file inside the workspace                  |
| Bounded run                           | `WithMaxTurns`, `WithAttemptTimeout`, and a linked `CancellationToken`                           |
| Spend visible in the job log          | `ConversationUsageEvent` on the turn result                                                      |

## Compose the engine

```csharp
static AgentEngine CreateReviewer(string checkoutRoot, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .UseWorkspace(checkoutRoot)
        .WithInstructions(
            "You review pull requests. Load the 'review-guidelines' skill first, " +
            "then inspect only the files named in the request.")
        .WithOutput<ReviewVerdict>("""
            {
              "type": "object",
              "properties": {
                "approve": { "type": "boolean" },
                "summary": { "type": "string" },
                "blocking": { "type": "array", "items": { "type": "string" } }
              },
              "required": ["approve", "summary", "blocking"],
              "additionalProperties": false
            }
            """)
        .WithMaxTurns(40)
        .WithAttemptTimeout(TimeSpan.FromMinutes(10));

    // Deny wins over the local allow-all default.
    builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();

    // Guidelines live in the repository and are read through the same sandbox.
    builder.Services.AddSkillTool(o => o.Skills.Add(new SkillDefinition(
        new SkillId("review-guidelines"),
        "Review guidelines",
        "How this team reviews code: naming, tests, error handling, and blocking issues.",
        SkillTrust.Managed,
        new FileSystemPath("docs/review-guidelines.md"))));

    return builder.Build();
}
```

The policy is the one from
[Permissions and approvals](../guides/permissions.md), widened to cover the
network:

```csharp
sealed class ReadOnlyWorkspacePolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        var mutates = request.Kind is SecurityOperationKind.FileWrite
            or SecurityOperationKind.DirectoryCreate
            or SecurityOperationKind.Process
            or SecurityOperationKind.Network;

        return ValueTask.FromResult(mutates
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "ci.read-only", "The reviewer may only read the checkout.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
    }
}
```

`UseWorkspace` registers `write_file` and `edit` as well. Leaving them in and
denying them is deliberate: the model may attempt a change, the request is
denied before the file system is touched, and the tool result tells the model
so. If you prefer the model never to see them, skip `UseWorkspace` and register
`AddSandboxedFileSystem(root)` with only `AddReadTool`, `AddGlobTool`,
`AddSearchTool`, and `AddListTool`.

## Use it

```csharp
sealed record ReviewVerdict(bool Approve, string Summary, string[] Blocking);

using var jobCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(12));
await using var engine = CreateReviewer(checkoutRoot, apiKey);

var request = $"Review these changed files for PR #{prNumber}:\n{string.Join('\n', changedFiles)}";
var result = await engine.SendAsync(request, jobCancellation.Token);

var usage = result.Events.OfType<ConversationUsageEvent>().Select(e => e.Usage).ToList();
Console.WriteLine($"tokens in={usage.Sum(u => u.InputTokens ?? 0)} out={usage.Sum(u => u.OutputTokens ?? 0)} " +
                  $"cost={usage.Sum(u => u.EstimatedCost ?? 0m)} {usage.FirstOrDefault()?.CostCurrency}");

if (result.Output?.Value is not ReviewVerdict verdict)
{
    return 2; // the turn hit a limit, was cancelled, or the answer never validated; the log has the events
}

Console.WriteLine(verdict.Summary);
foreach (var issue in verdict.Blocking)
{
    Console.WriteLine($"  blocking: {issue}");
}

return verdict.Approve ? 0 : 1;
```

`WithOutput<T>` adds a definition-level instruction carrying the schema, and the
loop validates every final answer against it. An answer that is not valid JSON,
or does not match, is sent back to the model with a bounded repair instruction;
after the configured attempts the turn fails rather than returning prose the
pipeline would have to parse.

`EstimatedCost` is populated when the model descriptor carries pricing, which
`UseOpenAI` supplies from the known-model catalog; it stays `null` rather than
becoming zero when the provider or catalog cannot report it.

## What the framework guarantees

- **The sandbox is a hard boundary.** Every path is resolved relative to the
  checkout root and re-validated inside it; symlinks that escape, rooted paths,
  and `..` segments are rejected before the request reaches a policy.
- **Denial happens before the effect.** The write and edit tools build a
  `SecurityRequest`; the policy's `Deny` is final; the file system never sees
  the operation and the tool returns a typed denied result.
- **Skill content is data, not instruction authority.** The guidelines file is
  read through the sandbox with a size ceiling and injected as untrusted context
  the model asked for; it cannot grant a permission.
- **Limits are explicit.** `MaxTurns`, the attempt timeout, and your job token
  each end the run with a distinct outcome on the
  `ConversationTurnCompletedEvent`.

## What lives where

| Concern                        | Package                                                                                         |
| ------------------------------ | ----------------------------------------------------------------------------------------------- |
| Sandboxed file system          | [AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md)                                  |
| Read, list, glob, search tools | `AgentKit.Tools.Read`, `.List`, `.Glob`, `.Search` in the [catalog](../packages/index.md#tools) |
| Skill catalog and `skill` tool | [AgentKit.Tools.Skill](../../src/AgentKit.Tools.Skill/README.md)                                |
| Policy evaluation and grants   | [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)                                |
| Known-model pricing            | [AgentKit.Providers](../../src/AgentKit.Providers/README.md#known-model-catalog)                |

Next: [Documentation maintainer with approval](docs-maintainer-with-approval.md)
· [Working with files](../guides/file-system.md)
