# Workspace mutations and code editing

**Status:** Normative coding-harness profile  
**Depends on:** [Tools](tool-call-lifecycle.md),
[permissions](permissions-approvals-and-trust.md),
[coding workspaces](coding-workspaces-and-worktrees.md)

## Purpose

Coding edits are transactions over exact paths and bytes. Fuzzy matching,
formatting, patch moves, and watcher notification are part of the observable
mutation—not harmless implementation details that may happen after approval.

The tool runtime coordinates the high-level operation. The selected file-system
implementation enforces the same bounded grant at each concrete effect.

## Mutation set

Before authorization, the harness resolves one immutable mutation plan whose
entries include:

- canonical source and destination identities;
- operation kind: create, replace, patch, move, delete, metadata change, or
  directory operation;
- expected existence, file identity, size, content hash, mode, and link policy;
- input and final output encoding, BOM, newline, and terminal-newline policy;
- exact final bytes or their bounded cryptographic digest plus secure staging
  reference;
- formatter or code-action identity and version, when selected; and
- rollback/reconciliation class and watcher/LSP notification set.

A move has two protected targets. A directory rename covers the complete source
and destination scope. A patch that creates, deletes, or moves files cannot be
authorized as a generic edit to the patch file itself.

## Required transaction

A mutation follows this order:

1. canonicalize source and destination with symlink-aware containment;
2. read and record all preconditions without following an unauthorized target;
3. resolve fuzzy text, patch offsets, or code actions to one exact candidate;
4. compute formatting or other deterministic post-processing in a non-mutating
   preview when it changes committed bytes;
5. validate size, binary/text classification, encoding, patch coverage, and the
   complete mutation set;
6. authorize the final paths, bytes/digests, metadata, and destinations;
7. lock canonical paths in deterministic order;
8. revalidate every precondition immediately before the first effect;
9. stage writes and use atomic replacement where the file system supports it;
10. commit or durably record exact partial effects and reconciliation state;
11. release locks, then notify watchers and language services from the settled
    outcome; and
12. publish one per-path result plus one operation-level terminal result.

If a formatter, hook, fuzzy matcher, encoding conversion, line-ending policy, or
provider-supplied patch changes the authorized bytes, the runtime obtains a new
security decision. Authorizing the pretty diff and writing something else is a
security bug with excellent typography.

Cancellation is checked before the effect gate and remains observable during the
write, but a cancellation noticed after replacement cannot erase the fact that
bytes changed. The outcome then reports committed or uncertain mutation and
reconciles the file version; it MUST NOT return a clean “aborted before effect”
exception.

## Text semantics

Text tools MUST define:

- byte and character encodings accepted, invalid-sequence behavior, and Unicode
  normalization policy;
- BOM preservation or deliberate change;
- CRLF/LF detection, mixed-newline behavior, and final newline handling;
- whether offsets use bytes, Unicode scalar values, UTF-16 code units, or
  one-based line/column pairs;
- exact-match cardinality and ambiguity diagnostics;
- binary detection and maximum input/output sizes; and
- behavior for files changing concurrently between read and commit.

Fuzzy replacement MAY produce a candidate for user/model correction. It MUST NOT
silently choose among multiple matches, cross an authorization boundary, or
weaken a hash/version precondition.

## Patch batches and atomicity

Patch parsing is side-effect free and produces a complete ordered plan before
execution. Paths are normalized relative to the captured workspace, not the
process's ambient current directory. Duplicate or overlapping entries are
rejected or deterministically coalesced before authorization.

The result declares one of:

- `AtomicCommitted`, when no observer can see a subset and rollback is proven;
- `CommittedWithNonAtomicVisibility`, when all effects completed but the host
  could expose intermediates;
- `Partial`, with exact completed, unchanged, and uncertain entries; or
- `RejectedBeforeEffect`.

Sequential application without rollback MUST NOT report transaction atomicity. A
failure after three files were written returns those three facts and never a
single generic “patch failed” result.

## Formatting and code actions

Formatters and language-server code actions are separate process or protocol
effects with captured identity, version, configuration, timeout, diagnostics,
and output. A formatter exit code, malformed output, timeout, or changed file
set is observed—not discarded while the harness returns `true`.

Post-write formatting is permitted only as a second explicitly authorized
mutation. Prefer previewing the formatter against staged content so the final
authorized bytes can commit once.

## Git and external effects

Editing files does not imply permission to stage, commit, reset, clean, update a
submodule, or run Git hooks. Those are separate process/file mutations. The
result distinguishes workspace bytes from any VCS projection and records which
external effects cannot be rolled back.

## Acceptance scenarios

- A formatter that changes previewed output after authorization forces a new
  decision and no write occurs under the old grant.
- A patch move is denied when destination permission is absent.
- Multiple fuzzy matches fail before effects and identify candidate locations.
- A concurrent writer changes the file hash after locking and the mutation
  returns a precondition conflict.
- CRLF, BOM, non-BMP Unicode, a missing final newline, and invalid UTF-8 follow
  their declared policies byte-for-byte.
- Failure in a non-atomic three-file patch reports the exact completed prefix.
- Watchers and language services observe only the settled mutation outcome.
- Denial at the low-level adapter occurs before temporary or backup files are
  created.
- Cancellation racing atomic replacement reports the actual resulting file
  version rather than pretending the write did not happen.

## Authorization-ordering pitfalls

Authorization binds the final mutation, never an early diff. A formatter that
runs after a permission decision forces re-evaluation before writing. A
write-then-format pipeline cannot claim one atomic authorized mutation unless
the contract and adapter actually provide it. Sequential patch application
reports the exact completed prefix, move authorization binds both source and
destination, and formatter success depends on the observed process outcome
rather than the absence of an exception.

## Related specifications

- [Language services, formatters, and watchers](language-services-formatters-and-watchers.md)
- [Workspace snapshots and reversion](workspace-snapshots-and-reversion.md)
- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Artifact and content storage](artifact-and-content-storage.md)
