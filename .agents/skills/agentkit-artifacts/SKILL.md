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

Use the specification's acceptance scenarios for focused tests, then run the
artifact store's shared conformance suite and DI replacement checks.
