# Coding-harness export, sharing, and control plane

**Status:** Normative coding-harness profile  
**Depends on:** [Coding harness execution](coding-harness-execution-profile.md),
[execution identity](execution-identity-and-tenancy.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

A local or remote harness control plane selects workspaces, owns runtime
instances, exposes versioned routes and event streams, and may export or publish
session data. Routing, authentication, ownership, backpressure, and data egress
must be explicit. “It only listens on localhost” is not an authority model.

## Control-plane boundary

Every request is admitted with:

- authenticated immutable execution identity and tenant;
- route family and semantic version;
- selected `WorkspaceId`, session/operation identity when applicable, and
  expected descriptor version;
- request/idempotency identity, body/media classification, and size limits;
- origin, audience, transport security, and channel-specific authorization; and
- cancellation and disconnect policy.

Workspace selection from a query parameter, header, or ambient server current
directory is untrusted input. It is resolved to a registered workspace and
authorized for the caller before an instance boots or any file is read. Tokens,
credentials, and attach tickets MUST NOT appear in URLs.

Authentication is mandatory for non-public routes. Comparison is constant-time
where shared secrets are used; production profiles define TLS, origin/CORS,
cookie/CSRF, proxy-header, loopback, and browser exposure policy. Optional auth
is an explicitly insecure development profile that cannot be selected by
accident.

## Instance ownership and placement

The host caches runtime instances by canonical workspace identity and effective
profile revision, not caller path spelling. Concurrent opens join one boot. A
failed boot is evicted, a reload fences the prior instance, and disposal drains
resources exactly once.

A client or multiplexing gateway that can address more than one host prepends an
authenticated `ServerRealmId` to workspace, session, instance, and cache
identity. Identical IDs from different realms never collide. Missing or
ambiguous realm selection is a typed routing failure; it cannot fall back to the
currently active server merely because one tab happens to match.

An execution-location fence binds each admitted operation to the intended local
workspace, container, remote host, or worktree. Tool, terminal, LSP, and file
effects cannot silently run on a different placement after routing or failover.
Moving a session between placements is an explicit handoff with compatibility,
authority, lease, and recovery checks.

## Routes and runtime generations

Route versions name behavioral contracts, not just JSON shapes. If two runtime
generations coexist, each route declares which admission, session, event,
settlement, and error semantics it implements. A compatibility route MAY
translate to one canonical runtime; it cannot expose two contradictory state
machines behind the same version.

Unknown fields and future event variants follow a documented compatibility
policy. Every route has independent request, response, concurrency, deadline,
and rate limits. Long-lived streams do not inherit unlimited buffering merely
because their HTTP request was accepted.

Local JSONL/RPC is also a control-plane route. Commands, preflight/acceptance
responses, operation terminals, semantic/live events, and extension UI requests
are separately tagged multiplexed families. Acceptance is not completion.
Command IDs are mandatory where a response exists; UI correlation uses its own
typed identity. Optional concurrent dispatch requires expected state/version or
an explicit commutativity rule, and responses/events may interleave only under a
documented event sequence.

The framing profile states protocol version, UTF-8 and newline rules, maximum
encoded bytes, final unterminated-record behavior, duplicate IDs/commands,
unknown commands and fields, late UI responses, and flow control. EOF,
disconnect, and signals say whether accepted operations detach, abort, or remain
recoverable. A terminal outcome is durably preserved before a host may skip an
output flush.

## Streaming and reconnect

SSE, WebSocket, RPC, and terminal channels use the shared typed event model.
Subscriptions are bounded and identify a durable snapshot sequence or passive
live cursor. Reconnect either resumes all available events or returns an
explicit gap requiring a fresh snapshot.

Heartbeat frames are not semantic progress. Disconnect cancels the observation
according to channel policy; it does not silently abort durable work. A server
MUST NOT retain an unbounded queue per slow client.

## Export is not sharing

An export creates a bounded artifact under session/artifact retention policy. A
share publishes classified data to a network destination. They are different
operations even if they use the same serializer.

An export manifest identifies schema version, source session/branch/workspace,
selected record ranges, message/part kinds, tool data, diffs, artifacts,
provider metadata, usage, settings, and exclusions. It preserves stable
identities or records reversible remapping. Secrets and authority-bearing values
are excluded by construction.

Sharing additionally requires:

- explicit caller consent over a redacted preview and destination;
- field-level classification and configurable exclusion of prompts, files,
  diffs, tool inputs/results, reasoning, provider IDs, model/settings, and
  identity metadata;
- destination and data-egress authorization over the final serialized bytes;
- one idempotent durable outbox item before network activity;
- bounded retry using destination acknowledgement, never fire-and-forget;
- terminal publication receipt or exact uncertain/partial state;
- expiry, revocation, deletion, access visibility, and retention policy; and
- audit records that contain fingerprints and classifications, not the payload.

Auto-share is permitted only when a prior durable policy and consent grant bind
the exact data classes and destination. A UI toggle or background promise with
lossy in-memory retry is not durable publication.

## Import and trust

Imported exports are untrusted history and resources. Schema validation,
identity remapping, media/artifact bounds, tool-call repair, provider metadata
affinity, and tenant authorization happen before any entry becomes canonical.
Historical approvals, credentials, grants, workspace trust, or executable
extensions never import as current authority.

## Acceptance scenarios

- A caller cannot select an unauthorized workspace by query, header, path alias,
  or server current directory.
- Equal workspace or session IDs on two server realms route to independent
  instances and caches; an omitted realm cannot select the active server.
- Concurrent opens boot one instance; failed boot leaves no poisoned cache
  entry.
- A compatibility route cannot report idle before canonical settlement.
- An RPC prompt acknowledgement cannot be mistaken for its later terminal
  operation result, even when other command responses interleave.
- A slow SSE client receives a gap or disconnect without unbounded memory.
- Disconnecting a client leaves accepted durable work governed by its operation
  policy.
- Export performs no network activity; share cannot reuse export permission.
- Share failure after send remains in a durable uncertain state until
  acknowledged or reconciled.
- Redaction occurs before final-byte egress authorization.
- Imported approvals and extension manifests convey no current authority.

## Control-plane interoperability boundaries

Coexisting API generations require explicit version routing and cannot share an
ambiguous compatibility path. Authentication is mandatory according to the
selected deployment profile; caller-selected workspace identifiers are resolved
through authorized canonical routing, never a process-global current directory.

Sharing uses a durable bounded queue and a redaction-minimized payload. An
in-memory queue cannot establish send certainty after process loss, and a broad
session serialization is not an acceptable egress shape merely because it is
convenient.

Admission, run coalescing, context epochs, ordered publication, and replay plus
live-event aggregation remain canonical runtime semantics. A control-plane
adapter projects them; it does not reimplement them.

## Related specifications

- [Streaming and event protocol](streaming-and-event-protocol.md)
- [History validation and repair](history-validation-and-repair.md)
- [Artifact and content storage](artifact-and-content-storage.md)
- [Observability and audit](observability-and-audit.md)
