# Context compaction

**Status:** Normative  
**Depends on:** [Sessions](sessions-persistence-and-branching.md),
[context assembly](context-assembly-and-instructions.md)

## Purpose

Compaction creates a bounded semantic representation of older context while
preserving durable history. It is a checkpointing policy, not message deletion.

## Trigger policy

Compaction MAY be triggered by estimated context pressure, a provider overflow,
an explicit request, an epoch replacement, or a maintenance policy. Triggering
MUST be based on observable thresholds and emit a reason.

The runtime SHOULD compact before a provider request when estimated mandatory
output reserve cannot be met. An overflow retry MAY compact after a provider
rejects the request, but the original failure and retry decision remain
observable.

## Semantic cut point

The compactor MUST choose a cut at a complete semantic boundary, preferably
before a user turn. It MUST NOT split a tool call from its result, an assistant
content block, a deferred request from its resolution, or an admitted input from
its promotion.

When one oversized turn cannot fit, the compactor MAY summarize a prefix within
that turn only if it creates explicit repair markers and preserves all tool
causality needed by the retained suffix.

## Compaction record

A durable record MUST include:

- compaction ID and source session/branch;
- covered start and end sequences;
- retained suffix start sequence;
- summary content and structured state;
- source manifest or content hashes;
- model/algorithm and settings used;
- context epoch and effective instructions;
- file, resource, goal, and tool-side-effect state that future turns need;
- token estimates before and after; and
- status, failure, and supersession metadata.

The active branch uses the latest applicable successful compaction plus all
later entries. Original covered entries remain available for audit and
recompaction.

## Summary requirements

The summary SHOULD preserve:

- user objectives, constraints, preferences, and unresolved questions;
- verified facts with provenance and uncertainty;
- decisions and rejected alternatives with reasons;
- tool effects, affected resources, and pending/deferred calls;
- goals, task ownership, and progress;
- important identifiers without secrets; and
- explicit instructions for interpreting the retained suffix.

Generated summaries are untrusted model output until structurally validated.
They MUST NOT create new approvals, claim unrecorded tool success, or elevate
retrieved text into instructions.

## Concurrency and atomicity

Compaction reads a stable branch/version. Publishing succeeds only if its source
range is still applicable, or it is appended as a historical candidate without
becoming active. A concurrent append MUST NOT be lost.

Only the record activation needs to be atomic; potentially expensive summary
generation SHOULD occur outside the session append lock.

## Failure and retry

Compaction failure leaves the prior context path active. The runtime MAY retry
within a dedicated budget. It MUST prevent an infinite overflow/compact/retry
loop by recording attempts and requiring measurable reduction.

If compaction cannot reduce mandatory context below the model limit, the run
ends with `ContextLimitExceeded`, not a generic provider error.

## Acceptance scenarios

- A chosen cut never separates a tool call and terminal result.
- Concurrent appends survive publication or cause an activation conflict.
- Replaying with the active compaction produces the summary plus exact suffix.
- A summary cannot forge permission or tool outcome state.
- Repeated non-reducing compaction stops under a typed limit.
- The original covered history remains queryable and branchable.

## Upstream evidence

- Pi's semantic turn selection, recent suffix, summaries, and oversized-turn
  handling are in
  [`compaction.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core/compaction/compaction.ts)
  and
  [`utils.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core/compaction/utils.ts).
- OpenCode V2 represents compaction as a message/event boundary and loads
  post-compaction history in its session runner at
  [`llm.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/runner/llm.ts).
- Pydantic AI supplies compaction as a composable capability, documented in
  [compaction](https://ai.pydantic.dev/capabilities/compaction/).

## Related specifications

- [History validation and repair](history-validation-and-repair.md)
- [Usage limits and budgets](usage-limits-and-budgets.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
