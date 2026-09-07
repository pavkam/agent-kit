# Coding-harness frontends and protocol adapters

**Status:** Normative application profile

**Scope:** Optional application composition; not a required AgentKit capability.

**Depends on:**
[Input and output](../../concepts/input-admission-and-message-queues.md),
[streaming](../../concepts/streaming-and-event-protocol.md),
[coding-harness control plane](coding-harness-export-sharing-and-control-plane.md)

## Purpose

TUIs, IDE extensions, batch output, RPC, and agent-client protocols are views
and channel adapters over one harness. They may negotiate capabilities and
translate wire values; they do not own a second session model, loop, permission
engine, or definition of “idle.”

## Initialization and capabilities

Initialization authenticates the peer and captures protocol version, client
capabilities, content/media support, terminal/auth support, filesystem/location
features, and extension data bounds. Server-advertised capabilities come from
the effective AgentKit composition; an adapter cannot claim a feature it only
partially projects.

Extension UI support is negotiated per operation, not as one `HasUi` flag. The
snapshot distinguishes notifications, dialogs, status and working indicators,
widgets by content kind, editor read/write, custom components, autocomplete,
theme enumeration/switching, raw terminal input, and tool-output expansion. A
frontend that cannot implement one operation returns a typed unsupported result;
it MUST NOT silently accept a setter, discard a component factory, or fabricate
an empty/default getter value.

Unsupported methods and content variants fail with typed protocol errors before
state changes. Optional unknown fields are preserved as bounded extension data
when safe. Capability changes publish a new adapter snapshot and affect only
named request/session boundaries.

## Identity and session projection

Wire session, request, update, tool-call, permission, content-part, and terminal
IDs map reversibly to distinct AgentKit identities. A wire string is never used
interchangeably as several domain IDs. Reconnect restores mappings from durable
or reconstructible state rather than array position.

Client projection identity includes the authenticated server/host realm,
canonical `SessionId`, and a projection incarnation. Equal session strings on
different servers are different identities. Routing and caches never infer the
realm from whichever server is currently active or use a single-match fallback
after an ambiguous lookup.

Every asynchronous load, subscription, prompt, continuation, permission, and
projection update captures that incarnation. Navigating A -> B -> A creates a
new A incarnation; work captured by the first A remains stale even though the
visible session ID matches again. Disposal increments the fence before teardown
so a late callback cannot repopulate the replacement projection.

New, load, list, fork, resume, cancel, and close operations route through the
canonical workspace/session services. A client-provided `cwd` first resolves to
an authorized `WorkspaceId`; it is not session identity. Adapter-local cached
state is a projection and cannot outvote the session record.

Pagination uses a stable tuple/cursor that cannot skip or duplicate sessions
sharing the same timestamp. Loading a partial message page identifies the cursor
and completeness; it cannot masquerade as the whole transcript.

## Content and event translation

Translation preserves role/trust, ordered typed parts, media/artifact
references, provider/native metadata, tool correlation, provisional versus
committed state, and raw finish/error information. Every lossy projection emits
a diagnostic and retains enough source identity to refresh or inspect the
canonical value.

Live adapter updates reduce the same typed event grammar used by other clients.
They handle duplicate delivery, reconnect snapshot/rebase, tool progress before
terminal state, and late usage/metadata. The adapter does not reread and sum the
entire message history after every delta when the usage ledger already owns the
aggregate.

A delta-only wire projection publishes one bounded base frame, ordered deltas,
and one authoritative terminal aggregate. Its protocol states whether usage is
cumulative or incremental and supplies stable part and tool-call identity before
arguments can stream. It does not attach the complete response-so-far to every
delta: encoded traffic must grow with the content and frame count, not with the
sum of every cumulative prefix. Reconnect snapshots carry an epoch/cursor and
replace the reducer base; a terminal aggregate remains authoritative even when
late metadata differs from an earlier live frame.

A TUI renderer may coalesce frames for display. Machine-readable modes reserve
stdout for framed protocol output and send diagnostics/logs to a separate
channel. Rendering failure never mutates canonical session state.

## Tools, edits, and permissions

Tool calls project canonical name/kind, exact input, affected locations,
progress, output, and status. A frontend-friendly title or simplified tool kind
is presentation metadata, not tool identity or security scope.

Permission requests preserve their authorization/request identity and exact
effect preview. Per-session serialization MAY keep UI prompts intelligible, but
parallel requests retain independent deadlines and terminal resolutions. Client
disconnect follows the selected deferral policy; it never silently allows.

Proposed edits shown by an IDE are derived from the final mutation plan. If a
formatter, hook, fuzzy matcher, or concurrent write changes the plan after the
preview, approval is invalidated. Writing a temporary preview file is itself a
protected mutation and is not required merely to display a diff.

## Models, modes, commands, and resources

Model, thinking variant, agent/mode, command, prompt, skill, and MCP selections
come from one captured directory/resource/catalog snapshot. Wire configuration
options map to stable keys plus revisions. A display label is never sufficient
to select a provider route or grant a capability.

Changing model/mode/settings applies at a named safe boundary and uses
optimistic session versioning. Available-option updates are ordered with the
catalog publication that made them valid; a stale client choice returns a typed
conflict instead of selecting a similarly named replacement.

Authentication instructions such as “run this login command” are presentation
for an explicit credential operation. A terminal-auth capability does not let
the adapter execute the command or inherit terminal authority automatically.

## User shell actions

A user-invoked shell action is a typed control operation, not a model-requested
tool call and not ordinary conversational input. The request records command
provenance, the selected shell/process profile, workspace and identity, and an
explicit result-context disposition such as `IncludeResult` or `ExcludeResult`.
CLI sigils are presentation syntax only; they are never persisted as the
security or context decision.

The terminal result records command, bounded stdout/stderr projections, exit or
signal, cancellation, truncation and artifact references, effect certainty, and
the context disposition. Exclusion from model context does not erase the result
from durable session history or audit. When another run owns the session lane,
live output may continue, but transcript insertion waits for a named safe
boundary after the current assistant/tool-call/result segment. An extension may
select a registered executor or return a typed intercepted result only through
the same process authorization, provenance, validation, and settlement rules.

## Cancellation and idle

Cancelling a protocol request normally cancels that invocation's wait/stream.
Durable abort names the expected operation and is a separate method or explicit
adapter policy. A transport disconnect, closed TUI, or disposed event iterator
does not redefine that boundary.

The adapter reports idle only after canonical run settlement, required output
publication, and adapter event-drain guarantees. It cannot infer idle from the
absence of a local task or from receiving a provider stop event.

## IDE installation and launch

Installing an IDE extension or helper is a separate protected maintenance
operation with exact editor identity, extension version/source, executable,
arguments, environment, and output. Discovery and connection perform no hidden
install. Launch failure, already-installed state, unsupported editor, and
successful installation are distinct outcomes.

## Acceptance scenarios

- Two sessions with the same update time paginate without skip or duplication.
- Equal session IDs on two authenticated server realms never share a cache,
  route, subscription, or permission response.
- After A -> B -> A navigation, completion from the first A incarnation cannot
  mutate the replacement A projection.
- Wire tool IDs map back to canonical call IDs after reconnect.
- Unsupported content produces an explicit lossy/unsupported result rather than
  disappearing.
- Permission disconnect cannot turn denial/defer into allow.
- An edit changed after preview requires a new approval.
- Cancelling one RPC wait does not durably abort the operation.
- TUI render failure and log output never corrupt machine-readable stdout or
  session state.
- A delta-only JSON client reconstructs the terminal message without cumulative
  quadratic traffic or losing the tool-call ID available at call start.
- Every unavailable extension UI operation returns typed unsupported instead of
  succeeding as a no-op or returning a fabricated empty value.
- A user shell result excluded from model context remains durable, and a result
  finishing during tool execution cannot split a tool call from its result.
- A stale model option fails against its captured catalog generation.
- Merely connecting an IDE performs no extension installation.

## Frontend interoperability details

Mode selection is a semantic contract. An adapter may explicitly select an
RPC/JSON mode or switch to print mode when a print flag is set or stdin/stdout
is not a TTY. It publishes the selected mode and reason; redirection does not
silently change whether inputs are one turn, sequential operations, or an
interactive session.

Piped stdin, CLI text, and `@file` content remain separate typed segments.
Trimming stdin or joining initial parts without a delimiter can fuse tokens or
erase intentional boundary whitespace. File expansion that strips BOM or places
an absolute path and content in synthetic tags without size and delimiter bounds
is invalid. AgentKit preserves bytes, encoding, and source; applies
authorization and bounds; and lets context assembly choose escaped framing.
Protocols that do not support attachment syntax reject it explicitly.

A print adapter MAY process remaining CLI messages sequentially, print only
final assistant text, and send aborted operations and errors to stderr with a
failure exit status. An event adapter MAY emit a session header before events.
Output-part selection, initial header, diagnostics channel, and exit taxonomy
are advertised adapter capabilities rather than incidental mode differences.
Help or model listing may load extensions only through a declared discovery-only
or startup profile; it cannot run arbitrary extension effects just to render
help.

A JSONL RPC adapter may multiplex commands, preflight responses, agent events,
and extension UI requests. Because commands overlap and frames interleave, the
protocol requires version, correlation, concurrency, frame bounds, replay,
disconnect, and terminal-preservation rules. Optional IDs, missing sequence and
size limits, or treating stdin EOF as permission to abandon active work are
invalid. The complete requirements live in the
[control-plane specification](coding-harness-export-sharing-and-control-plane.md).

A JSON/RPC projection removes cumulative assistant snapshots from update frames:
message start supplies the base, deltas extend it, and message end is the
authoritative aggregate. It retains cumulative usage and hoists tool-call ID and
name into the call-start frame before removing the partial snapshot. This keeps
wire growth linear. The reducer grammar, usage semantics, terminal authority,
and reconnect base are explicit adapter capabilities.

Extension UI operations such as working indicators, header/footer components,
autocomplete, theme access, raw-terminal access, and tool expansion are
advertised individually. Unsupported editor reads, widget factories, or custom
components return typed unsupported outcomes; an empty string or silent no-op is
not compatibility.

User shell input MAY have two typed context dispositions: include the recorded
result in later model context or exclude it while retaining the durable session
entry. Extensions may intercept execution. If a command finishes during a model
run, its durable process message is deferred to a named safe boundary so it
cannot break tool-call/result ordering. Both dispositions preserve process
authorization and provenance.

Interactive key behavior supplies useful action semantics: abort and restore
queued input, clear-before-exit, exit-on-empty EOF, queue follow-up, retract one
identified input, suspend/continue with terminal restoration, and order
extension cleanup against UI teardown. These are named stateful actions with
collision/platform rules. External editor configuration stores executable and
argv separately rather than splitting on whitespace.

## External protocol projection boundaries

An ACP adapter maps sessions, content, events, tools, permission requests,
usage, modes, models, commands, and MCP registration without becoming their
canonical owner. Adapter-local session maps, timestamp-only cursors, message
rereads, and current-directory routing are insufficient: identity, completeness,
and projection ownership remain explicit.

Frontend incarnation fencing prevents an A -> B -> A transition from accepting a
stale completion from the first A. Session routing includes the server realm; an
omitted realm never falls back to whichever server happens to be active.
Connecting an IDE does not authorize extension installation.

## Related specifications

- [Coding harness execution profile](coding-harness-execution-profile.md)
- [Interactive terminals and process sessions](interactive-terminals-and-process-sessions.md)
- [Workspace mutations and code editing](workspace-mutations-and-code-editing.md)
- [Coding-harness resources and project trust](coding-harness-resources-and-project-trust.md)
