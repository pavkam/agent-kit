# Interactive terminals and process sessions

**Status:** Normative coding-harness profile  
**Depends on:** [Coding harness execution](coding-harness-execution-profile.md),
[streaming](streaming-and-event-protocol.md),
[permissions](permissions-approvals-and-trust.md)

## Purpose

One-shot command execution and an interactive terminal are different contracts.
A terminal owns a long-lived process tree, pseudoterminal state, input
arbitration, resumable byte output, attachment policy, and explicit teardown.

Terminals are leaf channel/process integrations. They MUST NOT bypass input
admission, process authorization, workspace leases, event backpressure, or
artifact retention.

## Spawn intent

Before a process starts, one immutable intent records:

- `TerminalId`, operation identity, owner/tenant, workspace identity and fence;
- executable identity and argument vector, without lossy shell re-parsing;
- canonical working directory;
- environment allowlist, explicit values, removals, and secret references;
- PTY or pipe mode, terminal type, locale, encoding, columns, and rows;
- sandbox/process-tree profile, resource limits, deadlines, and idle policy;
- output retention and subscriber backpressure policy; and
- attach, input, resize, signal, and teardown permissions.

The security grant binds this complete intent. The process adapter revalidates
it immediately before spawning. Inheriting the host's full environment,
credential helpers, agent sockets, or ambient working directory is opt-in and
observable.

Prompt or command templates remain data. A template feature that expands
`!command`, backticks, shell substitutions, or command interpolation MUST route
the resulting process through this same intent, authorization, audit, and
settlement path before its output enters a prompt.

## User-invoked shell actions

A user shell action is a one-shot process operation with user provenance. It is
not a model tool call, even when both routes eventually select the same shell
adapter. Its immutable request includes an explicit context disposition: include
the bounded result in later model context, or retain it only in durable
session/audit state. A frontend prefix such as `!` or `!!` merely selects that
typed disposition; the prefix itself grants no process or workspace authority.

The result records the original and resolved command fingerprints, process
profile, workspace, stdout/stderr projection, exit/signal/cancellation,
truncation artifact, effect certainty, and context disposition. Context
exclusion is visible in the context manifest and never means “do not persist.”
If the action completes while an agent run owns the lane, live output may still
be published, but durable transcript insertion waits for the first safe boundary
that cannot split an assistant tool call from its result. Crash recovery retains
the pending result and its intended boundary.

A typed extension hook may decline, select another registered process executor,
or return a validated intercepted result with provenance. It cannot bypass
process authorization, fabricate success/effect certainty, omit required audit,
or turn arbitrary extension output into trusted model context.

## Durable creation and ownership

The harness records an accepted terminal before spawn and records the concrete
process identity immediately after spawn. Publication of `Created` occurs only
after durable ownership exists; otherwise a client can attach to an orphan the
runtime cannot later reap.

A terminal has one owning host lease and fence. Reconnection may transfer or
reacquire ownership according to policy, but two hosts cannot concurrently
control the same process. Attach uses a bounded, audience-specific ticket or an
authenticated principal check—never knowledge of a terminal ID alone.

## Output log and cursors

PTY output is bytes. The canonical cursor is a monotonic byte offset over the
captured stream, independent of UTF-8/UTF-16 presentation. Decoding occurs at
the client boundary with explicit handling for code points split across chunks.

Retention is bounded by bytes and time. A subscriber supplies its last observed
offset and receives either:

- every retained byte strictly after that offset; or
- an explicit `Gap` containing requested, earliest-available, and current
  offsets, followed by a resynchronization snapshot or artifact reference.

Silent loss, guessed character cursors, and unbounded per-subscriber queues are
forbidden. Slow consumers follow a declared block, drop-with-gap, disconnect, or
spill policy. Full output that exceeds live retention MAY move to
`AgentKit.Artifacts`; it MUST NOT be left in an ambient global temp directory
with unrelated retention and authority.

stdout and stderr remain distinct for pipe-based commands. A PTY intentionally
combines them and reports that loss of distinction. Exit events occur after all
captured late output has been ordered and published.

## Input, resize, and signals

Input writers are serialized. The policy states whether one controller holds an
exclusive lease or multiple authenticated writers interleave whole frames. Input
acknowledgements bind the accepted byte range; cancelling a caller does not
retract bytes already written to the PTY.

Resize operations carry sequence/version and apply in accepted order. Duplicate
or stale resizes are harmless. Signals name an allowed semantic action rather
than accepting arbitrary platform signal numbers by default.

## Termination and reaping

Terminal state distinguishes requested termination, process exit, output drain,
resource cleanup, and terminal settlement. Timeout or abort follows a declared
escalation policy such as graceful signal, bounded wait, force termination, and
descendant cleanup. The result records which stage succeeded.

The harness MUST define descendant ownership for shell wrappers, job-control
groups, daemonizing children, Windows job objects, and remote executors. Killing
one PID is not automatically process-tree settlement. An exited terminal remains
attachable only for a bounded retention window and is then reaped without
leaking handles, tasks, subscriptions, or workspace leases.

## Acceptance scenarios

- A non-BMP character split across PTY chunks resumes correctly from byte
  offsets.
- A subscriber older than retained output receives an explicit gap and
  resynchronizes.
- Slow SSE/PTY subscribers cannot grow an unbounded queue.
- Terminal IDs alone cannot authorize attach, input, resize, or termination.
- Spawn denial occurs before any process or temporary shell script exists.
- Host shutdown and user abort exercise the configured process-tree escalation
  and report unreaped descendants.
- Final output is delivered before the terminal exit event.
- Template command interpolation cannot execute before process authorization.
- A user shell result excluded from model context remains in durable session and
  audit state.
- A shell result completing during tool execution is committed only after the
  complete tool-call/result segment.
- An extension-intercepted user shell action still carries process authority,
  provenance, effect certainty, and bounded output.

## Process interoperability details

One-shot shell adapters declare details that belong in a process profile rather
than folklore:

- shell selection is explicit path first, then platform discovery; Unix may fall
  back to `sh`, while Windows separately discovers Git Bash or PowerShell;
- PowerShell invocation is an exact executable/argument profile, including
  profile, interactivity, and execution-policy choices;
- timeout is absent or a finite positive duration within the host timer range;
- abort/timeout process-tree escalation is platform-specific and records whether
  descendants were actually killed;
- after the direct child exits, pipe draining waits for EOF or a bounded idle
  window that resets on late data, preventing inherited pipes from hanging
  forever; and
- harness metadata environment variables are removed from ambient inheritance
  and re-added from captured run state. They are diagnostics, never credentials
  or authority.

Interactive shell input MAY use `!command` to include the recorded result in
later model context and `!!command` to exclude it while retaining the session
entry. A typed extension event may provide a result or alternate operations.
When execution finishes during an active model run, the process message waits
for a named safe boundary so it cannot break tool-call/result ordering. AgentKit
represents disposition, interception, and commit boundary as typed state and
applies the authorization and recovery rules above.

Graceful-then-force escalation is configurable and uses `TimeProvider`; Windows
helpers resolve from trusted system locations; process ownership is not a
mutable global PID set; and merged stdout/stderr arrival order is labelled
nondeterministic. No command has an accidentally infinite timeout merely because
its caller omitted one without selecting an unlimited profile.

## PTY and template safeguards

Create, connect, write, resize, and kill are distinct PTY operations. In-memory
character retention and unbounded subscriber handling are insufficient; the
profile requires byte cursors, durable ownership, bounded fan-out, and explicit
gaps. Shell interpolation during prompt expansion remains inside the protected
process-tool boundary.

## Related specifications

- [Coding workspaces and worktrees](coding-workspaces-and-worktrees.md)
- [Coding-harness export, sharing, and control plane](coding-harness-export-sharing-and-control-plane.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Artifact and content storage](artifact-and-content-storage.md)
