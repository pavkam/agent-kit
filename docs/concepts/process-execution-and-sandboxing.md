# Process execution and sandboxing

**Status:** Normative host boundary

**Architecture:** [Process execution](../architecture/process-execution.md)

**Depends on:**
[Permissions, approvals, and trust](permissions-approvals-and-trust.md),
[streaming and event protocol](streaming-and-event-protocol.md),
[cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)

## Purpose

Starting a process is the broadest host effect an agent can request. This
specification defines the observable behavior every process implementation MUST
provide so that executables are resolved before authorization, sandboxes
constrain consequences without granting authority, and termination reports
side-effect certainty truthfully.

It specifies process creation and lifetime. Interactive terminal sessions,
cursors, and attachment are specified by
[interactive terminals and process sessions](../profiles/coding-harness/interactive-terminals-and-process-sessions.md);
language-server lifecycle is specified by
[language services, formatters, and watchers](../profiles/coding-harness/language-services-formatters-and-watchers.md).

## Boundary ownership

Framework components and tools MUST NOT call process APIs directly. They MUST
depend on narrow contracts owning executable resolution, arguments, standard
streams, working directory, environment projection, sandbox profile, resource
bounds, cancellation, termination, and exit reporting.

Process requests MUST use structured arguments. A shell command string MUST NOT
be interpreted unless the selected capability is explicitly a shell, and that
shell capability MUST be a distinct declared capability rather than a change in
how the ordinary executor interprets arguments.

An implementation MUST NOT weaken a sandbox, forward the ambient environment, or
fall back to an unsandboxed process.

## Canonical operation identity

Every start MUST be reduced to a canonical operation before authorization. That
identity includes the resolved executable, executable fingerprint where
available, arguments, working directory, projected environment names and safe
fingerprints, standard-input fingerprint, sandbox profile, resource limits, and
intended side-effect class.

Credentials and raw secret values MUST NOT enter the request, audit record, or
model-visible result. Environment projection is an allowlist; the ambient
environment MUST NOT be inherited implicitly.

## Authorization and sandboxing

Executable lookup, metadata reads, and hashing are protected observations with
their own bounded authorization. Lexical argument normalization alone performs
no protected read. Every start MUST pass through the shared security authority
after canonical resolution and before process creation. The implementation MUST
validate and consume a distinct bounded start grant immediately before starting.

A final path check or hash does not prevent a later executable swap. When the
policy requires an exact binary, the selected backend MUST bind execution to the
verified object through a supported handle or an enforceable isolation domain.
Scripts also bind the interpreter and any required resolution dependencies. If
the platform cannot preserve that identity through creation, it MUST return
unsupported rather than claim the check eliminated the race.

Changes to the executable, arguments, input, working directory, environment,
sandbox, principal, or limits MUST require a new security request.

A sandbox limits consequences but MUST NOT grant permission. The effective
sandbox profile MUST be derived from, or narrower than, the grant. If a required
sandbox, resource limit, working-directory boundary, environment isolation, or
audit sink cannot be applied, execution MUST fail closed rather than proceeding
with weaker enforcement.

Child-process creation MUST NOT be implicitly authorized. It is either denied by
the sandbox or represented as a separately constrained capability.

File and network effects available to the child MUST be intersected with their
own grants and the sandbox profile. Permission to start a process is never
blanket host access.

## Output and lifecycle

Process output MUST be streamed as bounded typed events with separate
standard-output and standard-error identity. The boundary MUST define encoding,
binary output, truncation, backpressure, cancellation, graceful termination,
forced kill, orphan prevention, and disposal.

When the model-facing retained output truncates, a coding-harness composition
SHOULD preserve the complete stdout or stderr stream as a bounded artifact and
return its portable reference beside the retained tail. Complete capture has an
independent hard ceiling. Exceeding that ceiling or failing artifact publication
MUST be reported truthfully and MUST NOT retry the process effect. Artifact
storage requires a fresh storage grant; a consumed process-execution grant does
not authorize persistence.

Each successful start MUST return an operation-owned handle. Disposing the
handle MUST terminate and reap a still-running owned child using the configured
graceful-then-forced policy. Completion MAY be awaited by several consumers and
MUST always return the same terminal result.

Exit codes, signals, and resource usage MUST remain typed facts. They MUST NOT
be flattened into exception text.

## Termination and certainty

A timeout outcome MUST distinguish a process that never started from one that
may have produced effects.

Cancellation before creation SHOULD prevent the start. After creation,
cancellation MUST trigger the explicit termination policy and report whether
effects may have occurred; abandoning the child is not a valid outcome.

Retries belong to the calling tool or integration pipeline and require declared
idempotency or proof that the previous process did not start. A backend retry
setting MUST NOT override the operation's safety policy.

## Concurrency and time

Global and keyed concurrency limits MUST reserve capacity before creation.
Process handles, environment content, and mutable sandbox state MUST NOT live in
shared immutable definitions or leak between run scopes.

Framework-owned deadlines, grace periods, and timestamps MUST use the injected
`TimeProvider`. Wall-clock sleeps MUST NOT be used for coordination.

## Determinism and testing

A deterministic implementation MUST be able to declare output, exit, timing,
cancellation, and failure behavior without starting a real process, so that unit
tests never depend on host executables.

Real-process coverage is explicit integration testing against harmless fixtures
in isolated temporary roots. The same conformance assertions MUST apply to both
implementations.

## Acceptance scenarios

- A denied start creates no process and emits an audit record.
- An executable that resolves outside the configured roots is rejected before
  creation.
- Arguments are passed through structurally, with no shell interpretation, quote
  rewriting, or glob expansion by the ordinary executor.
- An environment variable outside the allowlist is absent from the child.
- A required sandbox control the platform cannot enforce fails startup instead
  of running unsandboxed.
- A child attempting to spawn its own child is denied unless that capability was
  separately granted.
- Standard output and standard error retain separate identity and ordering
  evidence under interleaved writes.
- Output exceeding its bound applies the declared truncation or backpressure
  policy and reports it as typed evidence.
- A timeout before creation is distinguishable from a timeout after the process
  may have produced effects.
- Cancellation after creation escalates from graceful termination to forced kill
  and reports side-effect uncertainty.
- Disposing a handle terminates and reaps the child, leaving no orphan.
- A grant consumed by one start cannot authorize a second start.
- Secret values never appear in the canonical request, audit record, or result.

## Related specifications

- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [File-system access and bounds](file-system-access-and-bounds.md)
- [Network access and egress](network-access-and-egress.md)
- [Interactive terminals and process sessions](../profiles/coding-harness/interactive-terminals-and-process-sessions.md)
- [Language services, formatters, and watchers](../profiles/coding-harness/language-services-formatters-and-watchers.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
