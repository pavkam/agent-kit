# AgentKit.Goals.InMemory

Explicitly ephemeral, process-local goal storage for AgentKit.

`AddInMemoryGoalStore(GoalStoreKey)` registers one keyed `IGoalStore`. Nothing
here survives the process, so selecting it is the application's explicit act of
accepting ephemeral goals; no other package ever registers it implicitly.

## Behavior

- Goals, attempts, and transitions are partitioned by tenant. The same identity
  in two tenants is two goals, and one tenant never observes another's.
- Every operation consumes a single-use grant that binds that exact operation
  (goal, version, transition, attempt change) before any state is read or
  written, and refuses a request whose authorized agent or session does not own
  the goal.
- Creation and transitions are idempotent by key. A transition applies only when
  the stored version and status match what the requester observed.
- Children page in recorded child-ordinal order; ordinals are append-only, so
  paging is stable while siblings are created.
- `ReadIntentsAsync` lists open delegated children across tenants for a host
  worker. It is refused unless the host names its scanner in
  `InMemoryGoalStoreOptions.AuthorizedIntentScanners`.

The store runs the shared goal-store conformance suite in
`tests/AgentKit.Conformance`.
