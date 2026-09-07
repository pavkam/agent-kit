# Error taxonomy

**Status:** Normative  
**Depends on:** [Design principles](design-principles.md)

## Purpose

The [architecture coverage map](../architecture/index.md#concept-coverage) keeps
stable error values in AgentKit.Abstractions and assigns mapping to each effect
owner instead of creating a central dependency hub.

AgentKit errors are stable domain categories with structured context. Original
provider, transport, protocol, store, and implementation details remain
available for diagnostics without becoming the public control flow.

## Error shape

The canonical `AgentError` declaration lives in the
[foundation contracts](../architecture/foundation-contracts.md#stable-error-contract).
This specification defines its behavior; feature packages MUST reuse that
contract instead of declaring structurally similar error values.

The in-process result MAY retain an original `Exception` as non-serialized
diagnostic context. Exceptions, raw bodies, and stack traces MUST NOT appear in
model-visible content or public serialized errors by default.

## Stable categories

The taxonomy MUST include at least:

| Group              | Codes                                                                              |
| ------------------ | ---------------------------------------------------------------------------------- |
| Input/state        | InvalidInput, Conflict, InvalidState, SessionBusy, CorruptState                    |
| Configuration      | InvalidConfiguration, UntrustedConfiguration, MissingDependency                    |
| Capability         | UnsupportedCapability, IncompatibleModel, IncompatibleSchema                       |
| Authentication     | AuthenticationFailed, CredentialUnavailable                                        |
| Authorization      | AuthorizationDenied, ApprovalRequired, ApprovalExpired                             |
| Provider           | InvalidProviderRequest, RateLimited, ProviderUnavailable, ContentFiltered          |
| Transport/protocol | Timeout, ConnectionFailed, ProtocolViolation, TruncatedStream                      |
| Tool               | UnknownTool, InvalidToolArguments, ToolFailed, ToolInterrupted, ToolOutcomeUnknown |
| Output             | OutputValidationFailed, StructuredOutputMissing                                    |
| Limits             | RequestLimit, TokenLimit, CostLimit, ToolLimit, ContextLimit, QueueCapacity        |
| Persistence        | StoreUnavailable, ConcurrencyConflict, MigrationRequired                           |
| Recovery           | RecoveryIncompatible, LeaseLost, ReconciliationRequired                            |
| Runtime            | Cancelled, ObserverFailed, ExtensionFailed, InternalFailure                        |

The final enum MAY use hierarchical codes, but consumers must be able to branch
on the stable category without parsing messages or vendor codes.

## Retryability

`IsRetryable` is advice for the operation under the supplied context, not a
guarantee. The [resilience owner](cancellation-timeouts-and-resilience.md) still
checks idempotency, visible output, side-effect certainty, deadlines, limits,
and attempt policy.

Authentication, authorization, invalid request, unsupported capability, corrupt
state, and protocol violation are non-retryable by default. Rate limit,
unavailable, timeout, and connection failure MAY be retryable. Tool timeout is
not retryable when a non-idempotent effect may have occurred.

## Side-effect certainty

Every failure at an effect boundary SHOULD report one of:

- `NotStarted`;
- `Completed`;
- `PartiallyCompleted`;
- `Unknown`; or
- `NotApplicable`.

This value is independent of retryability. A response-parse failure can be
retryable at the provider level but still have consumed tokens; a tool timeout
can be transient yet unsafe to repeat.

## Mapping rules

Adapters MUST preserve external status, code, request ID, retry hints, and safe
response metadata. They MUST map from structured external values before falling
back to text. Unknown external failures map to the nearest stable origin plus
`Unknown`, never to success.

Cancellation mapping MUST inspect tokens and deadlines. A timeout does not
become caller cancellation simply because both use cancellation internally.

Content filters/refusals are distinct from transport errors. A provider-declined
response may be a successful protocol exchange with a typed refusal outcome.

## Results versus exceptions

Expected domain outcomes—denial, approval required, limit reached, invalid tool
arguments, content filter, deferred work, and cancellation—SHOULD be
discriminated results. Programmer errors, violated invariants, and invalid API
arguments MAY throw.

[Async streams](streaming-and-event-protocol.md) report one terminal failure
event and make completion return the same semantic error. They MUST NOT both
throw an unrelated wrapper and emit a different error code.

## Safe messages and diagnostics

Safe messages are concise and may be shown to an operator. Model-visible error
text is separately generated and even more restricted. Diagnostics may contain
redacted payload fragments and exception data under policy; secrets are never
retained.

## Acceptance scenarios

- All provider adapters map the same semantic failures to the same categories.
- A tool timeout with unknown mutation is non-retryable despite a transient
  origin.
- Caller cancellation and deadline timeout remain distinct.
- Stream terminal and completion task expose the same error code.
- Seeded secrets in external error bodies do not reach safe messages/logs.
- Unknown vendor code remains available as external metadata.

## Related specifications

- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
