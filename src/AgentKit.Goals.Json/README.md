# AgentKit.Goals.Json

Durable, inspectable, host-local JSONL goal storage for AgentKit.

`AddJsonGoalStore(GoalStoreKey, JsonGoalStoreTarget)` registers one keyed
`IGoalStore` over an explicit, host-authorized root directory. The host chooses
the root; nothing registers it implicitly.

## Durability

Every acknowledged write appends one full goal snapshot to a newline-delimited
log and flushes it to disk before returning, so an acknowledged record survives
process loss. Initialization replays the log through the same shared state
machine the in-memory adapter runs, recovers or refuses a torn trailing append
according to the target's recovery mode, and compacts the log to one snapshot
per goal above a configurable threshold.

The captured authorization of a delegated child is persisted with the goal, so
after a restart `ReadIntentsAsync` lets a host worker rediscover open children
and select the same authority and security profile the delegation was made under
rather than the agent's latest definition.

## Limits

The store holds an advisory exclusive lock on its root and rejects a second
writer. It claims no multi-process coordination, distributed leases, fencing, or
cross-store atomicity. A held lock never proves that an external effect stopped.

Every operation consumes a single-use grant that binds that exact operation
before any state is read or written.
