# AgentKit.Evaluation.Json

Durable, inspectable, host-local JSONL storage for AgentKit evaluation results.

`AddJsonEvaluationResultStore(EvaluationResultStoreKey, JsonEvaluationStoreTarget)`
registers one keyed `IEvaluationResultStore` over an explicit, host-authorized
root directory. The host chooses the root; nothing registers it implicitly, and
each store needs its own root.

## Durability

Every acknowledged append writes one newline-delimited JSON record and flushes
it to disk before returning, so an acknowledged result survives process loss.
Initialization replays the log through the same shared planner the in-memory
adapter runs, recovers or refuses a torn trailing append according to the target
recovery mode, and refuses a log whose records conflict with each other. The
manifest binds the root to its expected instance identity and encoding contract,
so a root is never opened under the wrong configuration or decoded under another
contract.

A stored record is a bounded projection: identities, manifest, usage, latency,
trace identity, evaluator outcomes with their safe evidence, and diagnostics.
Prompts, model output, and tool data are never persisted.

## Limits

The store holds an advisory exclusive lock on its root and rejects a second
writer. It claims no multi-process coordination, distributed leases, fencing, or
atomicity with report exporters.
