# Coding workspaces and worktrees

**Status:** Normative application profile

**Scope:** Optional application composition; not a required AgentKit capability.

**Depends on:** [Coding harness execution](coding-harness-execution-profile.md),
[permissions](../../concepts/permissions-approvals-and-trust.md),
[sessions](../../concepts/sessions-persistence-and-branching.md)

## Purpose

A coding workspace is the durable identity and lifecycle boundary around a
canonical project root. A worktree is one possible isolated checkout attached to
that workspace. Neither is merely a path string, and neither may be created,
reset, or removed as an incidental tool helper.

This profile composes session, file-system, process, security, and
host-lifecycle contracts. It does **not** create a universal workspace manager
package.

## Runtime owner and composition

The application selects one workspace profile and a host implementation of the
provider-neutral `IWorkspaceDirectory` and `IWorkspaceCoordinator` contracts in
`AgentKit.Abstractions`. The directory owns descriptor registration and
versioned lookup. The coordinator is the single runtime owner of provisioning,
leases, fencing, readiness, reset, removal, and disposal for that profile. A
standalone engine disposes the coordinator it builds; a hosted engine leaves
disposal to its service provider.

Composition validation requires exactly one effective workspace directory and
one coordinator for every selected workspace-profile key. Multiple keyed
profiles may coexist, but an operation captures one immutable selection before
admission and cannot switch coordinators midway through an effect. If workspace
support is not selected, neither contract is required and workspace operations
are unavailable.

This ownership does not absorb neighboring authorities:

- file-system and process leaves perform and revalidate concrete host effects;
- `AgentKit.Permissions` decides authority and issues bounded grants;
- sessions retain workspace identities, descriptor revisions, and operation
  references without owning checkout lifecycle; and
- artifact storage owns durable snapshot bytes without deciding workspace
  coverage or restore policy.

The coordinator depends inward on those narrow contracts. It never reaches
around them to raw host APIs, and no session, artifact, file-system, or process
implementation depends back on the coordinator.

## Identity and ownership

A workspace descriptor MUST record at least:

- a dedicated `WorkspaceId` and immutable owner or tenant scope;
- canonical root identity, including volume/device identity where available;
- repository kind, repository root, and selected worktree root;
- base revision, checked-out revision, branch/ref policy, and upstream identity;
- creation provenance and selected workspace profile revision;
- trust state, security profile, and allowed host capabilities; and
- lifecycle state, lease/fence identity, and latest transition outcome.

User-facing names and paths are labels. A destructive operation names a
registered `WorkspaceId` and expected descriptor version. It MUST NOT accept an
arbitrary caller-supplied directory and recursively delete it merely because the
path looks like a worktree.

Canonicalization is symlink-aware and platform-aware. Containment is evaluated
against resolved identities, not lexical prefix tests. Case folding, Unicode
normalization, drive roots, UNC paths, mount points, junctions, and repository
symlinks are explicit profile decisions. A path alias does not create a second
workspace identity or a new trust decision.

## Lifecycle

The minimum lifecycle is:

```text
registered -> provisioning -> bootstrapping -> ready
ready -> draining -> resetting -> ready
ready | failed -> draining -> removing -> removed
provisioning | bootstrapping | resetting -> failed
```

`Ready` means all required checkout, submodule, dependency, bootstrap, and
validation work has terminally settled. Returning `Ready` while startup scripts
continue in the background is forbidden. Optional background preparation uses a
different observable state and cannot be required for correctness.

Each transition records intent before effects, exact affected resources,
progress suitable for recovery, and one terminal outcome. Failure describes the
resources that exist and the reconciliation action required; it does not pretend
the workspace returned to its prior state.

## Provisioning

Provisioning MUST:

1. validate the requested repository, base revision, destination policy, and
   ownership before effects;
2. reserve a stable workspace identity and collision-safe destination;
3. authorize repository reads, destination creation, network fetches, and every
   process separately;
4. create the checkout without exposing it as ready;
5. verify the resulting repository identity and selected revision;
6. run pinned, trusted bootstrap operations through normal process execution;
7. validate required tools and workspace invariants; and
8. atomically publish the ready descriptor or a complete failed outcome.

Bootstrap commands are structured executable/argument/environment/working-
directory requests. Raw shell strings, ambient full-environment inheritance,
package lifecycle scripts, and moving `latest` downloads require explicit policy
and authorization. Installation and migration are operations, not configuration
reads.

## Leases and quiescence

Interactive runs, terminals, language servers, watchers, formatters, and active
mutations hold workspace-scoped leases. Reset, move, and removal first close
admission, acquire an exclusive fence, and reach a documented quiescence policy.
Killing the parent process is not proof that descendants, remote jobs, or file
effects stopped.

A stale holder cannot publish workspace events after a newer fence is active.
Cross-process hosts use durable leases and fencing tokens; process-local locks
are insufficient when more than one host can reach the same root.

## Reset and removal

Before destructive reset or removal, the harness builds and presents an exact
target inventory:

- registered checkout and administrative metadata roots;
- tracked, untracked, ignored, modified, and staged paths;
- nested repositories, submodules, linked worktrees, symlinks, and mount points;
- active leases, terminals, language servers, and background processes; and
- external effects that the operation cannot reverse.

The security request binds this inventory, expected workspace version, and
destructive mode. A lower-level file or process adapter revalidates the target
immediately before each effect. Newly discovered targets stop the operation or
require a new grant.

`Reset` MUST state whether it fetches, changes refs, hard-resets tracked files,
deletes untracked or ignored files, updates submodules, or runs hooks. Those are
independent effects and default to off. Removal deletes only roots registered in
the descriptor and never follows links outside them.

Partial destructive completion is a terminal `Partial` or `Reconciliation`
outcome with a journal of completed and pending effects. Rollback is claimed
only when the implementation proves it.

## Relationship to sessions

A workspace may host many sessions and a session records the `WorkspaceId` and
descriptor revision it observed. Forking a conversation does not implicitly fork
a checkout. Creating a worktree for a branch is an explicit operation with its
own identity, permissions, lifecycle, and cleanup policy.

Workspace disposal and session archival are distinct. Removing a worktree does
not delete session history or artifacts; deleting a session does not remove a
checkout unless a separately authorized retention policy says so.

## Acceptance scenarios

- Symlink aliases and case variants resolve to one workspace identity.
- Removal rejects an unregistered path even when it is inside the repository.
- Bootstrap failure never publishes `Ready` and reports residual resources.
- A running terminal prevents reset until the configured quiescence policy
  settles it.
- Reset authorization over a target inventory cannot delete a path discovered
  after the grant.
- Concurrent provision requests with the same idempotency key create one
  workspace.
- A stale fenced host cannot publish a later workspace transition.
- Session archival and worktree removal remain independently recoverable.

## Provisioning and destructive-boundary requirements

Multi-worktree provisioning, instance scoping, and startup hooks remain separate
operations with captured inputs and identities. Worktree removal accepts only a
registered `WorkspaceId`; a caller-supplied path cannot authorize recursive
deletion merely because it sits beneath a repository. Reset and startup never
combine destructive version-control operations and shell execution behind one
undifferentiated grant.

## Related specifications

- [Workspace mutations and code editing](workspace-mutations-and-code-editing.md)
- [Workspace snapshots and reversion](workspace-snapshots-and-reversion.md)
- [Interactive terminals and process sessions](interactive-terminals-and-process-sessions.md)
- [Coding-harness resources and project trust](coding-harness-resources-and-project-trust.md)
