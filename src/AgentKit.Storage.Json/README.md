# AgentKit.Storage.Json

Shared JSON and JSONL storage machinery for AgentKit's durable host-local store
adapters.

This package is family machinery, not a selectable store. It contains no
`IServiceCollection` registration and no `ISessionStore`, `ISecurityGrantStore`,
`IApprovalStore`, or `IBudgetLedger` implementation. Applications select a
concrete leaf instead:

- `AgentKit.Permissions.Json`
- `AgentKit.Budgets.Json`
- `AgentKit.Session.Json`

## What it owns

**File mechanics.** `JsonStoreRoot` resolves and validates one store root,
rejecting a path reached through a symbolic link or reparse point.
`JsonAtomicDocument` replaces a whole document through a same-directory
temporary file and rename, so a reader never observes a partial write.
`JsonRecordLog` appends newline-delimited records and flushes each one to disk
before acknowledging it. `JsonStoreLock` holds a host-local advisory exclusive
lock so a second writer fails fast instead of interleaving appends.

**Encoding contract.** `JsonStoreSerialization.CreateCanonicalOptions` builds
the strict baseline contract: camel-cased names, string enumerations without
integer fallback, strict numbers, no comments or trailing commas, and rejection
of unmapped members. `JsonEncodingOptions` lets a host replace any of this
through a full `JsonSerializerOptions` passthrough. `JsonEncodingSettings`
freezes the result and derives `JsonFormatFingerprint`, which a leaf records in
its manifest so a root written under one contract is never decoded under
another.

**Evidence shapes.** Portable DTOs for the identity and authorization graph
shared by every store family, so `SecurityGrant`, session recovery records, and
budget scope evidence encode identically wherever they are persisted.

## Durability

Leaves built on this package advertise durable single-process host-local storage
with crash-safe writes. An acknowledged record survives process loss. A torn
trailing append is recoverable, because a record is acknowledged only after its
terminating newline reaches disk.

This package makes no distributed claim. It provides no lease, no fencing token,
and no cross-store or cross-directory atomicity, and a held lock never proves
that an external effect stopped.
