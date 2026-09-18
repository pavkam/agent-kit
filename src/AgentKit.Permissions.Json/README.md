# AgentKit.Permissions.Json

Durable host-local JSON storage for AgentKit's security control plane. It
provides two independently selectable leaves:

- `JsonSecurityGrantStore` implements `ISecurityGrantStore`.
- `JsonApprovalStore` implements `IApprovalStore`.

Both are log structured. Every state change is one JSON line appended to a
newline-delimited record log and flushed to disk before the operation is
acknowledged, so an acknowledged transition survives process loss.

## Selecting it

The security runtime registers no concrete store, so the host chooses one
explicitly and supplies its persistence target. Nothing is defaulted.

```csharp
services.AddJsonSecurityGrantStore(
    new JsonSecurityGrantStoreTarget(
        "/var/lib/myapp/security/grants",
        new JsonSecurityGrantStoreInstanceId(deploymentStoreId),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends));
```

Trusted bootstrap then resolves the store and calls `InitializeAsync` once
before first use. Construction performs no I/O; initialization acquires the
lock, validates the manifest, and replays the log.

## Atomicity

A consumption that produces an enforcement receipt writes the new remaining-use
count and the receipt on the same line, so recovery can never observe a consumed
use without its receipt. Presenting the same enforcement intent again returns
`Reconciled` with the historical receipt, which is evidence of the earlier
attempt and never fresh authority to repeat the effect.

Operations that change nothing — re-registering identical grant evidence,
revoking an already revoked grant, or an approval conflict — append nothing.

## Encoding

`JsonSecurityGrantStoreOptions.Encoding` exposes the full
`JsonSerializerOptions` contract, so a host may change naming, converters, or
number handling. Because that makes the on-disk format caller-defined, the store
protects existing evidence two ways: the effective contract's fingerprint is
recorded in the store manifest and a root written under a different contract is
rejected, and initialization round-trips a representative probe so an unusable
contract fails at bootstrap instead of while committing authority evidence.

Indentation is honored for the manifest and always suppressed for record logs,
where one record must occupy one line.

## Durability claims

Durable single-writer host-local storage. An acknowledged record is flushed
before acknowledgement, and a torn trailing append is recovered under
`RecoverTornAppends`. The store holds an advisory exclusive lock for its
lifetime, so a second writer on the same host fails fast rather than
interleaving appends.

It claims no distributed lease, no fencing token, and no atomicity with any
external effect. Consuming a grant is not atomic with the protected operation it
authorizes.

See also `AgentKit.Permissions.InMemory` for explicitly ephemeral evidence and
`AgentKit.Permissions.Sqlite` when multi-process coordination is required.
