# Tool-call lifecycle

**Status:** Normative

**Architecture:** [Tools](../architecture/tools.md)

**Depends on:** [Tools and toolsets](tools-and-toolsets.md),
[permissions](permissions-approvals-and-trust.md)

## Pipeline

The [tool catalog and descriptor contracts](tools-and-toolsets.md) supply the
resolved operation, the [security authority](permissions-approvals-and-trust.md)
grants or denies its effect, and
[durable recovery](durable-execution-and-recovery.md) relies on the
record-before-invocation boundary.

Every application tool call MUST pass through this pipeline:

```text
discover -> resolve snapshot -> bound -> parse -> validate
  -> authorize -> approve/defer -> record call -> invoke
  -> normalize -> record terminal result
  -> project bounded result -> materialize in source order -> return to loop
```

The model requests a call; it never directly invokes application code. Any stage
may produce the corresponding pre-invocation terminal rejection without falling
through to later effect stages.

## States

```text
Observed
  -> Parsing -> Validating -> Authorizing
  -> AwaitingApproval | Deferred | Ready
  -> Planned -> EffectPending -> OutcomeReady -> Completed
```

`OutcomeReady` is terminal for effect execution: one complete result is durably
staged and the tool MUST never run again. `Completed` means that result has been
projected and placed into history in assistant source order. Every bounded call
with a stable `ToolCallId` MUST reach exactly one terminal result and one
message projection, including unknown tools, invalid arguments, and denials.
Calls admitted to invocation additionally have one accepted record before their
effect. Raw input rejected before a stable call identity can be assigned is a
provider/request protocol failure, not an anonymous tool success or invocation.

## Resolution snapshot

A call resolves against the exact tool-catalog snapshot included in the model
request. The runtime MUST verify provider-visible alias, stable tool identity,
and version. A tool removed after request dispatch does not silently resolve to
a different implementation.

The resolver receives the retained catalog capture, not only its serialized
snapshot. It validates run, identity, definition, configuration, and catalog
evidence before acquiring a binding. The alias maps only through that snapshot;
unknown or ambiguous aliases reject before invoker acquisition. Success
transfers an owned lease for the exact descriptor and source version to the
executor, which retains it through settlement. Resolution and validation retain
the requested alias and catalog version unchanged. No stage silently looks up a
current provider or service registration.

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

The security authority evaluates normalized arguments plus principal, agent,
session/run/call, tool identity/source/version, resource/effect scope, and
relevant host context. Allow produces a bounded grant; deny produces a typed
result or halts according to policy; require-approval follows the approval
contract.

If approval changes or supplies arguments, validation and authorization MUST run
again. Approval and its grant bind to the final fingerprint. The effecting
file-system, network, or process component validates its derived grant again.

## Record before side effects

The accepted call MUST be durably recorded before invocation. Its record
includes call ID, tool snapshot identity, normalized argument fingerprint,
security decision and grant reference, source part identity/position, scheduling
ordinal, reserved result identity, replay classification, attempt, idempotency
information, and start metadata with secrets redacted.

`SourcePartId` or source position refers to the call's position in the complete
assistant content sequence, including non-tool parts. `SchedulingOrdinal` orders
only accepted calls. The reserved result identity may also scope the framework
`ToolInvocationId` and its memos, but neither replaces the provider call ID.

If the record cannot be committed, invocation MUST NOT occur. This boundary
enables recovery to distinguish “never started” from “side effect may have
occurred.”

The executor receives invocation-only session and budget capabilities compiled
into the run plan. Accepted and terminal records use that exact versioned
session profile/coordinator, while every attempted invocation reserves and
settles against the exact budget profile/scope. Recorders and executors MUST NOT
inject an unkeyed session coordinator, rediscover a store, or capture a mutable
run budget in singleton state.

## Invocation context

The invoker receives a restricted immutable context containing the final
arguments, run/call/principal IDs, approved resource scope, deadline,
cancellation token, attempt, safe dependencies, and a progress reporter.

It MUST NOT receive loop mutation methods, the service provider, raw credentials
unrelated to the tool, or permission to append arbitrary messages.

Progress is live-only by default. Semantic checkpoints MAY be durable and MUST
be bounded complete snapshots with declared cadence and replacement behavior. A
checkpoint never proves completion. Progress after outcome staging is fenced,
ignored, and diagnosed.

The public progress view labels the latest live update separately from the
latest committed checkpoint. Ordered assistant frames and replacement tool
snapshots are different durability shapes; a tool update is persisted only when
the tool requests a checkpoint under the configured cadence.

An invocation MAY expose a narrow durable memo API for idempotent substeps. Memo
names and values are bounded, invocation-scoped, written before returning to the
tool, and deleted when the outcome becomes ready. Memos do not authorize new
effects or make the enclosing external call exactly once.

A before-tool transform is followed by schema validation, canonicalization,
fingerprinting, and authorization of the transformed call. Replacing arguments
after approval invalidates the prior grant; no hook can smuggle a different
effect through an already authorized fingerprint.

## Terminal result

Terminal status, retryability, and side-effect certainty follow the
[tool error and result contract](tool-errors-retries-and-results.md).

An invoker returns owned raw `ToolInvocationResult` evidence. The executor owns
normalization under captured policy and constructs `ToolCallResult` using the
retained admission and acceptance evidence. A raw success does not prove
successful normalization or terminal recording. Normalization or recording
failure must not trigger another invocation to reconstruct missing evidence.

The result MUST contain call ID, requested alias, resolved tool/version when
available, terminal status, bounded typed content, safe structured data,
normalized error when applicable, timestamps, usage or cost, retryability,
side-effect certainty, the captured result-projection policy reference, and
extension data.

Recording the complete staged outcome and transition to `OutcomeReady` SHOULD be
atomic. Source-order materialization then places the result and advances to
`Completed` without invocation. When atomic staging is unavailable, the
operation MUST use an external idempotency/status mechanism so recovery can
safely finish the record without invoking again.

History materialization is a separate deterministic projection from the
authoritative recorded result. It preserves terminal meaning and uncertainty,
uses the exact captured projection-policy version, and records every loss. A
projection or append retry never returns to invocation.

## Acceptance scenarios

- Denied, invalid, and unknown calls produce no invocation side effect.
- Denied, invalid, and unknown identified calls still receive one terminal
  record and one correlated history projection.
- The call record is observable before the invoker starts.
- A late progress update cannot mutate a terminal result.
- Result commit retry does not invoke the tool twice.
- A later parallel call may become outcome-ready first but cannot enter history
  before an earlier source ordinal.
- A durable progress checkpoint that looks successful still recovers as unknown
  when the effect never settled.
- Catalog changes after model dispatch cannot redirect a call.
- Unknown aliases and mismatched catalog/run evidence reject before invoker
  acquisition; a failed acquisition cannot leak a lease.
- A raw invocation success followed by normalization failure produces the typed
  normalization failure with the original effect certainty and no repeated
  effect.
- Every identified call has exactly one correlated terminal result and
  projection after settlement or repair; an accepted call has no second
  invocation during publication recovery.

## Related specifications

- [Tool scheduling and concurrency](tool-scheduling-and-concurrency.md)
- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Observability and audit](observability-and-audit.md)
