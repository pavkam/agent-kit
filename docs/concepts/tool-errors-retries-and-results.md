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

## Result bounds and normalization

Tool output MUST be bounded before it enters history or telemetry. The
normalizer MAY truncate, summarize, store externally with an authorized
reference, or reject according to tool policy. It MUST mark transformations and
retain safe provenance.

Result content MAY include text, structured data, images, audio, files, and
resource references. Unsupported media MUST not be silently stringified.

`isError` or terminal status is authoritative. Human-readable text MUST NOT be
parsed to decide success. Tool-specific details and usage remain typed extension
data.

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

## Acceptance scenarios

- Truncated tool arguments never execute.
- A mutating timeout with unknown side-effect status is not retried blindly.
- Retry backoff is deterministic under fake time and randomness.
- Raw exceptions and secret arguments never reach model-visible results.
- Oversized output follows configured normalization with a visible marker.
- Every sibling call receives a terminal result after fail-fast cancellation.

## Upstream evidence

- Pi rejects tool calls from length-truncated assistant output and normalizes
  hook replacements in
  [`agent-loop.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent-loop.ts).
- Pydantic AI distinguishes retry prompts, terminal tool failures, timeouts, and
  unexpected exceptions in its tools documentation at
  [function tools](https://ai.pydantic.dev/tools/).
- OpenCode's legacy processor persists pending/running/completed/error tool
  states and repairs unsettled calls in
  [`processor.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/opencode/src/session/processor.ts).

## Related specifications

- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Observability and audit](observability-and-audit.md)
