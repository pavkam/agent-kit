# AgentKit.Budgets.InMemory

This leaf provides process-local `IBudgetLedger` storage. Select it explicitly
with `AddInMemoryBudgetLedger()` after registering the budget runtime and
dimension catalog. Repeating this leaf is idempotent; a different ledger remains
visible so composition can reject ambiguity.

The adapter atomically stores scope topology, hierarchical reservations, start
evidence, settlement, correction, reconciliation, and recovery watermarks. Its
state is ephemeral and cannot support process-loss recovery. `AgentKit.Budgets`
still contains the older in-memory authority mechanics until the separate
runtime migration replaces them with this ledger.

Each dimension uses one fixed unit throughout a charged scope lineage because
this adapter has no conversion policy. Snapshots report locally configured
limits and locally charged usage; an inherited-only child therefore has an empty
snapshot while its ancestors still enforce admission. Expired, unstarted
reservations are ignored during admission and are removed lazily by successful
state-reading or state-changing operations.

The captured maximum-open-reservations value is an adapter capacity bound across
dimensions, not a budget dimension. Exceeding it throws
`BudgetLedgerStateException` with the ledger contract's no-transition guarantee;
the adapter never fabricates dimension or unit metadata for that capacity
failure.

This storage leaf records truthful reservation and actual-usage facts and
enforces captured finite ledger limits. Runtime authority migration and captured
overrun-policy holds remain separate work; selecting this adapter alone does not
provide the complete budget-authority policy layer.
