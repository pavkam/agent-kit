# Tool errors, retries, and results

**Status:** Normative  
**Depends on:** [Tool-call lifecycle](tool-call-lifecycle.md),
[error taxonomy](error-taxonomy.md)

## Error classes

Tool processing MUST distinguish:

| Class                                 | Model-visible by default | Retry budget   | Run effect          |
| ------------------------------------- | ------------------------ | -------------- | ------------------- |
| Unknown tool                          | Yes                      | Tool/input     | Continue if allowed |
| Malformed/invalid arguments           | Yes                      | Tool/input     | Continue if allowed |
| Permission denied                     | Policy choice            | None           | Continue or halt    |
| Approval required                     | Deferred event           | None           | Suspend or await    |
| Tool-declared recoverable failure     | Yes                      | Tool           | Continue/retry      |
| Tool-declared terminal failure        | Yes                      | None           | Continue if useful  |
| Timeout                               | Yes                      | Tool if safe   | Continue/retry      |
| Cancellation                          | No fabricated error      | None           | Cancel/interrupt    |
| Unexpected implementation exception   | No raw details           | Runtime policy | Fail run by default |
| Result serialization/protocol failure | Safe typed error         | Runtime policy | Fail or repair      |

Exceptions MUST NOT be copied raw into model context. Stable safe error content
is separate from diagnostic exception details.

## Retry ownership and precedence

Retry configuration SHOULD resolve from most specific to least specific:

1. explicit call/tool policy;
2. toolset policy;
3. run override;
4. agent definition; and
5. library default.

Tool retries and structured-output validation retries MUST use separate budgets.
Attempts include the initial attempt or retries consistently; the public name
and documentation MUST remove ambiguity.

## Retry safety

A retry requires both a retryable failure and safe execution semantics. Under
the [resilience contract](cancellation-timeouts-and-resilience.md), mutating
tools MUST NOT retry unless they declare an idempotency mechanism or the invoker
proves the prior attempt did not start. “Transient exception” alone is
insufficient.

The retry decision MUST consider tool effect, idempotency key, side-effect
certainty, deadline, remaining budgets, approval scope, and normalized error.
Approval MUST be rechecked if arguments, resource scope, tool version, or
principal change.

Backoff uses `TimeProvider` and injectable randomness, honors server/tool retry
hints within configured bounds, and remains cancellable.

## Model-requested correction

Malformed arguments, schema validation failures, and explicit model-retry
signals SHOULD return a concise tool result explaining how to correct the
request. The response MUST identify the original call ID and avoid exposing
secrets or host internals.

When the provider ends for output length while emitting tool calls, the runtime
MUST NOT execute them because their arguments may be truncated. It produces
interrupted/invalid terminal results for all such calls or retries the model
request according to policy.

## Authoritative terminal record and history projection

`ToolCallResult` is the complete authoritative terminal record. It retains the
exact terminal status, call and requested alias, resolved tool/version identity
when available, authorization and grant correlation, bounded normalized content,
safe error, side-effect certainty, usage, retry decision, timestamps, and
extension evidence. The runtime MUST commit that full value through
`IToolCallRecorder` before adding a tool result to message history. A failure to
materialize history retries from the recorded result and MUST NOT invoke the
tool again.

The accepted call and terminal record retain the captured projection-policy
identity, version, and bounds. Recovery MUST use that captured policy or return
a typed unavailable/publication failure; it MUST NOT reproject under whatever
policy happens to be current after a restart.

The terminal result and `ToolResultPart` MUST carry the same durable projection
policy key/version reference. The referenced immutable snapshot owns the exact
bounds and transformation rules and remains available for the retention period
of any call that can require history repair.

`ToolResultPart` is a separate bounded durable projection for history and model
context. It is not the authoritative record and MUST NOT be used to reconstruct
one. Its outcome retains the source `ToolTerminalStatus`, side-effect certainty,
and retryability alongside the coarser portable `ToolCallOutcomeKind`. Its
projection remains bound by call and requested alias, plus exact tool/version
when resolution succeeded, to the one terminal record. It records every
redaction, normalization, summary, truncation, omitted part/byte count, and
authorized artifact or continuation reference.

Projection policy MAY be tighter than terminal-record policy. It may omit
diagnostic detail and usage that the model does not need, but it MUST preserve
call identity, requested alias, resolved tool/version when available, terminal
meaning, uncertainty, and enough safe correction detail for the selected loop
policy. Projection failure is a result-publication failure, not evidence that
the invocation failed or should be retried.

## Loss-aware status mapping

The portable message outcome is derived from the authoritative status by a
closed, tested mapping:

| Authoritative terminal-status class                                                                     | Portable outcome | Required retained evidence                                       |
| ------------------------------------------------------------------------------------------------------- | ---------------- | ---------------------------------------------------------------- |
| Successful invocation and result normalization                                                          | `Success`        | Exact success status and content provenance                      |
| Unknown tool, invalid arguments, denial, unsupported call, or approval denied/expired before invocation | `Rejected`       | Exact rejection status and safe correction or policy reason      |
| Invocation, timeout, result-normalization, serialization, or protocol failure                           | `Failed`         | Exact failure status, retryability, and side-effect certainty    |
| Cancellation or interruption                                                                            | `Cancelled`      | Exact cancellation/interruption status and side-effect certainty |
| Unknown future or unrepresentable status                                                                | `Failed`         | Original status value plus an explicit unknown-mapping marker    |

`ApprovalRequired` or durable deferral is not terminal by itself. It suspends or
defers the call; only its eventual denial, expiry, cancellation, failure, or
successful invocation is projected as a terminal outcome.

A coarse outcome MUST never erase whether invocation began or whether an effect
may have occurred. In particular, cancellation with unknown effect remains
`Cancelled` with `SideEffectCertainty.Unknown`; it is never represented as a
clean pre-invocation rejection. Unknown statuses fail toward `Failed`, never
`Success`. Human-readable content, `isError`, or absence of an error string is
not parsed to determine any of these values.

When a provider protocol lacks a separate status field, the adapter MUST encode
a deterministic bounded status envelope in the model-visible tool result or
reject the mapping. It may not drop the status, infer it from text, or turn a
failed/uncertain result into ordinary successful-looking content.

## Result bounds and normalization

Both the authoritative result and its model/history projection are bounded, with
independently configured limits. The normalizer MAY truncate, summarize, store
externally with an authorized reference, or reject according to tool policy. It
MUST mark transformations and retain safe provenance. Replacing content with a
reference never changes terminal status.

Result content MAY include text, structured data, images, audio, files, and
resource references. Unsupported media MUST not be silently stringified.
Tool-specific details and usage remain typed fields or typed extension data in
the authoritative record; only explicitly selected safe fields enter the
projection.

## Hook overrides

An [after-invocation hook](extensions-hooks-and-middleware.md) MAY replace
content, details, status, usage, or termination advice only through a validated
typed result. Merge behavior MUST be fieldwise and explicit; there is no
implicit deep merge of nested untrusted data. Hooks cannot change call/tool
identity or turn a denied, unexecuted call into successful execution.

## Batch behavior

Under the [batch scheduling contract](tool-scheduling-and-concurrency.md),
failure of one parallel tool does not erase sibling results. Fail-fast policy
MAY cancel siblings but must still terminally settle every accepted call. A
batch MAY request loop termination only through an explicit aggregate policy;
one tool's arbitrary output flag must not silently suppress results.

The default coding-harness policy accepts early termination only when every
terminal result in the accepted batch explicitly requests it. Cancellation
removes termination advice. A profile choosing `Any`, priority, or another rule
names and tests it before effects start.

## Acceptance scenarios

- Truncated tool arguments never execute.
- A mutating timeout with unknown side-effect status is not retried blindly.
- Retry backoff is deterministic under fake time and randomness.
- Raw exceptions and secret arguments never reach model-visible results.
- Oversized output follows configured normalization with a visible marker.
- Every sibling call receives a terminal result after fail-fast cancellation.
- One terminating result cannot suppress non-terminating sibling results under
  the default all-results aggregate policy.
- A history-append retry reprojects the recorded result without invoking the
  tool again.
- An unavailable captured projection-policy version fails publication instead of
  silently using the current version.
- An unknown requested alias reaches one rejected terminal result and projection
  without fabricating a resolved `ToolId` or `ToolVersion`.
- A timeout with unknown side effects projects as failed with uncertainty, not
  as success or a clean pre-invocation rejection.
- A provider without native tool status receives an explicit bounded status
  envelope or rejects the mapping.

## Related specifications

- [Message and content model](message-and-content-model.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Observability and audit](observability-and-audit.md)
