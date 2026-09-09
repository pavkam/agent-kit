# Tool errors, retries, and results

**Status:** Normative

**Architecture:** [Tools](../architecture/tools.md)

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

A call has three durable stages with distinct authority:

1. `AcceptedToolCall` records a successfully resolved, semantically validated,
   authorized invocation: requested alias, exact tool/version, declared effects,
   retry-mechanism facts, raw admission evidence, acceptance evidence (including
   the historical invocation grant ID), and selected normalization/projection
   snapshots. The recorder commits it **before** an invoker may start an effect.
2. `ToolCallResult` records exactly one terminal outcome for every bounded call,
   including pre-invocation rejection. It is authoritative execution evidence
   and is committed before publication.
3. `ToolResultPart` is a bounded history/model projection of the terminal
   record. A publication retry starts from the recorded result and cannot invoke
   the tool again.

Every accepted or terminal record preserves the requested alias. A terminal
record for an unresolved alias has both `ToolId` and `ToolVersion` absent; a
resolved terminal record and every accepted record have both present.
`ToolEffects` and an external idempotency key are likewise absent when no
descriptor resolved; no value invents a read-only or mutating effect for an
unknown alias. No provider, recorder, or projection fabricates a canonical ID
from an alias or provider name.

`ToolCallAdmissionEvidence` records the bounded raw-argument fingerprint,
catalog version, and source ordinal for every terminal path. Its fingerprint is
raw admission evidence, not proof that arguments were semantically valid.
`ToolCallAcceptanceEvidence` exists only after semantic validation, planning,
and authorization produce an accepted invocation. It retains the historical
invocation grant ID, validated-argument fingerprint, and acceptance timestamp,
not a reusable grant. `ToolCallResult.GrantId` independently preserves a grant
that may have been issued even when acceptance recording failed; when acceptance
evidence exists, the two grant IDs must match. A terminal result carries
acceptance evidence only when an invocation was accepted. The fingerprint
denotes accepted-invocation evidence in this contract, not every successful
standalone validator call. Malformed, truncated, unknown, ambiguous,
denied-before-acceptance, and unsupported calls retain admission evidence
without being represented as accepted invocations.

### Terminal status and error

`ToolTerminalStatus` has an integer wire representation. The named base set is:

| Status                                                                     | Meaning                                                            |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| `Succeeded`                                                                | Invocation and result normalization completed.                     |
| `UnknownTool`, `InvalidArguments`, `Unsupported`                           | Rejected before invocation.                                        |
| `Denied`, `ApprovalDenied`, `ApprovalExpired`                              | Authorization/approval did not permit invocation.                  |
| `InvocationFailed`, `TimedOut`                                             | Invocation reached a terminal failure.                             |
| `Cancelled`, `Interrupted`                                                 | Invocation or its surrounding operation was cancelled/interrupted. |
| `ResultNormalizationFailed`, `ResultSerializationFailed`, `ProtocolFailed` | A result could not be normalized, represented, or mapped safely.   |

The value is not a closed CLR enum validation boundary. A reader retains an
unknown numeric status exactly in `ToolCallResult`; it neither activates an
unknown implementation nor guesses success. When a portable projection cannot
represent that status, it emits `ToolCallOutcomeKind.Failed`, retains the
numeric source status, and includes `StatusCoarsened` in
`ToolResultProjectionInfo.Losses`.

`ToolError` is durable, bounded safe evidence:

```csharp
public sealed record ToolError(
    ToolErrorKind Kind,
    string SafeMessage,
    string? ExternalCode,
    TimeSpan? RetryAfter,
    ExtensionData Extensions);
```

`Kind` is a closed stable source classification: `Tool` (a safe tool-declared
failure), `Transport`, `Host`, `Policy`, `Serialization`, or `Protocol`. It is
not an exception type or a provider status-code registry, and it does not repeat
the terminal status. `SafeMessage` and `ExternalCode` are validated, bounded
fields selected for durable/model-safe use. `RetryAfter` is nonnegative when
present. Raw exception objects, stack traces, request/response bodies,
secret-bearing arguments, and unbounded provider diagnostics are excluded;
implementations may retain those only in classified, separately controlled
diagnostics.

A succeeded result has no `ToolError`. A failed/rejected result may omit one
when no safe explanation exists. Human-readable text and an `isError` convention
never determine terminal status.

### Closed terminal content family

`ToolResultContent` is a closed, discriminated family. Its first portable
variants have these shapes:

```csharp
public abstract record ToolResultContent;
public sealed record ToolResultTextContent(
    string Text, TextSemantics Semantics, ExtensionData Extensions)
    : ToolResultContent;
public sealed record ToolResultStructuredContent(
    JsonElement Value, JsonSchemaReference? Schema, ExtensionData Extensions)
    : ToolResultContent;
public sealed record ToolResultMediaContent(MediaReference Reference, ExtensionData Extensions)
    : ToolResultContent;
public sealed record ToolResultArtifactContent(ArtifactReference Reference, ExtensionData Extensions)
    : ToolResultContent;
public sealed record ToolResultOpaqueContent(
    string TypeDiscriminator, ExtensionValue CanonicalPayload, ExtensionData Extensions)
    : ToolResultContent;
```

They carry, respectively, bounded text; structured JSON with an optional schema;
a media or artifact reference; and a bounded unknown type discriminator plus
canonical opaque bytes. Each variant carries `ExtensionData` for compatible
field additions.

Structured JSON is copied into an owned cloned DOM before the result is made
observable or durable; no variant may retain a caller-owned `JsonDocument` or
`JsonElement` buffer. Opaque content retains its unknown type discriminator and
canonical bytes without CLR type-name activation or semantic promotion. Media
and artifact variants retain their immutable references; they do not silently
inline bytes or stringify unsupported media. The selected normalizer owns the
canonical retained-content encoding used for byte accounting and portable
round-trip preservation.

### Usage evidence

`ToolCallResult.Usage` is optional. Its absence means no usage report was made,
never reported zero and never an implied budget settlement. A present
`ToolUsage` contains an immutable collection of unique `ToolUsageMeasurement`s,
keyed by exact `(BudgetDimension, BudgetUnit)`:

```csharp
public sealed record ToolUsageMeasurement(
    BudgetDimension Dimension,
    BudgetUnit Unit,
    BudgetQuantity? Amount,
    ToolUsageMeasurementQuality Quality);
```

`ToolUsageMeasurementQuality` is the closed set `Measured`, `Estimated`,
`Unknown`, and `NotApplicable`. `Measured` and `Estimated` require an exact
nonnegative `BudgetQuantity`; `Unknown` and `NotApplicable` require no amount.
The four qualities distinguish reported measurement from estimate, explicit
unknown, and an inapplicable dimension. A usage report has no authority to
reserve, settle, or reconcile a budget ledger. Budget settlement uses its own
recorded evidence.

### Retry and authorization evidence

A terminal result retains `ToolEffects` and an optional external
`IdempotencyKey` whenever a descriptor resolved, including a resolved rejection
that never became an accepted invocation. These are the declared replay facts,
not proof that a host executed an idempotent operation. The `ToolCallResult`
constructor validates only local pairing and presence invariants.
`IToolCallRecorder.RecordTerminalAsync` loads the accepted record when
acceptance evidence is present and validates exact terminal-to-accepted
identity, effect, idempotency-key, admission, acceptance/grant, and policy
evidence. The supplied `SecurityAuthorizationContext` is historical correlation
for the record's exact agent/session/run/turn/operation; neither constructor nor
recorder reauthorizes, revalidates a live grant, or infers an effect from
content.

Before retrying a potentially started mutating invocation, the executor must
validate that the selected invoker and effecting host will enforce the declared
idempotency mechanism and captured key where applicable. It may retry an attempt
known not to have started under the ordinary retry policy. A descriptor's
idempotency declaration alone never proves that this condition holds.

### Captured normalization and projection

`ToolResultNormalizationSnapshot` is an immutable captured value containing:

```csharp
public sealed record ToolResultNormalizationSnapshot(
    ToolResultRejectionPolicyReference RejectionPolicy,
    ToolResultProjectionPolicyReference ProjectionPolicy,
    ToolExecutionPolicyReference? ExecutionPolicy,
    ToolResultNormalizationAlgorithmVersion AlgorithmVersion,
    ToolResultBounds Bounds,
    ToolResultProjectionTransformations AllowedTransformations,
    ExtensionData Extensions);
```

`ToolResultBounds` sets a positive maximum content-part count and canonical
retained-content byte limit. A run captures the nondefault immutable
`ToolResultRejectionPolicyReference` before resolution; it supplies the
normalization bounds and captures the exact projection-policy reference/bounds
for unknown, ambiguous, invalid, and other pre-policy rejections.
`ExecutionPolicy` is null in those paths and otherwise is the selected per-tool
policy identity for its immutable normalization rules. This separates captured
run-level rejection handling from a current/default lookup and does not
introduce a second normalizer catalog. `AlgorithmVersion` identifies the
deterministic normalizer revision. The normalizer validates aggregate canonical
bytes before constructing a terminal record and returns a
`ToolResultNormalizationInfo`. Its actual transformations are a defined, unique,
ordered collection of `Redacted`, `Normalized`, `Summarized`, `Truncated`, or
`Externalized`; nullable input/omitted byte and part counts are present only
when measured, never replaced with zero when unknown. When both an omitted count
and its corresponding input count are measured, the omitted count cannot exceed
the input count. This is terminal-normalization evidence and is separate from
`ToolResultProjectionInfo.Losses`, which describes later history/model
projection loss. Value constructors validate required references, legal bounds,
flags, content shape, and nonnegative supplied counts, but do not falsely claim
independent aggregate-byte measurement without the canonical encoder.

`ToolResultProjectionPolicyReference` is captured inside normalization and
repeated on accepted/terminal records for direct recorder and message indexing;
all three values must match. It remains the policy for the tighter history/model
projection. Its version and the normalization snapshot remain resolvable for
every accepted record that can require recovery. Projection may redact,
normalize, summarize, truncate, or externalize only when allowed by its captured
rules, and records every loss. It preserves call identity, requested alias,
resolved identity when present, source status, certainty, retryability, and safe
correction evidence.

### Terminal invariants

A `ToolCallResult` locally requires a nonempty requested alias; paired resolved
ID/version; effects only with resolved identity; no invocation start without
acceptance evidence; and, for `Succeeded`, resolved identity, acceptance
evidence, invocation start, and no error. The recorder, which has both records,
checks those terminal-to-accepted facts when acceptance evidence is present:
identity, effects, idempotency key, admission, acceptance/grant evidence, and
captured-policy coherence. UTC timestamps are facts but their comparison does
not establish stage order because clocks can move backwards. A retained grant ID
is historical correlation, not proof a current grant remains valid.

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

The terminal normalizer enforces the accepted `ToolResultNormalizationSnapshot`
before a `ToolCallResult` is constructed. It uses canonical retained-content
encoding to account for the complete closed content family and extension
evidence, so a constructor does not pretend to have independently measured
aggregate bytes. It may apply only the captured allowed transformations and must
retain their provenance. Projection then applies its separate, potentially
tighter captured policy.

Unsupported media, unknown typed content, and future status values are retained
loss-aware or rejected with a typed terminal/projection failure; they are never
silently stringified, dropped, or promoted into a known semantic value.

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

The first-party `All` aggregate policy accepts early termination only when every
terminal result in the accepted batch explicitly requests it. Cancellation
removes termination advice. A configured `Any`, priority, or other policy names
and tests its behavior before effects start.

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
- Recorder failure after authorization prevents invocation because no accepted
  call exists; publication failure after terminal recording reprojects that
  terminal record without repeating the effect.
- An unknown or ambiguous call has raw admission evidence and the captured
  run-level rejection policy, but no accepted invocation, resolved effects, or
  validated-argument fingerprint. A malformed call rejected after resolution
  retains its resolved effects while still lacking acceptance evidence.
- An unknown numeric terminal status remains exact in durable evidence and
  projects as `Failed` with `StatusCoarsened`, never as success.
- A structured result remains readable after its source `JsonDocument` is
  disposed, and unknown opaque typed content round-trips its discriminator and
  canonical bytes without type activation.
- A normalizer rejects content over its canonical aggregate-byte bound before a
  terminal record is created; a record constructor does not pretend that an
  unrelated byte count proves this bound.
- Missing usage remains absent, while measured, estimated, unknown, and
  inapplicable dimension/unit measurements remain distinguishable and do not
  settle a budget reservation.
- A retry of a possibly-started mutating call proceeds only after the selected
  host confirms enforcement of the captured idempotency mechanism and key.

## Related specifications

- [Message and content model](message-and-content-model.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Observability and audit](observability-and-audit.md)
