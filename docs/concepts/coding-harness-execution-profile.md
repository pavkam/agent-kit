# Coding harness execution profile

**Status:** Normative integration profile  
**Depends on:** [Agent loop](agent-loop-state-machine.md),
[sessions](sessions-persistence-and-branching.md),
[input admission](input-admission-and-message-queues.md),
[provider requests](provider-request-pipeline.md),
[tools](tool-call-lifecycle.md), and
[durable execution](durable-execution-and-recovery.md)

## Purpose

A coding harness is more than an agent loop with four file tools attached. It is
the host-level composition that accepts work, exposes live progress, survives
interruption, coordinates conversation branches, executes protected effects,
loads workspace resources, and presents the same semantics through interactive,
batch, and machine-readable front ends.

This document is a conformance profile over existing AgentKit extension points.
It does **not** create an `AgentKit.Harness` package or a universal harness
interface. Each behavior remains owned by its narrow package; the `AgentEngine`
composition validates that the selected pieces close this profile.

## Ownership and dependency direction

| Concern                                         | Policy owner                                               | Harness responsibility                                                                           |
| ----------------------------------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Run and turn transitions                        | `AgentKit.Loop`                                            | Select one loop and expose its named boundaries                                                  |
| Session tree, branches, lane state, and commits | `AgentKit.Session`                                         | Select one session profile and mutation coordinator                                              |
| Input admission and live/final publication      | `AgentKit.IO`                                              | Route every channel through admission and publication                                            |
| Context and compaction                          | `AgentKit.Context` and `AgentKit.Context.Compaction`       | Select profiles; never mutate context ad hoc in the host                                         |
| Model selection and execution                   | `AgentKit.Providers` plus a concrete provider leaf         | Resolve capabilities and credentials at each request boundary                                    |
| Tool lifecycle                                  | `AgentKit.Tools`                                           | Select catalog, scheduler, invoker, and normalizer                                               |
| Workspace lifecycle                             | Host-selected `IWorkspaceCoordinator`                      | Own one selected registry, lease, fence, readiness, reset, removal, and disposal policy          |
| Workspace snapshots                             | Host-selected `IWorkspaceSnapshotCoordinator` when enabled | Coordinate coverage and restore without absorbing artifact, file, session, or security ownership |
| File, network, and process effects              | focused host packages                                      | Provide protected, capability-declared adapters                                                  |
| Authority and approval                          | `AgentKit.Permissions`                                     | Supply the selected security authority; never infer trust from installation                      |
| Durable recovery                                | `AgentKit.Durability` when selected                        | Schedule wakes and host ownership around its evidence                                            |
| Hooks and observation                           | `AgentKit.Hooks` and `AgentKit.Observability`              | Expose typed points without making them state authorities                                        |

Channel adapters, terminals, IDEs, HTTP servers, RPC servers, and worker hosts
are leaves. They may translate protocol values but MUST NOT bypass admission,
authorization, session coordination, the loop, or settlement.

## Execution topology

The harness profile distinguishes these identities:

- `SessionId` identifies the canonical durable conversation and application
  state boundary.
- `BranchId` identifies one named path through the immutable conversation tree.
- `ExecutionLaneId` identifies independently runnable agent state anchored to a
  branch. It includes total model, thinking, tool, queue, and policy selection.
- `OperationId` identifies one durably accepted unit of work on a lane. Run,
  compaction, navigation, and maintenance operations are distinct kinds.
- `RunId` and `TurnId` identify conversational execution inside a run operation.
- `DriveInvocationId` identifies one caller's process-local observation of an
  operation. It is never a substitute for durable operation identity.
- `ToolInvocationId` identifies one framework tool execution and scopes its
  checkpoints and memos independently from the provider's `ToolCallId`.

All are dedicated immutable value types. String names used by a UI are labels,
not identities.

A branch is data. It owns a tip and branch-relative queries. It has no model,
queue, operation, or effect policy. An execution lane adds those behaviors. A
session MAY contain zero or more branches and lanes; `main` is not magically
created by a storage abstraction.

At most one operation is open on a lane. Different lanes in one session MAY run
provider and tool effects concurrently. Their durable decisions still serialize
through the session mutation coordinator, so readers see whole commits and
branches cannot race by mutating a shared tip accidentally.

Hosts that need the simpler legacy policy MAY configure one lane per session.
They MUST express that as a profile constraint, not redefine session identity as
run identity.

## Durable state categories

A conforming session exposes these logical categories even when one backend
physically stores them together:

1. **Immutable entries** form the conversation tree. A committed entry includes
   placement and payload; neither is later updated in place.
2. **Current state cells and ordered lists** hold replaceable orchestration or
   application state such as branch tips, lane configuration, inboxes, current
   operation state, bounded partial frames, and staged results.
3. **Usage ledger rows** append provider, tool, or reconciliation usage without
   becoming recovery authority.

Indexes, search stores, statistics, caches, and UI projections are rebuildable.
They MUST NOT decide whether an effect ran or an operation completed.

The public store contract need not expose generic value maps. AgentKit MAY use
typed records and narrow repositories instead. It MUST nevertheless state, for
every durable value, whether it is immutable history, current mutable state, an
append-only ledger, or a derived projection. “It is somewhere in the session
JSON” is not a storage contract, babe.

## Acceptance is not execution

The harness MUST expose semantics equivalent to five primitives:

```text
Accept          durably installs one operation and returns its identity
Drive           advances an expected operation until a typed wait or terminal
RequestAbort    durably requests cancellation of an expected operation
Inspect         atomically reports the current and latest terminal execution
Attach          reconstructs projections and lists open operations without driving
```

Convenience methods such as `PromptAsync`, `ResumeAsync`, `CompactAsync`, or
`AbortAsync` MAY compose these primitives with process-local waiting. They MUST
produce the same durable writes, events, results, and recovery behavior as the
explicit composition.

Acceptance MUST:

- validate and authorize immutable input before its first commit;
- use an idempotency identity and expected lane version;
- in one transaction verify lane ownership, capture eligible queued input,
  promote selected pending payloads to immutable entries, append the initiating
  input, delete the promoted payloads, advance the branch tip, write immutable
  operation metadata plus complete initial state, and install lane ownership;
- start no provider, tool, hook, timer, or background task before that commit;
- return `LaneBusy` when another operation already owns the lane; and
- remain recoverable when the accepting process dies before any driver starts.

Acceptance performs no provider lookup. Attachment is likewise observational: it
validates and reconstructs state, installs process-local coordination, and
returns the open operation inventory—lane, operation identity, kind, start time,
and aborting state—without starting providers, tools, hooks, retries, deferred
polls, or timers. Resumption is an explicit `Drive`. Attaching does not invent a
`main` lane.

Every `Drive` and `RequestAbort` names the expected `OperationId`. A stale wake
for operation A MUST NOT advance or cancel later operation B. Matching
concurrent drivers join the one installed pass or receive an explicit
already-driven outcome; they do not create two top-level state writers.

## Total operation state

Immutable operation metadata records identity, kind, accepted input, source tip,
configuration/profile references, and start time. After every transition the
runtime commits one complete current state for the operation. Recovery reads
that state and dispatches to the responsible procedure; it MUST NOT infer the
phase from a collection of missing records or replay a write journal to discover
what probably happened.

The state union is a total dispatcher algebra: every persisted leaf has one
named recovery procedure, with cancellation represented orthogonally where it
can apply. A drive step either commits a different total state, returns a typed
wait/terminal outcome, or faults the harness. Returning `continue` without
durable progress is an invariant violation, not permission to hot-loop.

State may reference bounded sibling payloads by typed identity when embedding
them would be excessive. Those references remain operation-owned and are deleted
atomically on terminal settlement. A missing required reference is corruption;
an explicitly optional progress checkpoint may be absent.

The state graph MUST distinguish at least:

```text
accepted -> preparing -> ready -> effect_pending
effect_pending -> outcome_ready -> committed -> next_boundary
next_boundary -> preparing | waiting | terminal
any nonterminal -> cancel_requested -> reconciling -> terminal
```

Provider and structural operations may collapse `outcome_ready` and `committed`
when complete output is born in its final tree position. Parallel tools need the
separation because effects settle in completion order while results commit to
conversation history in assistant source order.

One terminal transaction removes operation-owned current state, clears the lane,
and writes one immutable operation-result record. Recovery never uses an old
terminal record as the current restart point.

## Intent, effect, and settlement

Every effect whose outcome can become uncertain follows this sequence:

1. prepare and validate the exact provider request, tool arguments, destination,
   replay classification, and bounded identifiers;
2. commit an intent that reserves response, usage, tool-result, and idempotency
   identities as applicable;
3. cross an operation-owned effect-start gate;
4. invoke the provider or tool with the gate's cancellation signal and bounded
   security grant;
5. durably stage or commit the complete outcome; and
6. advance state in the same transaction that makes the outcome authoritative.

The effect-start gate is not a read-then-act check of durable status. It
synchronously arbitrates process-local admission after preparation and
immediately before an integration starts. When abort begins, the gate moves to
`aborting` before the durable mutation and refuses new admission with a barrier
that resolves only after the cancel marker commits. Abort-before-admission
invokes nothing; admission-before-abort lets that complete invocation reconcile
truthfully. Hook pipelines, credential refresh, lazy adapter loading, provider
setup, tool invocation, and retry timers all need a named gate boundary.

Hooks are not automatically durable effects. A hook result is consumed in one
commit; a crash before that commit MAY rerun the hook. A hook that performs an
external effect therefore needs its own idempotency identity and must document
the replay contract.

## Assistant generation and partial durability

The runtime resolves the selected model descriptor, provider registration,
credential, and compatible tool snapshot for every request. Durable state stores
stable identities and configuration revisions, not live SDK objects or bearer
tokens. A removed model or provider fails in band before request intent; it does
not corrupt the lane or silently select another model.

Before network I/O, intent reserves the assistant entry and usage identities.
During streaming, the harness MAY persist compact replayable frames for
reconnection and truthful partial recovery. Such frames:

- carry response, part, and content-index correlation;
- form a validated subsequence of provider-event order;
- never include a terminal `done` or `error` as proof of settlement;
- never enter the canonical transcript as a completed assistant message;
- are bounded and appended without awaiting storage on every provider delta;
- drain through an ordered write chain before terminal settlement; and
- are deleted in the transaction that commits or synthesizes the final response.

The live accumulator is not durable truth. If the process dies during
`effect_pending`, committed frames show only a prefix; they do not prove that
the provider stopped, was billed once, or produced no later output. Unless the
provider exposes a verified resumable operation, recovery MUST NOT pretend to
reattach. It settles a typed interrupted/unknown outcome or follows an explicit
safe retry policy, retaining the original attempt's usage evidence when known.

The complete assistant message and the corresponding provider-attempt usage row
MUST commit atomically. A partial, errored, cancelled, or deferred response is
never written as a successful settled assistant entry.

## Tool effects, progress, and ordering

Every accepted tool call is recorded before invocation with its original call
identity, stable content-part identity or position in the assistant message's
complete content array, scheduling ordinal, selected descriptor/version,
canonical validated arguments, security request fingerprint, replay
classification, and reserved result identity. A source position counts text and
reasoning parts; it is not the ordinal in a filtered list of tool calls.

The reserved result identity and `ToolInvocationId` MAY share a deterministic
derivation, but both remain typed separately from the provider call ID and from
a driver's process-local invocation identity.

Tool child states distinguish `planned`, `effect_pending`, `outcome_ready`, and
`completed`:

- `planned` has performed no effect;
- `effect_pending` may have performed an effect and cannot be inferred safe;
- `outcome_ready` has one complete staged terminal result and never executes
  again; and
- `completed` has one immutable result entry in source order.

Parallel tools may reach `outcome_ready` in completion order. Materialization
advances only the completed source-order prefix. Staging the full result before
placement is what prevents a completed later effect from replaying after a
crash.

A tool progress checkpoint is an optional bounded **complete snapshot** for
presentation or synthetic interruption output. It is not effect settlement.
Checkpoint cadence, maximum bytes, replacement behavior, and duplicate
suppression MUST be explicit; otherwise an append-log backend turns a chatty
process into a storage leak with excellent enthusiasm.

Public progress distinguishes the latest live update from the latest durable
checkpoint. Assistant progress is an ordered committed frame prefix; tool
progress is a replacement snapshot. Updates after seal or ownership loss are
ignored, and queued progress persistence/event delivery drains before after-tool
hooks and terminal cleanup.

Tools MAY persist invocation memos for idempotent substeps. A memo is scoped to
the exact tool invocation, written before its protected subeffect, awaited by
the tool, and deleted with terminal staging. It cannot become a global cache or
an undeclared application-state store.

Recovery MAY replay only a tool declared and authorized as safe for the same
canonical arguments. A non-replayable `effect_pending` call becomes an explicit
interrupted result, optionally containing the latest durable checkpoint. The
runtime terminally settles every accepted sibling call. A model response stopped
for output length MUST NOT execute any contained tool call because its arguments
may be truncated.

Live tool-end events may follow completion order. Durable result entries and the
next provider request MUST preserve assistant source order. The two orders are
different contracts and MUST be tested independently.

## Queue payloads and deferred tree writes

Queued input is durable content, but it is not yet conversation history. Before
promotion, one operation-owned or lane-owned pending payload represents it.
Promotion atomically creates the immutable entry, deletes the pending payload,
moves the branch tip, and removes the queue item. Cancellation deletes the
pending payload without claiming it entered the model context.

Exactly one representation is authoritative at every commit boundary:

```text
pending payload -> immutable entry
reserved result id -> staged terminal result -> immutable result entry
```

The harness keeps four delivery classes distinct:

- `Steer` enters at the next safe boundary of the current run;
- `FollowUp` enters only when that run would otherwise finish;
- `NextRun` is reserved for the next accepted run operation; and
- `Write` is non-triggering deferred tree content.

At acceptance, the transaction captures eligible `NextRun` and `Write` items;
steer/follow-up capture follows the selected drain policy. A lone write cannot
make an otherwise empty run valid. Durable abort removes and reports only
current-run steer/follow-up items; next-run work, deferred writes, and input
admitted after the abort marker survive reconciliation.

An operation-aware append received while a lane owns an operation becomes a
`Write` or is rejected by an explicitly selected policy; it cannot bypass the
lane. When a normal append later runs against an idle lane, the coding-harness
profile atomically drains older `Write` items in FIFO order before the new
append. An alternative profile MUST name an equally deterministic drain boundary
and prove that deferred writes neither reorder nor starve.

Steering and follow-up queues retain bounded drain modes. Configuration may
choose one-at-a-time or bounded-all delivery, but preparation work that takes
time MUST not accidentally poll a one-at-a-time queue twice and inject two
messages into one turn.

Application entries requested during an operation MUST declare whether they are
triggering input, non-triggering annotations, or deferred writes. A raw branch
append racing a lane-owned operation is either serialized through an explicit
operation-aware policy or rejected as a programming conflict; it cannot insert
before a provider cache tail invisibly.

## Context, compaction, and navigation

Provider context is a derived branch view. Within an uncompacted lane, new
conversation content SHOULD append at the tail so provider prompt-cache affinity
is not destroyed by hidden insertion.

A compaction checkpoint MUST be self-contained: summary, structured state,
source manifest, and a complete retained tail. Normal context reconstruction
reads from the newest applicable checkpoint forward and does not need to scan
covered history. Covered entries remain separately queryable, branchable, and
auditable.

Context preparation excludes assistant attempts whose terminal state is error,
aborted, deferred, or unknown. It preserves genuine output-length responses when
their content is valid and terminal. Custom entries enter provider context only
through an explicit projector with trust and provenance.

Manual compaction and branch navigation are structural operations with their own
`OperationId`, usage, cancellation, and terminal result. Expensive summary
generation occurs outside the mutation line; activation rechecks the source tip
and version. Navigating away from unsummarized work may create a branch summary,
but moving a branch never claims to reverse external effects.

## Cancellation, abort, close, and fault

The profile separates four signals:

- invocation cancellation stops one caller waiting for or streaming an
  operation;
- durable abort requests cancellation of the expected operation;
- host shutdown asks the runtime to stop admitting effects and preserve a
  recoverable checkpoint; and
- deadline or budget exhaustion produces its own typed operation outcome.

Cancelling an RPC request, terminal subscription, or `IAsyncEnumerable`
enumeration MUST NOT silently write durable abort. Durable abort uses a precise
linearization: synchronously move the local effect gate from `open` to
`aborting`; commit `cancel_requested` and current-run queue pruning; resolve the
gate's cancellation barrier; only then signal effects that were already
admitted; and finally reconcile from the current total state. This prevents both
a late new effect and an in-flight effect observing cancellation before its
durable cause exists. A real tool outcome that wins the race remains real; it is
not overwritten by a fabricated cancellation result.

Close is a controlled process loss, not a normal terminal transition. It seals
new admission, prevents new effects, drains already admitted local commits as
documented, releases resources, and may leave `effect_pending` state for later
recovery. It MUST NOT write a false success or assume that killing a child
process reversed its effects.

A lane is idle only when it has neither a durable open operation nor a
process-local drive. An installed operation with no driver is recoverable work,
not idle. Any `RunWhenIdle`-style maintenance primitive owns the serialized idle
window through its callback so an admission cannot race between the idle check
and the protected action.

After an admitted durable commit fails, the mutation projection is no longer
known to match storage. The harness faults, stops all new effects, rejects new
operations, and requires reattachment/recovery. Continuing optimistically is a
state-corruption feature, not resilience.

## Snapshots, watches, and live events

A reconnecting client needs a state snapshot plus later live events with no gap
or duplicate. The session coordinator MUST establish that boundary atomically:

1. register a bounded event buffer while holding the mutation line;
2. capture or reconstruct the durable snapshot at a known sequence;
3. publish the snapshot;
4. drain buffered events strictly after that sequence; and
5. continue live delivery under the normal backpressure policy.

Registration precedes snapshot capture, and delivery remains buffered until the
consumer explicitly starts. Resnapshot increments an epoch, drops delivery at or
before the new boundary, holds later events while the replacement snapshot is
captured, then resumes in order. Recipient sets are fixed at publication time;
payloads are immutable or cloned per recipient. Subscriber failures are isolated
and reported without recursively publishing another failure through the same
broken subscriber.

Once a resnapshot epoch has discarded pre-boundary delivery, capture failure
cannot fall back to the old snapshot and release only the held later events. The
watch either retries atomically while retaining a complete gap-closing buffer or
terminates with a typed resnapshot failure. Silent continuation would create a
replica that looks current while missing committed state.

Snapshots distinguish the immutable transcript, queued inputs, the current
operation, streaming assistant partial, running tool partials, latest terminal
result, totals, and fault state. A live partial may be newer than its durable
checkpoint, but it MUST be labelled provisional and never spliced into the
transcript.

Every event-producing commit publishes the new owned projection and binds its
complete event batch in the continuation that observes the commit. Delivery may
be asynchronous; ordering and recipient selection cannot be rediscovered later
from mutable global state. Events carry the invocation/telemetry context of the
emitting operation without retaining that context on shared receivers or
serializing it into the session.

## Forking and backend semantics

A fork reads one coherent source snapshot. Branch-scoped and whole-tree forks
have different, explicit policies:

- a branch fork copies only the selected ancestry and reconstructs one idle lane
  at the selected tip;
- a tree fork copies every selected immutable entry and branch tip, then
  reconstructs each configured lane as idle;
- open operations, queued pending payloads, progress frames, staged results,
  terminal-operation caches, and leases do not migrate as live work;
- usage either starts at zero or is copied as an explicitly named billing
  policy; it is never accidentally inherited;
- application values declare tree, branch, recompute, or exclude behavior; and
- copied sequence identities and the destination high-water mark MUST prevent
  later sequence reuse when source IDs are retained.

A fork from before a root entry may legitimately produce an empty branch. A
selected entry must lie on the named branch ancestry; finding the same entry
elsewhere in the session is insufficient.

All storage adapters run one conformance suite for atomic writes, ordering,
idempotency, optimistic conflicts, bounded paging, whole-list cleanup, fork
snapshots, close, and corrupt/torn data. Backend-specific obligations remain
observable:

- an append-log backend discards a torn final transaction as a unit but treats a
  malformed interior record as corruption; it states whether a resolved commit
  includes `fsync` durability and how superseded current state is compacted;
- a relational backend uses a transaction mode that cannot read a stale snapshot
  and then fail an unavoidable write-lock upgrade; coherent read-only snapshots
  remain read-only; and
- query plans for ancestry scans and ordered pagination are regression-tested,
  not assumed from the presence of an index.

Storage compaction preserves surviving sequence identities and the next-sequence
high-water mark. It is physical reclamation, not conversation compaction or
semantic deletion.

## Host modes and protocol cleanliness

A production coding harness SHOULD expose one semantic session surface through
several replaceable channel adapters:

- interactive TUI/IDE mode;
- one-shot or print mode;
- newline-delimited typed event mode; and
- request/response RPC mode with asynchronous event fan-out.

All modes share admission, execution, events, and settlement. Batch modes MUST
not initialize interactive UI dependencies. Machine-readable modes reserve
stdout for protocol frames; diagnostics, progress, and logs use a separate
channel. Framing specifies encoding, maximum record size, unknown command/error
shape, request IDs, event sequence, and process-exit semantics.

Each adapter declares how mode is selected, including TTY/redirection behavior.
Redirecting stdout or piping stdin MUST NOT silently change from interactive to
multi-prompt execution without a documented/observable mode result. Initial
stdin, command-line text, and file attachments remain typed segments with
source, encoding, boundaries, and trust; they are not trimmed and concatenated
with an empty separator. Multiple initial inputs explicitly mean one composite
turn or several sequential operations.

File attachment syntax is parsed only at channels that declare it. Expansion
produces a bounded typed file part with canonical path provenance, BOM/encoding
diagnostics, and authorization; synthetic XML/Markdown tags are never trusted
delimiters. Machine modes reject ambiguous interactive syntax rather than
quietly giving it another meaning.

The process contract maps configuration/startup failure, invalid invocation, run
failure, cancellation, termination signal, terminal-restoration failure, and
clean completion to one documented exit taxonomy. Final semantic output,
diagnostics, protocol frames, and resume hints have distinct destinations.

RPC cancellation is request-correlated. Disconnect cancels the reconstructed
invocation context according to adapter policy; it does not automatically abort
durable work. Prompts received during compaction, retry delay, or tool execution
still pass through the same input-admission rules.

RPC is a multiplexed protocol, not a blocking function call. Commands,
preflight/acceptance acknowledgements, operation terminals, live events, and
extension UI requests use distinct frame types and correlation domains. A prompt
acknowledgement means only that preflight/admission succeeded; terminal truth
arrives later. The protocol defines command concurrency, expected-state tokens,
duplicate command behavior, version negotiation, maximum line/frame bytes,
LF/CRLF and unterminated-final-record handling, event sequencing, and
unknown/late response policy.

Extension dialogs form a separate request family with uniform timeout, abort,
disconnect, and late-response semantics; fire-and-forget UI notifications do not
pretend to have a response. Stdin EOF, client disconnect, and process signals
state whether accepted work aborts, detaches, or remains durable. Shutdown must
flush or durably preserve a terminal result rather than intentionally losing it
because stdout is inconvenient.

## Interactive lifecycle and key actions

Interactive controls name semantic actions, not hard-coded byte sequences. The
keymap defines platform and terminal variants, focus/mode scope, chord timeout,
collision detection, and reserved sequences. Typical actions—abort current work,
clear editor, exit, queue follow-up, retract queued input, open tree, suspend,
resume, and invoke an external editor—have separate state guards.

Aborting restores only queue items named by the durable abort result. Repeated
Escape, Ctrl+C, or EOF behavior is an explicit state machine; an empty editor is
not inferred from screen pixels. Suspending a TUI releases/restores terminal
state, handles continuation signals, and prevents background repaint. Quit and
signal shutdown order extension cleanup, terminal restoration, transcript/resume
output, and process-tree cleanup without letting one failed cleanup hide the
original terminal outcome.

External editors and shells are structured executable-plus-argument vectors;
whitespace splitting a command string is not portable parsing. Fullscreen exit
policy explicitly chooses transcript, resume hint, or silence without mixing it
into machine-readable output.

## Diagnostics, telemetry, and offline operation

Update checks, installation telemetry, and product analytics are separate
features with separate defaults, consent, retention, stable-pseudonym creation,
reset/delete behavior, and egress destinations. Default telemetry excludes
prompts, paths, payloads, tool arguments/results, session IDs, credentials, and
reasoning. Diagnostics declare fatality and redaction; extension/resource
startup failures cannot leak sensitive expanded configuration.

Offline mode is a network-egress policy applied to providers, catalog refresh,
version checks, remote resources, telemetry, package resolution, and network
tools—not merely a flag that skips one update request. Attempts denied by
offline policy are typed and observable. Machine-readable stdout remains pure
whether diagnostics are fatal or recoverable.

## Workspace resources, settings, and trust

The harness host discovers instructions, skills, prompt templates, themes,
extensions, packages, and model configuration as typed resource kinds. For each
kind it defines:

- global, user, package, project, and runtime locations;
- traversal direction and stop conditions;
- deterministic precedence and collision identity;
- additive, replace, disable, and reset semantics;
- file-size, count, nesting, and symlink bounds;
- parse diagnostics and last-known-good behavior;
- reload boundary and watcher debounce; and
- the trust required before content becomes executable or authoritative.

Instruction files are data sources with explicit ancestry and precedence. Skills
and prompt templates have validated names, front matter, descriptions, argument
substitution, collision rules, and source provenance. Resource text does not
become a permission grant.

Project trust is a durable host decision scoped to the canonical workspace
identity. Before trust, project configuration cannot load executable extensions,
run package lifecycle scripts, change credentials, widen tool/network scope, or
silently override managed policy. Symlink aliases and path spelling do not mint
new trust decisions.

Settings writes use atomic replacement and preserve unrelated concurrent
changes. Invalid reload keeps the last known-good immutable snapshot and emits a
diagnostic. A current run observes a reload only at named boundaries; changing
active tools, model, or thinking mid-stream affects a later request.

## Tool-facing host details

Coding tools are host integrations, not clever prompt snippets. Their contracts
include the unglamorous details that keep them correct:

- canonical workspace-relative and absolute path handling, root containment,
  symlink policy, Unicode normalization, and platform case sensitivity;
- deterministic text decoding, BOM handling, binary detection, line-ending
  preservation, and bounded line/byte reads;
- edit preconditions, match cardinality, conflict detection, atomic replacement,
  and serialization of concurrent mutations to the same file;
- command, argument, working-directory, environment, shell, PTY, and platform
  selection as distinct values;
- concurrent stdout/stderr capture without deadlock, bounded live updates,
  tail/head truncation markers, full-output artifact spill where authorized, and
  late output after process exit;
- timeout, abort, signal escalation, descendant-process handling, and truthful
  exit metadata; and
- image/media inspection, orientation, resizing, MIME validation, and provider
  capability downgrade.

The same canonical effect description used for authorization reaches the
low-level file or process adapter. Extension-provided remote execution may
replace that adapter only through an explicit capability; it cannot smuggle a
different destination behind the built-in tool name.

## Extensions and lifecycle

Extensions register narrow contributions—tools, commands, resource sources,
renderers, model providers, hooks, or UI components. Registration is additive
and collision behavior is deterministic. An extension is not implicitly trusted
because discovery succeeded.

Async extension factories receive a bounded startup context and cancellation.
Partial factory failure removes every contribution from that factory. Shutdown
and process signals invoke registered cleanup exactly once within a bounded
deadline. Reload either replaces a contribution without changing its public
shape or performs an explicit host/process replacement; stale handlers, model
registrations, event subscriptions, or background jobs cannot survive invisibly.

Session replacement—new, switch, fork, clone, or tree navigation—has before and
after boundaries. Extensions MUST release session-scoped subscriptions and read
the replacement context after the switch; retaining old mutable session handles
is invalid. Calls that require idle state say so and race-check again at commit.

Hooks may block or transform only the fields permitted at that boundary. They
cannot grant authority, forge a tool success, mutate stable identity, or bypass
the result and settlement contracts.

## Security requirements

Running with the permissions of the host process is not an authorization model.
Project trust, tool allowlists, containers, and OS sandboxing are useful inputs
or consequence limits, but none is authorization.

Every file, process, network, provider-egress, credential, session, memory, MCP,
artifact, and extension-loading effect crosses `AgentKit.Permissions`; the
effecting leaf validates the bounded grant again. A coding-harness profile fails
composition when a selected protected capability lacks an authority, enforcement
point, or required audit path.

## Conformance and race catalogue

The profile requires deterministic tests for at least these two-order races:

- two accepts on one lane;
- accept versus process loss before drive;
- duplicate drive and stale drive versus a successor operation;
- durable abort versus provider settlement;
- abort versus tool staging and source-order materialization;
- progress/frame commit versus terminal settlement;
- later parallel tool completion versus an earlier tool;
- queued-input cancellation versus promotion;
- model/tool configuration change versus request snapshot;
- compaction preparation versus a new branch append;
- watcher registration versus state publication;
- close versus effect admission and settlement;
- credential refresh or model-catalog refresh versus provider replacement; and
- concurrent invocations on one shared receiver with independent cancellation
  and telemetry parentage.

Additional suites cover every durable state leaf after reopen, exact
intent/effect/settlement write order, convenience/primitive equivalence, partial
stream reduction across arbitrary fragmentation, backend conformance, fork
scope, stdout protocol purity, extension teardown, resource precedence, path
edge cases, and denial before each protected effect.

## Implementation-sizing values

These values are useful sizing evidence for validated profiles, not universal
AgentKit defaults:

- common tool output is bounded at 2,000 lines or 50 KiB; reads keep the head,
  shell keeps the tail, and grep clips an individual match line to 500
  characters;
- live shell progress is coalesced around 100 ms, while changed durable tool
  checkpoints are written at most about every two seconds under the same 2,000
  line/50 KiB bound;
- session discovery scans at most 1 MiB for a recognizable header before an
  authoritative open;
- production compaction triggers strictly above `contextWindow - reserve`, with
  observed defaults of 16,384 reserve tokens and roughly 20,000 retained tokens;
  fallback estimation uses about four characters per token and counts an image
  as 4,800 characters;
- history-summary output is bounded to the smaller of roughly 80% of reserve or
  model output capacity, while an oversized-turn prefix summary uses roughly 50%
  of reserve;
- ordinary transient retry defaults to three retries at approximately 2, 4, and
  8 seconds; overflow compaction/retry has a separate one-retry path; and
- after the direct child exits, pipe capture waits for EOF or about 100 ms of
  output idleness, resetting the window when late descendant output arrives.

Every AgentKit value above lives in a named validated profile, uses
`TimeProvider`, and has storage/context/process budgets. Reproducing a number
without reproducing its bound semantics would be cargo cult with punctuation.

The profile requires immutable agent definitions, DI-owned replacement, typed
security grants, injected time/randomness/identities, provider-neutral
contracts, and explicit distributed leases and fencing when work crosses
processes. Unimplemented, contradictory, or single-process behavior is a
caution, never a proven distributed contract.

## Acceptance criteria

- A host can use the same admitted operation through interactive, batch, event,
  and RPC adapters without semantic forks.
- Two lanes can overlap effects while one session coordinator serializes their
  durable decisions.
- Killing the host between any two durable transitions either exposes the prior
  state or the next state, never a guessed hybrid.
- Reconnecting yields one coherent snapshot and all later events exactly once
  according to the subscription delivery contract.
- Unknown provider and tool effects remain explicit; no recovery path reruns
  them merely because output is missing.
- Resource loading, extension execution, and every host effect preserve trust,
  authorization, provenance, and teardown rules.
- Every selected provider profile supplies the behaviors required by
  [coding-harness provider profiles](../providers/coding-harness-provider-profiles.md).

## Related specifications

- [Agent definition and run context](agent-definition-and-run-context.md)
- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Streaming and event protocol](streaming-and-event-protocol.md)
- [Configuration and overrides](configuration-and-overrides.md)
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
- [Testing and evaluation](testing-and-evaluation.md)
