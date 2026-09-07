# Context compaction

**Status:** Normative

**Architecture:** [Context compaction](../architecture/context-compaction.md)

**Depends on:** [Sessions](sessions-persistence-and-branching.md),
[context assembly](context-assembly-and-instructions.md)

The implementation boundary is defined by the
[context compaction architecture](../architecture/context-compaction.md).

## Purpose

Compaction creates a bounded semantic representation of older context while
preserving [durable session history](sessions-persistence-and-branching.md). It
is a checkpointing policy, not message deletion.

## Trigger policy

Compaction MAY be triggered by estimated context pressure, a provider overflow,
an explicit request, an epoch replacement, or a maintenance policy. Triggering
MUST be based on observable thresholds and emit a reason.

The runtime SHOULD compact before a provider request when estimated mandatory
output reserve cannot be met. An overflow retry MAY compact after a provider
rejects the request, but the original failure and retry decision remain
observable.

The profile defines the comparison operator, reserve, retained-tail target,
usage-vs-estimate precedence, and modality estimates. Reported nonzero final
usage is preferable to estimation; missing or invalid usage may fall back to a
versioned estimator. Usage before the newest active compaction boundary is not
reapplied to the compacted request or it can trigger immediate recompaction.

## Semantic cut point

The compactor MUST choose a cut at a complete semantic boundary, preferably
before a user turn. It MUST preserve the
[history invariants](history-validation-and-repair.md): it cannot split a tool
call from its result, an assistant content block, a deferred request from its
resolution, or an admitted input from its promotion.

When one oversized turn cannot fit, the compactor MAY cut before a complete
assistant message or summarize a prefix within that turn only if it creates an
explicit turn-prefix summary and preserves all tool causality needed by the
retained suffix. It never begins the retained suffix at a tool result, and
metadata that semantically qualifies a retained entry stays adjacent to it.

## Compaction record

A durable record MUST include:

- compaction ID and source session/branch;
- covered start and end sequences;
- retained suffix start sequence;
- summary content and structured state;
- source manifest or content hashes;
- model/algorithm and settings used;
- context epoch and effective instructions;
- relevant resource, goal, and tool-side-effect state that future turns need;
- token estimates before and after; and
- status, failure, and supersession metadata.

The active branch uses the latest applicable successful compaction plus all
later entries. Original covered entries remain available for audit and
recompaction.

The checkpoint MUST be self-contained for ordinary context reconstruction: the
summary plus its complete retained tail and all later entries are sufficient.
The assembler does not need to scan through the checkpoint into covered history.
This is a request-read optimization and recovery invariant, not permission to
delete the covered entries.

## Summary requirements

The summary SHOULD preserve:

- user objectives, constraints, preferences, and unresolved questions;
- verified facts with provenance and uncertainty;
- decisions and rejected alternatives with reasons;
- tool effects, affected resources, and pending/deferred calls;
- goals, task ownership, and progress;
- important identifiers without secrets; and
- explicit instructions for interpreting the retained suffix.

Generated summaries remain untrusted model output after structural validation.
Authoritative checkpoint fields MUST derive from identified committed records,
never generated prose. Validation checks those fields, provenance, causality,
and bounds; it does not prove arbitrary prose factually correct. Summary text
MUST NOT create approvals, establish unrecorded tool success, or elevate
retrieved content into instructions.

Summary generation is its own provider operation with stable identity, usage,
budget, cache/session-affinity policy, capability checks, retry accounting, and
terminal validation. An error, output-length stop, attempted tool call, invalid
manifest, or non-reducing result cannot activate. External resource state comes
from committed tool outcomes and audit evidence, never merely from an
assistant's intended tool calls.

## Concurrency and atomicity

Compaction reads a stable branch/version. Publishing succeeds only if its source
range is still applicable, or it is appended as a historical candidate without
becoming active. A concurrent append MUST NOT be lost.

Only the record activation needs to be atomic; potentially expensive summary
generation SHOULD occur outside the session append lock.

Manual compaction is a structural lane operation with its own operation
identity, cancellation, usage, and terminal result. Preparation captures the
source tip and settings outside the mutation line; activation rechecks both. A
concurrent append either precedes a newly prepared compaction or causes the
stale preparation to be discarded—never silently omitted.

The caller passes the immutable session execution capability selected for the
agent's run as a separate invocation-only value. Source loading and activation
use that exact keyed coordinator; the compactor MUST NOT inject an unkeyed
session coordinator, select a store, or consult an ambient current session. The
capability and security grants are never persisted in compaction records,
manifests, events, or caches.

The caller also passes the exact invocation-owned budget capability. The
compactor validates its profile version, identity, correlation, and scope and
propagates it through a selected strategy to any model-backed summary generator.
Every attempt reserves and settles its own expected/actual work; compaction
never captures a bare run budget or treats an out-of-run operation as a run.

## Failure and retry

Failure before activation leaves the prior context path active. Failure or
cancellation after activation preserves the committed record and reports its
commit state; unknown activation is reconciled before retry. The runtime MAY
retry within a dedicated budget. It MUST prevent an infinite
overflow/compact/retry loop by recording attempts and requiring measurable
reduction.

If compaction cannot reduce mandatory context below the model limit, the run
ends with `ContextLimitExceeded`, not a generic provider error.

Overflow recovery and ordinary transport/rate-limit retry use separate budgets
and attempt records. The failed assistant attempt remains auditable but is
excluded from the retry request context. A profile states whether manual
compaction durably aborts the current operation or can suspend/resume it; it
never resumes implicitly. Extension-supplied summaries and cut identities pass
the same source-bound validation as built-in output.

## Acceptance scenarios

- A chosen cut never separates a tool call and terminal result.
- Concurrent appends survive publication or cause an activation conflict.
- Replaying with the active compaction produces the summary plus exact suffix.
- A summary cannot forge permission or tool outcome state.
- Repeated non-reducing compaction stops under a typed limit.
- The original covered history remains queryable and branchable.
- Context reconstruction stops at the newest checkpoint and reproduces exactly
  its summary, retained tail, and later suffix.
- A stale manual-compaction preparation cannot publish over a newer branch tip.
- A failed effect intent cannot appear as completed resource state in a summary.
- A length-truncated or tool-calling summary is rejected without changing the
  active context path.

## Related specifications

- [History validation and repair](history-validation-and-repair.md)
- [Usage limits and budgets](usage-limits-and-budgets.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
