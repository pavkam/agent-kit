# Delegating to specialist agents

A research lead agent takes a broad question, breaks it into parts, and hands
each part to a specialist: one agent that only reads the codebase, one that only
reads the design documents. Each specialist has its own instructions, its own
tools, and its own limits, and reports back a bounded summary. The lead never
gets the specialists' tools, and a specialist cannot start further delegations.

## What the agent needs

| Need                             | AgentKit part                                                                                  |
| -------------------------------- | ---------------------------------------------------------------------------------------------- |
| A way for the lead to delegate   | `AddTaskTool` (`task`) from `AgentKit.Tools.Task`                                              |
| Authorization of each delegation | `AddAgentDelegation` registers the `ITaskDelegationBroker` that obtains and enforces the grant |
| Something that runs the child    | Your `ITaskDelegationChannel`: builds the specialist's engine, runs it, returns a typed result |
| Distinct specialists             | One builder per specialist with its own workspace, policy, instructions, and limits            |

## Compose the lead

```csharp
static AgentEngine CreateLead(SpecialistChannel specialists, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .WithInstructions(
            "You lead a research team. Split the question into focused sub-tasks and " +
            "delegate each with the task tool to agent 'code-reader' (source code) or " +
            "'docs-reader' (design documents). Combine their summaries into one answer.")
        .WithMaxTurns(20);

    builder.Services.AddSingleton<ITaskDelegationChannel>(specialists);
    builder.Services.AddAgentDelegation();
    builder.Services.AddTaskTool(o =>
    {
        o.DefaultMaximumTurns = 15;
        o.DefaultMaximumToolCalls = 40;
        o.DefaultTimeout = TimeSpan.FromMinutes(5);
    });

    return builder.Build();
}
```

The `task` tool exposes `target_agent_id`, `objective`, `acceptance_criteria`,
`allowed_tools`, `max_turns`, `max_tool_calls`, and `timeout_seconds` to the
model, clamps them to the configured ceilings, and asks the broker to delegate.
The broker issues a `SecurityRequest` of kind `Delegation` for the target and
forwards the prompt with the grant to your channel.

## Write the channel

The channel is where a delegation becomes a run. It maps the target agent id to
a specialist composition, runs one turn with the objective, and returns a
`TaskDelegationChildResult` whose summary is all the parent will ever see:

```csharp
sealed class SpecialistChannel(string repoRoot, string designRoot, string apiKey) : ITaskDelegationChannel
{
    public static readonly AgentId CodeReader = new(Guid.Parse("6f1c6d8e-1111-4a00-9c00-000000000001"));
    public static readonly AgentId DocsReader = new(Guid.Parse("6f1c6d8e-1111-4a00-9c00-000000000002"));

    public async ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationPrompt prompt, CancellationToken cancellationToken = default)
    {
        var builder = prompt.TargetAgentId switch
        {
            var id when id == CodeReader => Specialist(repoRoot, "You answer questions about the source code only.", prompt),
            var id when id == DocsReader => Specialist(designRoot, "You answer questions about the design documents only.", prompt),
            _ => null,
        };
        if (builder is null)
        {
            return new TaskDelegationRejected(prompt.Id, "Unknown specialist.");
        }

        using var deadline = new CancellationTokenSource(prompt.Deadline - DateTimeOffset.UtcNow);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        await using var child = builder.Build();

        var objective = $"{prompt.Objective}\n\nAcceptance criteria:\n- {string.Join("\n- ", prompt.AcceptanceCriteria)}";
        var result = await child.SendAsync(objective, linked.Token);
        var summary = string.Concat(result.Events.OfType<ConversationAssistantTextEvent>().Select(e => e.Text));
        var completed = result.Events.OfType<ConversationTurnCompletedEvent>().Last();

        return new TaskDelegationChildResult(
            prompt.Id,
            new GoalId(Guid.NewGuid()),
            prompt.TargetAgentId,
            childSessionId: result.SessionId!.Value,   // the session the child turn was recorded against
            childAttemptId: null,
            childRunId: result.RunId,
            completed.Outcome switch
            {
                "settled" when result.Succeeded => TaskDelegationStatus.Succeeded,
                "cancelled" => TaskDelegationStatus.Cancelled,
                _ => TaskDelegationStatus.Failed,
            },
            summary.Length <= 16_000 ? summary : summary[..16_000],
            SideEffectCertainty.DefinitelyNotPerformed);
    }

    AgentEngineBuilder Specialist(string root, string instructions, TaskDelegationPrompt prompt)
    {
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI(apiKey, "gpt-4o-mini")
            .UseWorkspace(root)
            .WithAgentId(prompt.TargetAgentId)
            .WithIdentity(prompt.Identity)
            .WithInstructions(instructions)
            .WithMaxTurns(prompt.Budget.MaximumTurns);

        builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();
        return builder;
    }
}
```

The child engine carries the parent's identity (`prompt.Identity`), the parent's
chosen limits (`prompt.Budget`), the parent's deadline, and a read-only policy.
It has no `task` tool, so a specialist cannot delegate again.
`ReadOnlyWorkspacePolicy` is the one from
[Read-only code review in CI](code-review-in-ci.md).

## Decide who may delegate

Delegation is a protected operation of kind `SecurityOperationKind.Delegation`
whose resource is the delegation itself (`delegation:<id>`); the target agent,
objective, budget, and deadline are bound into the request fingerprint. A policy
therefore decides _whether this identity may delegate at all_, and the channel
decides _to whom_, as the `switch` above does. To forbid delegation for some
callers:

```csharp
sealed class NoDelegationForGuestsPolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(request.Kind == SecurityOperationKind.Delegation && request.Identity.TenantId == new TenantId("guest")
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "lead.no-guest-delegation", "Guest sessions cannot delegate work.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
}
```

## Use it

```csharp
var specialists = new SpecialistChannel("/src/acme", "/docs/acme-design", apiKey);
await using var lead = CreateLead(specialists, apiKey);

var result = await lead.SendAsync(
    "Does the retry policy implemented in the payment client match what the design says it should be?",
    new ConsoleProgress(),
    cancellationToken);
```

The observer sees two `task` calls, each with a `ConversationToolResultEvent`
whose `Summary` is the specialist's report, followed by the lead's combined
answer.

## What the framework guarantees

- **Delegation is a protected operation.** The `task` tool obtains a grant whose
  fingerprint binds the exact target, objective, budget, and deadline; the
  broker consumes that grant once, immediately before your channel runs. A
  policy `Deny` or a stale grant ends in a typed rejected result and no child
  run.
- **Budgets and deadlines are narrowed, never widened.** The `task` tool clamps
  the model's requested turns, tool calls, and timeout to the configured
  ceilings; the child builder applies them as its own limits.
- **The parent sees a summary, not a transcript.** `TaskDelegationChildResult`
  carries a bounded `Summary`, a `TaskDelegationStatus`, and a
  `SideEffectCertainty`; the child's tools, messages, and grants never flow back
  into the parent's context.
- **Identity is carried, not minted.** The child runs as the same principal that
  asked the lead; delegation does not manufacture a new authority.

## Status

The broker (`AgentKit.Goals`) and the `task` tool are implemented; the channel
and the child composition are the host's, as shown. What the architecture
describes beyond this, durable goal and attempt records, joins over several
children, and worker hosting for detached child work, is tracked in the
[implementation ledger](../implementation-progress.md#component-coverage). Until
then a child is a synchronous run inside the parent's tool call, bounded by the
deadline it was given.

## What lives where

| Concern                    | Package                                                                             |
| -------------------------- | ----------------------------------------------------------------------------------- |
| `task` tool                | [AgentKit.Tools.Task](../../src/AgentKit.Tools.Task/README.md)                      |
| Delegation broker          | [AgentKit.Goals](../../src/AgentKit.Goals/README.md)                                |
| Delegation contracts       | [AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md)                  |
| Normative delegation rules | [Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md) |

Next: [Testing an agent without a live model](testing-agents-offline.md) ·
[Order lookup with your own tool](domain-tool-integration.md)
