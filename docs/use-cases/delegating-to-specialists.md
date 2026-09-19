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

## Compose the engine

All three agents live on one engine. The lead is the builder's default agent;
the specialists are added with `AddAgent` and share the model, identity, and
security profile while owning their instructions, tools, and limits:

```csharp
static readonly AgentId CodeReader = new(Guid.Parse("6f1c6d8e-1111-4a00-9c00-000000000001"));
static readonly AgentId DocsReader = new(Guid.Parse("6f1c6d8e-1111-4a00-9c00-000000000002"));

static AgentEngine CreateTeam(string repoRoot, string designRoot, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .UseWorkspace(repoRoot)
        .WithInstructions(
            "You lead a research team. Split the question into focused sub-tasks and " +
            "delegate each with the task tool to agent 'code-reader' (source code) or " +
            "'docs-reader' (design documents). Combine their summaries into one answer.")
        .WithMaxTurns(20)
        .AddAgent(CodeReader, o =>
        {
            o.DisplayName = "code-reader";
            o.Instructions.Add("You answer questions about the source code only. Read; never write.");
            o.MaxTurns = 15;
        })
        .AddAgent(DocsReader, o =>
        {
            o.DisplayName = "docs-reader";
            o.Instructions.Add("You answer questions about the design documents only. Read; never write.");
            o.MaxTurns = 15;
        });

    // The task tool, the delegation broker, and the engine-backed channel that runs the child.
    builder.WithDelegation(o =>
    {
        o.DefaultMaximumTurns = 15;
        o.DefaultMaximumToolCalls = 40;
        o.DefaultTimeout = TimeSpan.FromMinutes(5);
    });

    // Nobody on this engine may write or run anything.
    builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();

    return builder.Build();
}
```

The `task` tool exposes `target_agent_id`, `objective`, `acceptance_criteria`,
`allowed_tools`, `max_turns`, `max_tool_calls`, and `timeout_seconds` to the
model, clamps them to the configured ceilings, and asks the broker to delegate.
The broker issues a `SecurityRequest` of kind `Delegation` and forwards the
prompt with the grant to the channel.

## What the channel does

`WithDelegation` registers `EngineDelegationChannel`, the first-party
`ITaskDelegationChannel`. It resolves the target through
`engine.GetAgentAsync(prompt.TargetAgentId)`, so only agents published on this
engine can be delegated to; sends the objective and acceptance criteria as one
turn of that agent in a new session under the parent's identity, bounded by the
prompt's deadline and the narrower of its turn budget and the target's own
limit; and returns a `TaskDelegationChildResult` carrying the child's real
`SessionId` and `RunId`, a status mapped from the run outcome, the child's final
answer bounded to `MaximumSummaryCharacters`, and a side-effect certainty
derived from the child's tool results. An unknown target, an expired deadline,
or a rejected admission is a typed `TaskDelegationRejected`.

To write your own channel (for example to run children on another engine or
through a queue), implement `ITaskDelegationChannel` and register it before
calling `WithDelegation`; the sugar's `TryAdd` keeps yours.

The specialists have no `task` tool, so a specialist cannot delegate again, and
the shared `ReadOnlyWorkspacePolicy` (from
[Read-only code review in CI](code-review-in-ci.md)) denies every write and
process for all three. Because each delegation is a new session, the engine runs
concurrent delegations from one lead turn in parallel.

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
await using var engine = CreateTeam("/src/acme", "/docs/acme-design", apiKey);

var result = await engine.SendAsync(
    "Does the retry policy implemented in the payment client match what the design says it should be?",
    new ConsoleProgress(),
    cancellationToken);
```

The observer sees two `task` calls, each with a `ConversationToolResultEvent`
whose `Summary` is the specialist's report, followed by the lead's combined
answer. `engine.GetAgentsAsync()` lists all three agents, and each specialist's
sessions are visible through the engine's session directory.

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

The broker, the `task` tool, multi-agent hosting on one engine, and the
engine-backed channel are implemented. What the architecture describes beyond
this, durable goal and attempt records, joins over several children, and worker
hosting for detached child work, is tracked in the
[goals and delegation workstream](../workstreams/goals-and-delegation.md). Until
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
