# Workspace snapshots and reversion

**Status:** Normative coding-harness profile  
**Depends on:** [Coding workspaces](coding-workspaces-and-worktrees.md),
[sessions](sessions-persistence-and-branching.md),
[artifacts](artifact-and-content-storage.md)

## Purpose

A filesystem snapshot, a session-state snapshot, a context-compaction
checkpoint, and a UI reconnect snapshot are different things. A coding harness
must name which one it has, what it covers, and what it cannot restore.

The host-selected `IWorkspaceSnapshotCoordinator` is the single runtime owner of
filesystem/workspace snapshot coverage, manifest publication, and restore
coordination for its selected profile. It does not redefine durable session or
compaction contracts.

Composition requires exactly one effective snapshot coordinator for every
workspace profile that enables snapshots; profiles without that capability
expose no snapshot operation. The coordinator captures one immutable workspace,
artifact, security, and host-adapter selection before creation or restore. The
workspace coordinator supplies lifecycle fences, artifact storage owns content
bytes, file-system leaves enforce concrete effects, `AgentKit.Permissions`
authorizes them, and sessions retain only snapshot identities and projections.
None of those collaborators may independently claim that a snapshot is complete
or that restoration settled.

## Snapshot identity and manifest

Every workspace snapshot has a dedicated `WorkspaceSnapshotId`, workspace ID and
fence, creation operation, base revision, parent snapshot when incremental,
algorithm/version, and integrity root. Its manifest classifies every relevant
category:

- tracked, staged, modified, untracked, and ignored paths;
- directories, regular files, symlinks, hard links, special files, and metadata;
- executable bits, permissions, timestamps when retained, encoding-neutral
  bytes, and platform attributes;
- submodules, nested repositories, linked worktrees, sparse-checkout state, and
  Git index/ref state when included;
- oversized, unreadable, volatile, excluded, outside-root, and unsupported
  entries; and
- artifacts or external stores holding snapshot content.

Completeness is separate from operation success. A snapshot that intentionally
excludes ignored files or files over a size threshold may be successfully
created, but it is `PartialCoverage` and reports the exact exclusions. Silence
is not a coverage policy.

## Creation consistency

The creator captures a workspace version/fence and a consistency policy:

- quiesced snapshots acquire an exclusive mutation lease;
- version-checked snapshots may read concurrently but fail if observed paths or
  workspace version change; and
- best-effort diagnostic snapshots label per-entry races and are never eligible
  for automatic restore.

Files are read without following unauthorized links. Content is bounded,
integrity-checked, and committed through artifact storage before the manifest
becomes authoritative. A crash cannot publish a manifest whose referenced
content was never durably stored.

## Restore and revert

Restore is a new destructive workspace-mutation operation. It MUST:

1. validate snapshot identity, integrity, coverage, platform compatibility, and
   retention state;
2. compare the current workspace and produce an exact create/replace/move/delete
   preview;
3. identify paths and effects the snapshot cannot restore;
4. acquire workspace quiescence and authorize the final mutation set;
5. revalidate current preconditions and snapshot artifacts;
6. apply through the normal mutation transaction; and
7. return exact restored, unchanged, skipped, failed, and uncertain entries.

A restore does not swallow errors to make a best-effort rollback look complete.
If the backend lacks atomic multi-path replacement, the result is explicitly
partial-capable and its recovery journal remains durable.

`Revert` may mean restore workspace bytes, move a session branch tip, activate a
context checkpoint, or change a UI projection. APIs MUST use distinct typed
operations rather than one ambiguous verb. Moving a conversation tip never
claims to undo files, commands, deployments, emails, network calls, or provider
charges. Restoring files never deletes canonical session history.

## External and irreversible effects

The snapshot records related effect identities only for explanation. It cannot
reverse process output already consumed, remote repository pushes, package
publishes, database/network mutations, provider calls, approval consumption, or
secrets disclosed. A reversion preview lists these known external effects and
their certainty so the caller does not confuse local reconstruction with global
rollback.

## Fork and retention

Forking from a snapshot creates a new workspace/worktree operation and new
identity; it never aliases mutable snapshot storage. Snapshot references in
sessions and artifacts participate in retention. Deletion is denied while a
retained branch, audit record, or recovery operation still requires the
snapshot, unless an explicit policy first severs that dependency.

## Acceptance scenarios

- Ignored and oversized files appear as explicit exclusions and make coverage
  partial.
- A symlink escaping the workspace is recorded without reading its target.
- Mutation during a version-checked snapshot prevents publication.
- Restore preview includes deletions as well as recreated content.
- Failure halfway through a non-atomic restore returns exact partial effects.
- Session-tip reversion does not alter workspace bytes or claim to undo tools.
- Artifact loss or integrity mismatch stops restore before mutation.
- Snapshot retention protects content referenced by a recoverable operation.

## Coverage and reversion pitfalls

A version-control-backed snapshot may provide efficient diff and restore, but it
does not imply complete workspace coverage. Ignored files, oversized untracked
files, submodules, symlinks, and external state remain explicit exclusions.
Restore failures are never swallowed, and filesystem restoration never claims to
revert process, network, provider, or other external effects.

## Related specifications

- [Workspace mutations and code editing](workspace-mutations-and-code-editing.md)
- [Context compaction](context-compaction.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Coding-harness export, sharing, and control plane](coding-harness-export-sharing-and-control-plane.md)
