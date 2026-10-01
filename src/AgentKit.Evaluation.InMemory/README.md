# AgentKit.Evaluation.InMemory

Ephemeral in-process storage for AgentKit evaluation results.

`AddInMemoryEvaluationResultStore(EvaluationResultStoreKey)` registers one keyed
`IEvaluationResultStore`. A plan selects it by key; nothing registers a store
implicitly.

## Behavior

Results are identified by evaluation run, case ordinal, and repetition.
Appending an identical result replays the original acknowledgement; a different
result for that identity, a second plan identity or version for one run, or a
second case at one ordinal is rejected as an identity conflict. Reads page one
run in case-ordinal then repetition order, so order never depends on append
order. The adapter runs the same shared planner and the same result-store
conformance suite as the SQLite and JSON adapters.

## Limits

Results live only in process memory and are lost when the store is disposed or
the process exits. Use `AgentKit.Evaluation.Sqlite` or
`AgentKit.Evaluation.Json` to retain results across restarts.
