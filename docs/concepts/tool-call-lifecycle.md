# Tool-call lifecycle

**Status:** Normative  
**Depends on:** [Tools and toolsets](tools-and-toolsets.md),
[permissions](permissions-approvals-and-trust.md)

## Pipeline

Every application tool call MUST pass through this pipeline:

```text
discover -> resolve snapshot -> bound -> parse -> validate
  -> authorize -> approve/defer -> record call -> invoke
  -> normalize -> record terminal result -> return to loop
```

The model requests a call; it never directly invokes application code.

## States

```text
Observed
  -> Parsing -> Validating -> Authorizing
  -> AwaitingApproval | Deferred | Ready
  -> Recorded -> Running
  -> Succeeded | Failed | Denied | Cancelled | TimedOut | Interrupted
```

Terminal states are immutable. There MUST be exactly one terminal result for
every accepted recorded call. Invalid raw calls rejected before acceptance MAY
produce a model-visible validation result but MUST still have stable diagnostic
correlation.

## Resolution snapshot

A call resolves against the exact tool-catalog snapshot included in the model
request. The runtime MUST verify provider-visible alias, stable tool identity,
and version. A tool removed after request dispatch does not silently resolve to
a different implementation.

Unknown or ambiguous tools fail closed before side effects.

## Bounding, parsing, and validation

Raw arguments MUST be bounded in bytes, depth, member count, string size, and
numeric representation before general-purpose deserialization. Parsing yields a
JSON-compatible immutable value. Canonical schema validation and optional typed
validation follow.

Invalid arguments normally produce a model-visible retry result within the
tool's retry budget. They MUST NOT reach authorization or invocation. Raw
secret-bearing arguments MUST not be copied to logs or exceptions.

## Authorization and approval

Policy evaluates normalized arguments plus principal, agent, session/run/call,
tool identity/source/version, resource/effect scope, and relevant host context.
An allow result can proceed; deny produces a typed result or halts according to
policy; require-approval follows the approval contract.

If approval changes or supplies arguments, validation and authorization MUST run
again. Approval binds to the final fingerprint.

## Record before side effects

The accepted call MUST be durably recorded before invocation. Its record
includes call ID, tool snapshot identity, normalized argument fingerprint,
permission decision reference, attempt, idempotency information, and start
metadata with secrets redacted.

If the record cannot be committed, invocation MUST NOT occur. This boundary
enables recovery to distinguish “never started” from “side effect may have
occurred.”

## Invocation context

The invoker receives a restricted immutable context containing the final
arguments, run/call/principal IDs, approved resource scope, deadline,
cancellation token, attempt, safe dependencies, and a progress reporter.

It MUST NOT receive loop mutation methods, the service provider, raw credentials
unrelated to the tool, or permission to append arbitrary messages.

Progress is live-only by default. Semantic checkpoints MAY be durable and MUST
be bounded. Progress after terminal settlement is ignored and diagnosed.

## Terminal result

The result MUST contain call/tool IDs, terminal status, bounded typed content,
safe structured data, normalized error when applicable, timestamps, usage or
cost, retryability, side-effect certainty, and extension data.

Recording the terminal result and durable completion event SHOULD be atomic.
When that is unavailable, the operation MUST use an idempotency key so recovery
can safely complete the record without invoking again.

## Acceptance scenarios

- Denied, invalid, and unknown calls produce no invocation side effect.
- The call record is observable before the invoker starts.
- A late progress update cannot mutate a terminal result.
- Result commit retry does not invoke the tool twice.
- Catalog changes after model dispatch cannot redirect a call.
- Every accepted call has exactly one correlated terminal result after
  settlement or repair.

## Upstream evidence

- OpenCode V2 records tool calls before forking execution in
  [`llm.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/runner/llm.ts).
- Pydantic AI's validation, approval, execution, and event behavior is
  implemented across its tool manager and
  [`messages.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/messages.py).
- Pi exposes prepare, before, after, update, and terminal tool events in
  [`agent-loop.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent-loop.ts).

## Related specifications

- [Tool scheduling and concurrency](tool-scheduling-and-concurrency.md)
- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Observability and audit](observability-and-audit.md)
