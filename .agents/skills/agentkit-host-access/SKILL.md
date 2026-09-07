---
name: agentkit-host-access
description:
  "Design, implement, or debug AgentKit filesystem, network, or process
  contracts and host adapters. Use for protected low-level host effects; not
  higher-level tool, provider, or MCP semantics."
---

# AgentKit Host Access

Read [AGENTS.md](../../../AGENTS.md), then load only the affected surface. Each
surface has one normative concept specification and one architecture page; read
the concept for required behavior and the architecture page for package
ownership and registration:

- file and directory work:
  [file-system access and bounds](../../../docs/concepts/file-system-access-and-bounds.md)
  and [file-system architecture](../../../docs/architecture/file-system.md);
- DNS, connections, requests, redirects, or egress:
  [network access and egress](../../../docs/concepts/network-access-and-egress.md)
  and [network architecture](../../../docs/architecture/network.md);
- executable resolution, sandboxing, standard streams, or termination:
  [process execution and sandboxing](../../../docs/concepts/process-execution-and-sandboxing.md)
  and [process architecture](../../../docs/architecture/process-execution.md).

For coding-harness work, load the focused profile that owns the requested host
surface:

- [workspaces and worktrees](../../../docs/profiles/coding-harness/coding-workspaces-and-worktrees.md)
  for canonical roots, provisioning, leases, reset, or removal;
- [workspace mutations](../../../docs/profiles/coding-harness/workspace-mutations-and-code-editing.md)
  for edits, patches, moves, formatting, and final-byte authorization;
- [terminals](../../../docs/profiles/coding-harness/interactive-terminals-and-process-sessions.md)
  for PTY ownership, output cursors, attach, and process-tree cleanup;
- [language services](../../../docs/profiles/coding-harness/language-services-formatters-and-watchers.md)
  for LSP, formatter, code-action, or watcher lifecycle; and
- [workspace snapshots](../../../docs/profiles/coding-harness/workspace-snapshots-and-reversion.md)
  for filesystem coverage, restore, and external-effect boundaries.

For protected effects also read the
[security specification](../../../docs/concepts/permissions-approvals-and-trust.md).
For timeout, cancellation, or retry work read the
[resilience specification](../../../docs/concepts/cancellation-timeouts-and-resilience.md).
When changing C#, read the [modern C# rules](../references/modern-csharp.md).

## Boundary

- Keep file, network, and process contracts separate in AgentKit.Abstractions.
  Do not create a universal host-access interface or let one grant imply
  another.
- Real and deterministic test implementations belong to their focused packages;
  higher-level consumers depend only on the narrow contracts they need.
- Coding-workspace directory, lifecycle, and snapshot coordinators are optional
  host-selected orchestration contracts, not hidden file-system features.
  Composition requires exactly one effective coordinator per selected profile;
  the coordinator still performs every effect through the focused host and
  security contracts.
- Canonicalize the exact target and bounds before authorization. The effecting
  implementation revalidates and consumes the bounded grant immediately before
  each material host effect.
- Enforce read and write bounds against bytes actually consumed, never metadata
  alone or full-read-then-slice. Text profiles declare encoding, BOM, newline,
  and malformed-input behavior.
- File writes require an explicit `CreateOnly`, `ReplaceExisting`,
  `CreateOrReplace`, or `Append` disposition with atomic target-state behavior.
  Bound append by final size, preserve non-interleaving/side-effect certainty,
  and report created/replaced/appended sizes plus payload/final fingerprints.
  Empty and whitespace-only payloads are valid.
- Parent-directory creation is a separate declared, authorized, and audited
  effect; never smuggle it into file writing for convenience.
- Sandboxing and transport restrictions reduce consequences but never grant
  permission. Changed paths, destinations, redirects, executable inputs, or
  scopes require reevaluation.
- Declare capabilities, ownership, disposal, cancellation, backpressure, and
  unsupported behavior explicitly; never fall back to raw host APIs.
- Test denial before effects and run the same conformance suite against the
  deterministic and real implementation using isolated fixtures. Cover target
  races, missing/existing disposition matrices, concurrent append, actual stream
  overflow, text formats, fingerprints, and cancellation atomicity.

Keep lexical normalization separate from protected observation. Atomic
publication, conditional target-state commit, and crash durability are distinct;
cooperative locks cannot prove safety against independent writers. Pooled
network peers satisfy each send's grant, and a full-fingerprint one-pass body is
staged before egress under separate authority.

Load more than one surface document only when the requested operation truly
crosses those boundaries, and preserve their independent authorization.
