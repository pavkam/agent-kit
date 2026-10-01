# AgentKit.Goals

Coordinate goals, delegation, and joins.

`AddAgentGoals` registers the runtime: the goal and delegation coordinators, the
profile registry and catalog, target discovery and selection, the delegation
policy pipeline with its deny-unless-authorized baseline, the child-budget
manager, the goal-event dispatcher, and the five first-party join strategies. It
registers no store and no dispatcher: a goal profile names them explicitly.

```csharp
services.AddAgentGoals();
services.AddInMemoryGoalStore(new GoalStoreKey("memory"));          // or .Json / .Sqlite
services.AddLocalDelegationDispatcher(new DelegationDispatcherKey("local"));
services.AddGoalProfile(new GoalProfileKey("default"), options =>
{
    options.StoreKey = new GoalStoreKey("memory");
    options.DispatcherKey = new DelegationDispatcherKey("local");
});
```

## Use this project

Start with `AddAgentGoals` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Behavior

- **One ordered gauntlet.** A delegation resolves its captured profile, the
  active parent goal, exactly one target, the policy pipeline (which can only
  narrow), the join strategy and dispatcher, and a child budget reserved inside
  the parent's, then obtains one single-use grant bound to the exact request.
  Only then is the child goal created, and every step fails closed before a
  child exists.
- **Idempotent by key.** Child identities derive from the parent goal and the
  request's idempotency key, so a retried request resolves to the one child and
  a settled child is reported from durable state.
- **The dispatcher never runs the child.** `LocalDelegationDispatcher` commits a
  ready child goal and signals a host worker; `AgentKit.Goals.Hosting` claims
  and runs it. The parent waits on durable state and parks its session's worker
  slot while it waits.
- **Joins decide from durable evidence.** All-results, ordinal-first-success,
  fastest-valid-success (recorded winner), quorum, and best-effort strategies
  are pure functions of child ordinals, statuses, eligibility, and settlement
  sequences; none reads completion order or a clock.
- **A session-backed store** (`AddSessionBackedGoalStore`) projects goals over
  the selected session contracts. It persists no delegation authorization, so it
  cannot discover intents across sessions; use it with the signal only.

## Related projects

- [AgentKit.Goals.Hosting](../AgentKit.Goals.Hosting/README.md) — the worker
  that runs delegated children.
- [AgentKit.Tools.Task](../AgentKit.Tools.Task/README.md) — the model-facing
  `task` tool over the delegation coordinator.
- [AgentKit.Permissions](../AgentKit.Permissions/README.md) — evaluate security
  requests and manage bounded grants, profiles, and audit dispatch.
- [AgentKit.Budgets](../AgentKit.Budgets/README.md) — reserve and account for
  capacity across hierarchical budget scopes.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Goals.Tests](../../tests/AgentKit.Goals.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/goals-and-delegation.md) —
  intended ownership, contracts, and recorded deviations.
- [Workstreams](../../docs/workstreams/index.md) — how this component was built,
  chunk by chunk.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
