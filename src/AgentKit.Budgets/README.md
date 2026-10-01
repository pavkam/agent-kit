# AgentKit.Budgets

Reserve and account for capacity across hierarchical budget scopes.

Use budgets to bound tokens, cost, and other declared dimensions across
concurrent operations. The first-party authority is a stateless runtime over an
explicitly selected `IBudgetLedger`; consumers borrow lightweight scope and
reservation handles while the ledger owns all authoritative accounting.

## Use this project

Start with `AddAgentBudgets`, `AddBudgetProfile`, `AddBudgetDimension`, and the
`Replace*` helpers in [ServiceExtensions.cs](ServiceExtensions.cs). Read the
overload XML for required collaborators, lifetimes, and duplicate-registration
behavior.

`AddAgentBudgets` does not select storage. Applications using the first-party
runtime must register exactly one unkeyed ledger adapter, such as
`AddInMemoryBudgetLedger` from `AgentKit.Budgets.InMemory`. Selecting none or
more than one fails when the first-party authority is activated, before any
ledger operation. Replacing `IBudgetAuthority` removes that ledger requirement.

Captured overrun policy and truthful held outcomes are supported. The agent loop
consumes the authority: every definition selects a run budget profile through
`AgentComponentSelection.BudgetProfile` and gets one run scope whose limits come
from that profile (a profile with no limits bounds nothing), reservations before
each turn, model request, and tool call, and post-response accounting of
reported tokens and cost, settling as `RunLimitReached` naming the dimension
when a reservation is refused with full evidence, or `RunFailed` when the
authority reports a held overrun or an unsupported outcome with no such
evidence. `BudgetUnknownCostBehavior` governs unknown cost reservations on the
`agentkit.cost` dimension through
`BudgetReservationRequest.CostEstimateUnknown`. Named profiles resolve through
`IBudgetProfileCatalog`; the loop publishes a run-scoped
`BudgetExecutionCapability` for output repair, tools, and other consumers.
`RunBudget` implements `IRunBudget` for the owned run scope handle. Parent
host/tenant scopes remain application-owned.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Budgets.Tests](../../tests/AgentKit.Budgets.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/budgets.md) — intended
  ownership and contracts.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
