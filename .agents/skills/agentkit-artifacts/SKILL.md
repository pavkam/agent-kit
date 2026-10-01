---
name: agentkit-artifacts
description:
  "Design, implement, or review AgentKit artifact and content storage,
  references, integrity, retention, and backend adapters. Use for durable binary
  or generated content; not session history, memory, or raw filesystem access."
---

# AgentKit Artifacts

Read [AGENTS.md](../../../AGENTS.md), the
[artifact architecture](../../../docs/architecture/artifacts.md), and the
[normative artifact specification](../../../docs/concepts/artifact-and-content-storage.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

For a coding host, read the focused profile when artifacts hold
[truncated tool output](../../../docs/profiles/coding-harness/coding-harness-built-in-tools.md),
[workspace snapshots](../../../docs/profiles/coding-harness/workspace-snapshots-and-reversion.md),
or
[session exports/shares](../../../docs/profiles/coding-harness/coding-harness-export-sharing-and-control-plane.md).

## Boundary

- Keep portable identities, references, metadata, requests, results, and store
  contracts in AgentKit.Abstractions. AgentKit.Artifacts owns coordination,
  integrity, retention, selection, and reconciliation; backends are leaves.
- Artifacts are bounded durable content, not conversation history, durable
  memory, open streams, provider handles, signed URLs, or host paths.
- Preserve tenant and owner scope, classification, media type, length, hash,
  version, retention, and mutability in every durable reference.
- Make writes explicitly prepare/finalize, bounded, cancellable, and integrity
  checked. A partial upload is never readable as committed content.
- Keep artifact storage one-way: callers coordinate session or tool-reference
  commits. The artifact runtime never appends those records itself.
- Route protected backend effects through security and the selected file or
  network boundary; every real backend revalidates its bounded grant.
- Define stream ownership, orphan reconciliation, tombstones, external
  ownership, and deletion semantics instead of pretending cross-store atomicity.

Reference commitment uses a caller-owned durable intent and a pin/retention
fence. Timer expiry alone cannot prove an orphan or authorize garbage collection
that races a late reference commit. Finalize and abort have one conditional
winner; artifact storage never calls back into the referencing coordinator.

## Composition

- `AddAgentArtifacts(key, profileKey)` registers one keyed coordinator bound to
  a versioned `AddArtifactProfile` whose routes name `ArtifactBackendKey`s; the
  application registers every keyed `IArtifactStore` (`.InMemory`, `.Sqlite`,
  `.Json`, or `.FileSystem`) explicitly. No registration order selects a store.
- `AgentOptionalCapabilitySelection.ArtifactCoordinator` selects the coordinator
  per agent. The facade's `ArtifactCompositionValidator` reads
  `IArtifactCoordinatorCatalog` evidence and requires the keyed coordinator and
  a keyed store for every routed backend; it never resolves either.
- Finalize, abort, and reconcile carry no directory, so the coordinator probes
  the profile's distinct backends in deterministic order and relies on the
  store's conditional transition as the single winner.
- Storage adapters share the planner, state machine, gateway, and enforcement in
  `AgentKit.Artifacts.Storage.Shared` (compiled into each leaf) and run one
  conformance suite.

Use the specification's acceptance scenarios for focused tests, then run the
artifact store's shared conformance suite and DI replacement checks.
