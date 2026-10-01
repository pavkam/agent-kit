# AgentKit.Goals.Sqlite

Durable, host-local SQLite goal storage for AgentKit.

`AddSqliteGoalStore(GoalStoreKey, SqliteGoalStoreTarget)` registers one keyed
`IGoalStore` over an explicit, host-authorized database file. The host chooses
the path, whether it may be created, and whether the schema may be created;
nothing registers a database implicitly.

## Behavior

Each goal is one row holding its aggregate, with indexed columns for tenant,
parent, creation sequence, child ordinal, status, and settlement sequence. Every
mutation runs in an immediate transaction that reads stored state through the
shared planner and writes the result, so the version check, idempotent replay,
sequence allocation, and child ordinal are atomic even across processes that
share the file. An acknowledged write is committed and survives process loss.

The captured authorization of a delegated child is persisted with the goal, so
`ReadIntentsAsync` lets a restarted host worker rediscover open children and
select the same authority and security profile the delegation was made under.

## Limits

SQLite provides durable local storage only. It implies no distributed lease,
fencing, or cross-store atomicity, and a held database lock never proves that an
external effect stopped. Connections are unpooled, so no file handle outlives a
transaction.

Every operation consumes a single-use grant that binds that exact operation
before any transaction opens.
