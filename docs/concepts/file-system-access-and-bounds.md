# File-system access and bounds

**Status:** Normative host boundary

**Architecture:** [File system](../architecture/file-system.md)

**Depends on:**
[Permissions, approvals, and trust](permissions-approvals-and-trust.md),
[architecture and dependency boundaries](architecture-and-dependency-boundaries.md),
[error taxonomy](error-taxonomy.md)

## Purpose

File access is a protected host effect, not a utility. This specification
defines the observable behavior every file-system implementation MUST provide so
that reads are bounded, writes are explicit and atomic, and authority is
enforced at the byte boundary rather than assumed from a higher-level approval.

It specifies the boundary itself. Model-facing windows, edit planning, and
workspace orchestration belong to [tools](tools-and-toolsets.md),
[workspace mutations](../profiles/coding-harness/workspace-mutations-and-code-editing.md),
and [workspaces](../profiles/coding-harness/coding-workspaces-and-worktrees.md).

## Boundary ownership

Framework, tool, and integration code MUST NOT call operating-system file APIs
directly. It MUST depend on narrow capability contracts covering reading,
writing, directory enumeration, metadata, temporary storage, and optional change
observation.

A consumer MUST receive only the capabilities it uses. One oversized service
granting every file operation is forbidden. An implementation MUST declare its
supported capabilities before selection, and selection MUST reject an
incompatible operation before any effect.

Implementations MUST NOT fall back to raw host APIs, widen a configured root, or
treat an unsupported operation as an ordinary discovery failure.

## Path identity

Paths are logical values bound to an explicit root and comparison policy. A raw
host path MUST NOT become a portable identifier implicitly.

Each profile MUST define normalization, case sensitivity, symbolic-link
behavior, and root containment. Pure lexical normalization precedes
authorization. Resolving external links, mounts, metadata, or content requires
its own bounded observation authority; the final effect grant binds the observed
canonical target. Resolution that escapes a configured root MUST fail closed.

Symbolic-link traversal is an authorization input. A grant for a normalized path
MUST NOT authorize a different link target discovered later.

## Authorization at the effect

A caller MUST send the canonical operation through the security authority before
the effect. The implementation MUST revalidate the resulting bounded grant
against the exact operation, principal, audience, target, link evidence, and
input fingerprint, atomically consume a use, and emit required audit immediately
before acting.

If any of those checks cannot be performed, no host call may occur. Missing,
expired, consumed, mismatched, or unauditable grants MUST deny before access.

A sandbox or restricted root MAY narrow consequences but MUST NOT grant
permission. Mutation of the target, path, link target, content fingerprint,
principal, or operation after evaluation MUST require a fresh security request;
a prior approval MUST NOT be replayed for changed facts.

## Read bounds

Read bounds limit bytes actually obtained from the backing store. Metadata
length observed before opening MUST NOT be treated as enforcement.

Every returned stream MUST be bounded. The implementation MUST stop before
exposing bytes past the authorized limit and MUST return typed limit or
truncation evidence when the target grows, metadata is stale, decompression
expands content, or a remote source exceeds the bound. Reading a whole file and
slicing afterward is not an implementation of bounded access.

The boundary owns bytes, snapshot or version evidence, and content integrity.
Line, page, or character windows belong to the consuming feature. A continuation
MUST bind the resolved target, snapshot or version evidence, decoding profile,
and next position; if the target changed, continuation MUST return conflict
rather than joining content from two versions.

Reaching the exact byte ceiling does not prove EOF. Without stable snapshot
length evidence, the reader MUST report limit/unknown EOF and MUST NOT consume
an extra unauthorized probe byte. A complete-file fingerprint requires a
complete stable snapshot and cannot be supplied from a truncated prefix.

## Text interpretation

Text access MUST select a declared decoding profile stating encoding,
malformed-input behavior, byte-order-mark interpretation, and newline
projection. It MUST NOT depend on process locale or an ambient default encoding.

A leading byte-order mark is encoding evidence rather than ordinary text unless
the selected profile explicitly says otherwise.

## Write dispositions

Every write MUST select one disposition explicitly. There is no destructive
default. The target-state test and the commit MUST form one atomic operation
with respect to competing writers.

| Disposition       | Missing target           | Existing regular file      |
| ----------------- | ------------------------ | -------------------------- |
| `CreateOnly`      | Create, report `Created` | Conflict, no mutation      |
| `ReplaceExisting` | Not found, no mutation   | Replace, report `Replaced` |
| `CreateOrReplace` | Create, report `Created` | Replace, report `Replaced` |
| `Append`          | Not found, no mutation   | Append once, report        |

`CreateOrReplace` MUST be requested and authorized deliberately. A tool schema,
registration helper, or host adapter MUST NOT select it when the disposition is
absent.

A target state or fingerprint captured for authorization is a commit
precondition. A racing change MUST produce conflict rather than falling back to
another disposition. An implementation that cannot provide the requested atomic
test and commit MUST report unsupported before mutation.

Atomic visibility, conditional target-state commit, and crash durability are
separate guarantees. Profiles MUST name the enforced writer-isolation domain.
Cooperative locks cannot guarantee a fingerprint condition against independent
writers. A read/check followed by rename MUST NOT be advertised as a conditional
atomic commit unless a primitive or exclusive workspace boundary enforces that
condition through publication. Unsupported required guarantees fail before
mutation; see the
[isolation contract](../architecture/file-system.md#isolation-visibility-and-durability).

## Atomicity and bounds on write

Streamed writes MUST consume at most the authorized bound through a bounded
reader and compute the payload fingerprint over bytes actually consumed. A
declared length, seekable stream length, or model-provided size MUST NOT replace
this enforcement.

Atomic create and replace MUST stage validated content outside the visible
target and publish it in one commit. Limit, cancellation, encoding, or
fingerprint failure before that commit MUST leave the prior target unchanged.

Append policy bounds the committed total size, not only the appended payload.
Under the same concurrency boundary that commits the append, the implementation
MUST check actual current size with overflow-safe arithmetic, verify any
expected target fingerprint, and append exactly once without interleaving bytes
from another append. The capability MUST state whether cancellation or host
failure can expose a partial append; a policy requiring atomic append MUST
reject an implementation that cannot provide it, and an uncertain side effect
MUST NOT be reported as a clean failure.

Text writes additionally declare encoding, BOM, and newline behavior. Byte
counts, limits, and fingerprints are computed after those policies produce the
exact bytes. Append MUST NOT insert a BOM mid-file and MUST reject an
incompatible existing text format unless an explicit conversion was authorized.
Empty and whitespace-only payloads are valid; missing content is not. No layer
may silently trim content or add a terminator.

## Secondary effects

Creating a missing parent directory is a separate protected effect. The ordinary
write contract MUST return a typed parent-not-found result. A caller wanting
parents created MUST declare and authorize each directory creation or use an
explicit compound operation whose directory and file effects remain separately
bound and audited. A writer MUST NOT call an implicit recursive create as a
convenience.

Directory pages MUST bind continuation to the fingerprint of the complete
ordered name snapshot. Resumption after a change MUST report a typed
snapshot-changed result instead of mixing two directory versions.

## Determinism and time

Deterministic implementations MUST use the injected `TimeProvider` for created,
modified, expiry, and watcher timestamps. A real implementation reports host
metadata as external truth but MUST use injected time for framework-owned
deadlines, polling, retries, and event times.

Enumeration order, path comparison, and conflict detection MUST be deterministic
for the same versioned inputs.

## Acceptance scenarios

- A read of a file that grows after metadata inspection stops at the authorized
  bound and returns typed truncation evidence.
- A continuation against a changed target returns conflict rather than content
  spanning two versions.
- `CreateOnly` against an existing file returns conflict and leaves bytes
  unchanged.
- `ReplaceExisting` against a missing file returns not found and creates
  nothing.
- A write whose captured fingerprint no longer matches at commit returns
  conflict instead of downgrading to another disposition.
- A streamed write exceeding its bound aborts before commit and leaves the prior
  target intact.
- Two concurrent appends both commit without interleaving bytes, and the final
  size bound is enforced against the committed total.
- A write to a path whose parent is missing returns parent-not-found without
  creating directories.
- A grant issued for one normalized path denies after the path resolves to a
  different symbolic-link target.
- An expired, consumed, or unauditable grant denies before any host call.
- A profile lacking atomic replacement reports unsupported rather than
  performing a non-atomic replace.
- Text decoded under a declared profile produces identical results regardless of
  process locale.

## Related specifications

- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Network access and egress](network-access-and-egress.md)
- [Process execution and sandboxing](process-execution-and-sandboxing.md)
- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Artifact and content storage](artifact-and-content-storage.md)
- [Testing and evaluation](testing-and-evaluation.md)
