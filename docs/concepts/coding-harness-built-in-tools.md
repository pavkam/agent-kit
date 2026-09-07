# Coding-harness built-in tools

**Status:** Normative coding-harness profile  
**Depends on:** [Tools and toolsets](tools-and-toolsets.md),
[tool lifecycle](tool-call-lifecycle.md),
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

## Output bounds and spill

Every tool has separate live-event, normalized-result, model-context, durable-
record, and artifact bounds. Truncation says whether head, tail, sampled, or
structured fields were retained and reports original/retained bytes and lines.

Full output that remains useful is written through `AgentKit.Artifacts` with
owner, classification, integrity, retention, and access policy. It is not placed
in a globally shared temp directory and rediscovered by path. Cleanup uses
`TimeProvider` and reference-aware retention.

## Web fetch and search

Network tools validate URI syntax and scheme, resolve DNS through the protected
network boundary, authorize every redirect/effective destination, prevent
credential forwarding, and bound connect/header/idle/overall timeouts, redirect
count, headers, compressed bytes, decompression expansion, and final content.

MIME is validated from declared and sniffed evidence. Charset and decoding are
explicit. HTML-to-text/Markdown conversion is a deterministic transform with
diagnostics; it does not make remote content trusted. Scripts, embedded objects,
data URLs, link targets, and prompt-like instructions remain untrusted data.

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

## Delegation and orchestration code

Delegation creates a scoped child goal/attempt with explicit context, tool
catalog, workspace placement, authority attenuation, budget, cancellation, join,
and result publication. A child cannot inherit every host credential or
permission merely because its parent could access them.

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
- [Goals and multi-agent delegation](goals-and-multi-agent-delegation.md)
