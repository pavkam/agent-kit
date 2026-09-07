# Artifact and content storage

**Status:** Normative

**Architecture:** [Artifacts](../architecture/artifacts.md)

**Depends on:** [Message and content model](message-and-content-model.md),
[permissions, approvals, and trust](permissions-approvals-and-trust.md),
[execution identity and tenancy](execution-identity-and-tenancy.md)

## Purpose

Large, binary, generated, or reusable content needs a durable boundary that is
neither conversation history nor agent memory. Artifact storage owns bytes and
metadata referenced by messages, tool results, media, evaluations, checkpoints,
and exported reports.

## Artifact identity and metadata

An artifact reference MUST include stable typed identity, tenant and owner
scope, media type, length, content hash, classification, creation time,
retention policy, storage version, and integrity metadata. A reference MUST say
whether content is immutable, appendable, or externally owned.

Content-addressed identity MAY deduplicate bytes, but deduplication MUST NOT
reveal that another tenant or principal possesses the same content. A logical
artifact identity remains distinct from its content hash and backend location.

Backend keys, filesystem paths, signed URLs, credentials, and provider-native
handles MUST NOT become portable artifact identity.

An agent selects a logical artifact profile, and requests address a logical
artifact directory within that profile. The immutable profile snapshot maps the
directory to a backend as composition data. Backend keys are available to the
composition root and artifact runtime only; they MUST NOT appear in portable
write requests, references, session records, messages, or provider payloads.
Changing a route creates a new profile version and MUST preserve resolution of
references created under older versions.

## Read and write lifecycle

Writes MUST be bounded, cancellable, integrity checked, and use explicit
prepare, finalize, and abort operations. Prepare returns only a typed staging
receipt. Finalize atomically publishes the staged version and returns its first
portable `ArtifactReference`; abort idempotently makes an unfinalized staging
receipt ineligible for publication. A partially uploaded or merely prepared
artifact is not readable as a committed artifact. Streaming reads and writes
MUST document buffer ownership, maximum size, seek behavior, disposal, and
whether cancellation leaves resumable state.

The coordinator validates metadata and authority before selecting a backend. The
backend revalidates the bounded grant immediately before reading, writing,
publishing, or deleting content. Redirects and external downloads follow the
network security boundary rather than bypassing it through artifact storage.

Grant evidence binds the operation and the complete portable reference,
including its directory, profile/version, tenant and owner, content metadata,
integrity, classification, and retention decision. Changing a reference after
authorization requires a new evaluation; retaining only its artifact ID,
version, and content hash is insufficient. The backend also compares the
supplied reference with its authoritative stored reference before exposing or
deleting content. A caller-supplied reference cannot redefine the stored owner
or hold.

Fingerprint formats are versioned and domain-separated by operation. An upgrade
that binds previously omitted fields invalidates grants from the incomplete
format; enforcement MUST NOT fall back to that format. Pending work obtains new
authority before a new effect. Already committed effects remain committed and
are reconciled from their recorded evidence rather than repeated to obtain a new
fingerprint.

### Preparation authority and replay identity

Prepare has two distinct comparison boundaries. Its per-attempt grant binds the
reserved artifact and preparation IDs, immutable artifact version, selected
profile key and version, authenticated tenant and creating principal, directory,
complete metadata and content fingerprint/length, and concrete staging creation
and expiry instants. The backend recomputes this evidence from the actual
request. Changing any of these fields after authorization requires a new
decision. The separately carried tenant and creator must equal the authenticated
identity's tenant and principal; an ownership label cannot impersonate the
creator.

The coordinator captures all selected profile and lifecycle settings before
authorization and uses those same values to issue the backend request. Mutation
of a caller-owned options object or a later configuration publication cannot
change an already authorized attempt.

Prepare idempotency compares stable intent within the tenant partition: creating
principal, immutable artifact version, profile key/version, directory, metadata,
exact content, and staging lifetime policy. For a relative staging lifetime,
compare the positive duration between creation and expiry, rather than the
absolute instants. These staging bounds are distinct from the artifact's
retention expiry, which remains part of the exact metadata.

A retry may generate new reservation IDs and later staging instants. Those
fields remain bound in its fresh grant but are excluded from stable replay
matching. An otherwise identical request returns the original preparation
receipt, IDs, and expiry. It does not create new staging state or extend the
original lifetime. Changed version, profile, creator, content, or lifetime under
the same idempotency key returns a typed conflict. Reusing the original grant is
governed by its authoritative use count; receipt replay never replenishes a use.

Creation must precede staging expiry. Both instants are compared and
fingerprinted by their UTC value so equivalent offset representations produce
the same evidence. A longer staging duration is a policy change, not an
incidental consequence of retrying later.

## References in durable state

Messages and session records store immutable artifact references, not incidental
local paths or open streams. Provider adapters may dereference an artifact only
through an authorized resolver and must record any upload or provider-file
translation in request provenance.

Tool-result normalization MAY externalize oversized content into an artifact and
return a bounded reference. It MUST mark the transformation and preserve the
original media type, length, hash, and security classification.

## Consistency and orphan handling

Artifact creation and a session or tool-result append commonly span different
stores. Implementations MUST use an explicit prepare/finalize protocol, outbox,
or compensating orphan policy; they MUST NOT pretend the writes are one atomic
transaction when they are not.

An artifact may be finalized before its durable reference is committed. The
returned reference is resolvable only through ordinary authorization; artifact
storage cannot infer the transaction state of another store. The caller MUST
record a reference-commit intent before finalization and reconcile it after
finalization and reference append. A pending preparation is never a readable
message reference.

Automatic collection MUST honor a durable pin or equivalent retention fence
covering a pending commit. An expired timer alone does not prove an orphan:
collection must fence late commits and establish the intent's terminal state, or
retain the object for reconciliation. The caller owns that cross-store protocol;
the artifact runtime never calls back into the session/tool owner. The exact
protocol is defined in
[reference commitment](../architecture/artifacts.md#reference-commitment-and-garbage-collection).
Deleting a reference does not delete shared bytes until retention, hold, and
reference accounting permit it.

## Retention, deletion, and external ownership

Retention policy MUST distinguish ephemeral run output, session-owned content,
durable memory sources, evaluation evidence, and externally owned resources.
Deletion is idempotent and auditable. A tombstone prevents a stale reference
from silently resolving to different bytes.

Deletion replay retains tenant and canonical reference evidence. Matching an
artifact ID and version alone cannot produce an already-deleted receipt for a
different tenant or an altered reference. Foreign or mismatched references
remain indistinguishable from unavailable content, including after deletion. The
stored reference remains authoritative for retention and ownership checks.

Store indexes and replay state are tenant-qualified. Preparation, finalization,
abort, and deletion state in one tenant cannot reserve identifiers or change
receipts in another tenant, even when their raw preparation or artifact IDs
match. Lookup derives the tenant from the authenticated request identity and
checks any separately supplied tenant before using it. A supplied artifact
reference must match both that tenant and the stored canonical reference.

An abort for an unknown preparation returns `NotFound` and creates no tombstone
or identifier reservation. `AlreadyAbsent` is a replay result backed by that
tenant's recorded abort or deleted publication, not a claim that every unknown
identifier was previously aborted. Foreign preparation lookups follow the same
unknown result and never inspect another tenant's state to choose a response.

Within one tenant, an immutable artifact ID/version is never rebound, including
by a new preparation after deletion. Finalization conditionally claims that
version before changing preparation or publication state. Competing preparations
in one tenant have one publication winner; a losing preparation receives a typed
conflict and remains available for explicit abort. A failed publication cannot
leave a successful receipt pointing to another preparation's bytes.

The finalized reference MUST carry the resolved retention decision, integrity
evidence, mutability mode, and ownership kind. Append-only content creates a new
typed version; immutable content cannot be overwritten. External ownership MUST
name a stable unsigned resource locator, the external owner, and whether delete
authority was delegated. A signed URL or current backend route is never that
stable locator.

Externally owned URIs remain references to external content. AgentKit MUST NOT
claim durability, immutability, or deletion of content it does not own.

## Failure behavior

Unsupported media, size overflow, hash mismatch, classification conflict,
authorization failure, backend unavailability, partial upload, stale version,
and deletion conflict are typed outcomes. Safe diagnostic metadata is retained;
raw sensitive content is not copied into exceptions or telemetry.

## Acceptance scenarios

- An interrupted upload never resolves as a committed artifact.
- A content hash match across tenants does not reveal existence or grant access.
- A tool result can externalize large binary output without losing media type,
  integrity, or causality.
- A message replay resolves the same immutable artifact version or returns a
  typed unavailable/tombstoned result.
- Failure to append an artifact reference leaves a reconcilable orphan rather
  than invisible permanent storage.
- Deletion honors retention and legal hold and cannot replace old bytes under a
  stale reference.

## Related specifications

- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Sessions, persistence, and branching](sessions-persistence-and-branching.md)
- [Memory, retrieval, and storage](memory-retrieval-and-storage.md)
- [Testing and evaluation](testing-and-evaluation.md)
- [Observability and audit](observability-and-audit.md)
