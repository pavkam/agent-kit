# Language services, formatters, and watchers

**Status:** Normative application profile

**Scope:** Optional application composition; not a required AgentKit capability.

**Depends on:** [Coding workspaces](coding-workspaces-and-worktrees.md),
[workspace mutations](workspace-mutations-and-code-editing.md),
[streaming](../../concepts/streaming-and-event-protocol.md)

## Purpose

Language servers, formatters, linters, code-action providers, and file watchers
are long-lived host capabilities. They are not invisible best-effort helpers.
Their installation, process lifecycle, workspace scope, consistency model, and
failures affect edits and model-visible evidence.

These services compose tool, file-system, process, I/O, artifact, and security
contracts. A protocol-specific adapter remains a leaf.

## Portable query surface

`ILanguageIntelligenceService` is the provider-neutral read-only boundary for
diagnostics, hover, definitions, implementations, references, document symbols,
and workspace symbols. Every `LanguageQueryRequest` carries a stable
`LanguageQueryId`, an operation-specific validated shape, a positive retained
result count, a finite timeout, and the exact single-use observation grant.
Document positions and half-open ranges use zero-based UTF-16 coordinates;
`LanguageLocation` may carry the observed document fingerprint.

`AgentKit.Tools.Language` exposes those operations through the stable `language`
tool. Its JSON inputs and projected ranges are one-based. It validates all path,
position, query, count, and timeout fields before authorization, then requests
`FileRead`/`Observe` for the exact document or the workspace directory. The
workspace-symbol query enters authorization only through a cryptographic
fingerprint. The selected service revalidates and consumes the same grant before
observing language state.

Query results distinguish successful empty snapshots from `Unsupported`,
`Unavailable`, `Denied`, `Stale`, `TimedOut`, `Cancelled`, and `Failed`
outcomes. Arrays are bounded by the requested item count. The tool independently
bounds every projected array and provider-returned text field, marks any loss
with `projection_truncated`, and clears `complete`; an oversized or misbehaving
adapter therefore cannot create unbounded model context. The scripted leaf
provides identified deterministic scenarios for tests and replay and performs no
process or network effects.

## Descriptors and selection

Each service descriptor records:

- stable service and implementation identities, semantic version, executable or
  endpoint identity, and integrity/source provenance;
- supported languages, file patterns, root markers, initialization options,
  workspace-folder support, and protocol capabilities;
- process, network, file, memory, and concurrency requirements;
- whether requests are read-only, return proposed mutations, or may perform
  effects;
- startup, request, idle, restart, shutdown, and output bounds; and
- trust, installation, sandbox, and security profile requirements.

Selection is deterministic for a captured workspace/catalog snapshot. File-name
or model-name substring heuristics MAY help discover candidates, but they cannot
prove capability. Multiple matching services use an explicit priority or
composition policy rather than map overwrite.

## Installation and updates

Catalog discovery is observationally pure. A missing executable produces an
`Unavailable` capability or an explicit installation plan. Starting a language
service MUST NOT download an unpinned `latest`, execute package-manager scripts,
or mutate configuration as a side effect of lookup.

Installation is a protected, separately accepted operation that binds the exact
source, version, digest/signature when available, destination, package-manager
behavior, network egress, and process effects. Auto-update follows the same
path. Failed installation leaves a recoverable outcome and never publishes a
half-valid descriptor.

## Lifecycle

Service state distinguishes at least `Starting`, `Ready`, `Degraded`,
`Restarting`, `Failed`, `Stopping`, and `Stopped`. Initialization completes only
after protocol handshake and required workspace synchronization.

Failures are typed and observable. A service is not marked permanently broken
after one transient failure without applying its bounded restart policy; nor is
an exception swallowed while callers keep receiving empty results. Restart uses
injected time, a bounded budget, deterministic jitter in tests, and a circuit
state visible to callers.

Shutdown sends the protocol's graceful request when possible, waits a bounded
period, closes streams, terminates the process tree if required, and releases
workspace leases. Reload cannot leave the old process, watchers, or handlers
alive beside the replacement.

## Document synchronization

The adapter states whether it uses open-document versions, on-disk files, or a
staged virtual document. Every diagnostic, hover, definition, formatting edit,
or code action identifies the document version and workspace fence it observed.
Results for a stale version are rejected or explicitly labelled stale.

Workspace mutations publish one settled change batch. Watchers deduplicate
self-generated events by mutation identity without suppressing unrelated
concurrent writes. Rename carries old and new canonical paths. Overflow or lost
watch events produce an explicit gap and bounded rescan, not silent drift.

Debounce uses `TimeProvider`; event coalescing preserves create/delete/rename
semantics. Symlink traversal, ignored paths, maximum watch roots, and recursive
watch support are declared capabilities.

## Diagnostics and code actions

Diagnostics are immutable snapshots keyed by service, workspace, document
version, and publication sequence. Empty diagnostics mean the service
successfully reported none; timeout, crash, stale version, and unsupported file
are different outcomes.

Formatting and code actions return proposed `WorkspaceEdit`-style mutations.
They do not write files directly unless registered as effecting tools. Proposed
edits pass through the complete
[workspace mutation transaction](workspace-mutations-and-code-editing.md),
including final-byte authorization and conflict detection.

Partial protocol responses, progress, server log text, and dynamic registration
metadata are untrusted. They are bounded before entering context or telemetry
and never grant file/process authority.

## Acceptance scenarios

- Lookup with a missing server performs no download or configuration write.
- A service result for document version 7 cannot mutate version 8.
- Watch overflow emits a gap and reconstructs one coherent state snapshot.
- Rename notification preserves both canonical paths and one mutation identity.
- A formatter crash is distinguishable from a successful empty edit.
- Restart exhaustion remains observable and does not create an infinite loop.
- Graceful shutdown failure escalates and reports unreaped descendants.
- A language-server code action cannot bypass file authorization.

## Lifecycle safeguards

Language-server discovery and per-root client selection are pure catalog
operations. Discovery cannot download a server or write configuration; explicit
maintenance owns acquisition. Startup and protocol failures remain typed and
observable, broken-client state has a bounded recovery policy, and shutdown uses
graceful protocol termination before process escalation.

## Related specifications

- [Interactive terminals and process sessions](interactive-terminals-and-process-sessions.md)
- [Coding-harness resources and project trust](coding-harness-resources-and-project-trust.md)
- [Cancellation, timeouts, and resilience](../../concepts/cancellation-timeouts-and-resilience.md)
- [Observability and audit](../../concepts/observability-and-audit.md)
