# Coding-harness built-in tools

**Status:** Normative application profile

**Scope:** Optional application composition; not a required AgentKit capability.

**Depends on:** [Tools and toolsets](../../concepts/tools-and-toolsets.md),
[tool lifecycle](../../concepts/tool-call-lifecycle.md),
[coding workspaces](coding-workspaces-and-worktrees.md)

## Purpose

A coding harness needs boring tools with unusually precise contracts: read,
list, glob, search, edit, patch, command, web fetch, language intelligence,
resource loading, questions, planning state, and delegation. Their friendly
names do not excuse ambiguous bounds, hidden effects, or authorization after
observation.

Each tool is an independently selectable feature package or host contribution.
This profile does not mandate one monolithic “coding tools” service.

## Catalog and effect classes

Every descriptor declares one effect class:

| Class                     | Examples                             | Required boundary                                                 |
| ------------------------- | ------------------------------------ | ----------------------------------------------------------------- |
| Workspace observation     | read, list, glob, content search     | File-read authorization before metadata or content observation    |
| Workspace mutation        | edit, write, patch, move             | Exact final mutation transaction                                  |
| Process                   | command, formatter, language server  | Structured process intent and process-tree settlement             |
| Network                   | fetch, search, remote docs           | Destination and classified-egress/ingress policy                  |
| Session/application state | question, plan, todo                 | Typed session records and optimistic versioning                   |
| Delegation                | task/subagent                        | Goal/attempt identity, scoped authority, budget, join, and result |
| Provider-executed         | hosted search, interpreter, computer | Provider request only; never local dispatch                       |

The model receives one immutable catalog snapshot. Name, schema, descriptor
version, selected implementation, and effect class are recorded with each
accepted call. A later plugin, MCP, permission, or workspace reload cannot
silently change the implementation of an already accepted call.

## Read and directory inspection

Read authorization occurs before existence checks, `stat`, directory listing,
suggestion lookup, symlink target inspection, MIME sniffing, or LSP warm-up.
Those are observations too. Error shapes MUST NOT reveal sibling names or target
types outside the authorized scope.

The contract specifies:

- canonical workspace-relative/absolute path rules and external-root policy;
- symlink, mount, case, Unicode, and broken-link behavior;
- byte and line cursor units, one/zero-based indexing, and stable pagination;
- maximum bytes, lines, line length, directory entries, and traversal depth;
- text decoding, invalid sequences, BOM, newlines, binary and media detection;
- deterministic directory ordering and metadata included; and
- whether the result observed one file version/hash or a racy best effort.

Truncation returns the exact observed range and a continuation cursor. “Showing
the first 100” without a stable cursor is not pagination. Reading a media or PDF
attachment follows its declared artifact/media path instead of silently
base64-expanding unbounded content into model context.

## Glob and content search

Glob/search descriptors pin pattern dialect and engine version, base root,
case/newline/Unicode behavior, include/exclude and ignore-file policy, hidden
files, binary files, symlink traversal, maximum visited files/bytes/time, and
result sort order.

Results identify canonical path, exact version when available, line/byte range,
match text bounds, and whether the search was complete. Limit exhaustion,
timeout, cancellation, invalid pattern, unreadable path, and zero matches are
distinct. Continuation either resumes a stable snapshot or explicitly says that
the workspace changed.

An external search binary is a protected process with a captured executable and
arguments. It cannot receive an untrusted pattern through shell concatenation.
Pre-search `stat` and external-directory detection happen under the same
authorized observation scope.

## Command execution

`AgentKit.Tools.Command` is the explicit shell capability. It passes the model's
command as one final structured argument after configured fixed shell arguments;
ordinary `IProcessRunner` requests never infer or invoke a shell. The command
tool resolves the executable, workspace-relative working directory, workspace
access, sandbox profile, timeout, and output bounds before requesting
`Process`/`Execute` authority. The effecting runner resolves those facts again
and consumes the exact single-use grant immediately before process creation.
Child-process policy is part of the fingerprinted intent. The shell feature
explicitly requests sandbox-inherited children; ordinary process callers may
require denial, which is enforced on macOS and fails closed on the initial Linux
profile until a seccomp-backed denial profile is configured.

The first operating-system profile in `AgentKit.Processes` clears the ambient
environment, admits only configured executable paths and environment names,
requires the `workspace-no-network-v1` sandbox, and exposes the workspace as
read-only or read-write. macOS uses a deny-by-default `sandbox-exec` profile;
Linux uses bubblewrap and fails closed when it is unavailable. Neither platform
falls back to an unsandboxed process. `AgentKit.Processes.Scripted` provides
identified deterministic scenarios for unit tests and replay without starting a
host process.

Command results retain independent stdout and stderr byte tails, total observed
byte counts, truncation flags, exit status, and side-effect certainty. Strict
UTF-8 output is projected as text; non-UTF-8 tails are represented losslessly as
base64 rather than replacement text. A nonzero exit is a failed tool outcome
whose typed output remains available. Timeout or cancellation after creation
attempts bounded graceful and forced process-tree termination, reports that
effects may have occurred, and makes inability to confirm reaping explicit. When
a retained tail truncates, the operating-system runner captures the complete
stream up to an independent artifact ceiling and the command result projects the
committed artifact ID, version, and integrity hash. Artifact publication obtains
fresh storage authority and never retries the process.

## Language intelligence

`AgentKit.Tools.Language` provides diagnostics, hover, definitions,
implementations, references, document symbols, and workspace symbols over one
selected `ILanguageIntelligenceService`. Document operations authorize the exact
workspace-relative file; workspace-symbol search authorizes the workspace root,
with query text represented only by its fingerprint in security evidence. The
effecting service consumes that exact `FileRead`/`Observe` grant before it may
inspect language state.

Inputs use one-based line and character coordinates. Provider contracts use
zero-based UTF-16 coordinates and half-open ranges, and projected results return
to one-based coordinates. Successful empty, unsupported, unavailable, denied,
stale, timed-out, cancelled, and failed results remain distinct. Returned arrays
and text are bounded again at model projection, with truncation explicitly
clearing completeness rather than pretending the snapshot was whole.

## Output bounds and spill

Every tool has separate live-event, normalized-result, model-context, durable-
record, and artifact bounds. Truncation says whether head, tail, sampled, or
structured fields were retained and reports original/retained bytes and lines.

Full output that remains useful is written through `AgentKit.Artifacts` with
owner, classification, integrity, retention, and access policy. It is not placed
in a globally shared temp directory and rediscovered by path. Cleanup uses
`TimeProvider` and reference-aware retention.

## Resource loading

`AgentKit.Tools.Resource` provides the local configured-resource baseline. The
host maps stable public resource IDs to protected workspace-relative files and
captures that mapping when the tool is constructed. Catalog listing never scans
the filesystem and never reveals backing paths. A read authorizes the exact path
and complete-file byte bound, and the snapshot boundary consumes the resulting
single-use grant before observation.

Loaded resources use strict UTF-8, optional SHA-256 integrity pins, explicit
source kind/trust/media metadata, and an independent character ceiling. A trust
label records provenance only: resource output explicitly lacks instruction
authority. Discovery, prompt expansion, skill activation, migration,
installation, command-backed values, and remote fetch remain separate captured
pipelines rather than side effects hidden inside `resource`.

## Web fetch and search

`AgentKit.Tools.Web` provides the local `web_fetch` baseline over
`INetworkNameResolver` and `INetworkTransport`; it does not create an
unrestricted HTTP client. The baseline performs GET-only public-data fetches and
does not accept model-provided cookies, authorization values, user information,
or URI fragments. Search remains a separately selectable provider-backed or
endpoint-configured capability—AgentKit never fabricates a search service or
credential as a default.

`AgentKit.Tools.WebSearch` implements that selectable `web_search` surface over
one `IWebSearchProvider`. Its registration fails composition until the host
selects an operation. The tool authorizes classified query egress to the
provider's exact secret-free destination, and the provider must consume the same
grant immediately before its single attempt. Queries, canonical domain filters,
freshness, result and time ceilings are fingerprinted together. Returned
correlation, HTTP(S) URLs, requested-domain containment, counts, and text bounds
are checked again; bounded results remain untrusted data and an oversized
response is explicitly incomplete rather than silently whole.

Network tools validate URI syntax and scheme, resolve DNS through the protected
network boundary, authorize every redirect/effective destination, prevent
credential forwarding, and bound connect/header/idle/overall timeouts, redirect
count, headers, compressed bytes, decompression expansion, and final content.

MIME is validated from declared and sniffed evidence. Charset and decoding are
explicit. HTML-to-text/Markdown conversion is a deterministic transform with
diagnostics; it does not make remote content trusted. Scripts, embedded objects,
data URLs, link targets, and prompt-like instructions remain untrusted data.

The first-party fetch profile accepts bounded textual media, rejects binary NUL
evidence and unsupported/compressed representations, performs strict UTF-8,
UTF-16, ASCII, or Latin-1 decoding, removes script/style/object/embed regions
from declared HTML, and reports declared and conservatively sniffed media
evidence. Character truncation is explicit; byte-limit or deadline exhaustion is
a failure, never truncated success.

Retry after a challenge or alternate user agent is a new network attempt with a
fresh grant and attempt record. The result preserves final URL, redirect chain,
status, media type, validators/cache evidence, and truncation without exposing
credentials or cookies.

## Session-state and human tools

Questions, plans, and todos are typed application/session state, not magic chat
strings. They carry stable IDs, author, version, status transitions, ordering,
and terminal/cancellation behavior. Updating them uses optimistic concurrency
and cannot forge a run or tool terminal outcome.

Human questions use the normal deferred/approval channel with bounded options,
free-text policy, deadline, identity, and exactly one resolution. Rendering a
question is not acceptance of its answer.

The local baseline is `AgentKit.Tools.Question` over `IHumanQuestionBroker`.
Question publication is an application-state create effect, so the tool obtains
an exact grant and the first-party `AgentKit.IO.DefaultHumanQuestionBroker`
consumes it before presentation. Application channels receive
`HumanQuestionPrompt`, never the grant. They authenticate a response as new
input and, when durable waiting is promised, persist the pending prompt and its
exactly-one resolution independently of an in-memory task. A missing headless
channel returns an explicit unavailable result; response deadlines return an
explicit timeout. Selected IDs, optional text, and bounds are revalidated before
the answer is projected to the model as non-authoritative data.

`AgentKit.Tools.Plan` is the local planning baseline. `get` observes the current
session plan, `replace` installs a complete bounded snapshot against an expected
revision (or explicitly expects no current plan), and `set_status` advances one
stable item against an exact positive revision. The default store consumes state
authority before session access and appends each accepted revision as a typed
`PlanSessionEntry`. The tool rejects duplicate IDs, multiple in-progress items,
stale revisions, missing sessions, and oversized presentation before any
authorization or state access. Plan state is operational data; it does not gain
instruction precedence merely because the model authored it.

`todo` is a compatibility name over that same canonical plan state. Exposing
both names does not create two lists, two revision streams, or two security
resources; a change through either name is immediately visible through the
other.

`skill` lists or activates one entry from the immutable catalog also exposed by
the context inventory source. Listing returns only bounded public metadata and
the catalog version; it neither observes backing paths nor authorizes file
access. Activation uses the same stable ID and catalog version, requires an
exact protected snapshot read, validates integrity and UTF-8, and reports
truncation. Skill text remains non-authoritative tool data and activation never
installs dependencies, runs scripts, migrates files, or changes project trust.

## Delegation and orchestration code

Delegation creates a scoped child goal/attempt with explicit context, tool
catalog, workspace placement, authority attenuation, budget, cancellation, join,
and result publication. A child cannot inherit every host credential or
permission merely because its parent could access them.

`AgentKit.Tools.Task` exposes this boundary as `task`. The model selects one
target agent, bounded objective and acceptance criteria, an exact tool
allow-list, turn/tool-call ceilings, and a settlement timeout. The tool requires
a durable session and active run, authorizes the full envelope as one
`Delegation/Create` effect, and delegates only through `ITaskDelegationBroker`.
The default broker in `AgentKit.Goals` consumes that grant immediately before
dispatch; it supplies no default goal channel. A rejection before child creation
returns no child identities, while a child result preserves the durable goal,
attempt, session, run, status, and side-effect certainty and remains untrusted
data in the parent context.

A sandboxed orchestration/code-mode tool is still a tool scheduler. It uses the
captured child-tool catalog; validates and authorizes every nested call; assigns
stable nested call IDs; enforces call/depth/time/output budgets; records
progress; and terminally settles every admitted child. Interpreter confinement
limits consequences but does not authorize MCP, file, process, or network
effects.

## Acceptance scenarios

- An unauthorized missing path leaks neither existence nor sibling suggestions.
- Read pagination preserves an unterminated final line and split multibyte
  character according to its decoding contract.
- Search limit, timeout, invalid pattern, and no matches have distinct results.
- Ignore, hidden-file, binary, symlink, and case behavior are fixture-tested on
  every supported platform.
- Artifact spill preserves full output and enforces the caller's authority and
  retention policy.
- Redirect to a different origin requires a new grant and strips credentials.
- Remote instructions remain untrusted after HTML-to-Markdown conversion.
- Nested orchestration cannot invoke a child tool absent from the captured
  catalog or grant.
- Provider-executed tools never enter the local tool invoker.

## Implementation-sizing and compatibility details

Every bound includes both a number and a retention shape. A useful sizing
profile is 2,000 lines or 50 KiB: file reads retain the head, shell output
retains the tail, and search additionally clips individual match lines. AgentKit
does not hard-code those values universally, but every tool profile declares
line, byte, per-line, head/tail, continuation, and full-output policy.

A line read never returns a partial first line and supplies a typed continuation
offset. Advice to continue reading is structured data; it never interpolates an
unquoted path into a suggested shell command. Full shell output spills through
`AgentKit.Artifacts`, not a randomized ambient temp path, with classification,
integrity, authority, retention, redaction, and cleanup.

Model, display, durable/audit, live, and artifact output are separate bounded
projections derived from one authoritative result. Terminal-control sanitizing
only the TUI while sending raw bytes to the model or spill is not a complete
normalization policy. When pipe stdout and stderr are merged for a tool result,
arrival order is explicitly nondeterministic and the original streams remain
available where policy permits.

An edit compatibility profile MAY accept a JSON string instead of an edit array,
a single edit object, or legacy top-level old/new text. Such repairs are
versioned and diagnosed; canonical validation still runs afterward. Edits match
unique, non-overlapping ranges against the original version, preserve BOM and
declared line endings, and return both model-facing change data and an exact
patch/artifact projection.

Hazardous compatibility behavior remains forbidden: absolute paths outside a
registered workspace, UTF-8-decoding arbitrary binary files, non-atomic
overwrite without a version precondition, abort checks only after bytes were
written, or unmanaged temporary spill files. An abort that loses the race with a
write reports the committed or uncertain effect; it cannot throw a clean
pre-effect cancellation.

## Related specifications

- [Workspace mutations and code editing](workspace-mutations-and-code-editing.md)
- [Interactive terminals and process sessions](interactive-terminals-and-process-sessions.md)
- [Language services, formatters, and watchers](language-services-formatters-and-watchers.md)
- [Goals and multi-agent delegation](../../concepts/goals-and-multi-agent-delegation.md)
