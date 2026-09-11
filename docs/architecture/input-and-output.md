# Input and output

**Role:** Define how work enters one selected agent in a multi-agent engine and
how activity and results leave it.

I/O is a protocol boundary around the runtime. It accepts application input,
queues work safely, exposes live progress, and returns one typed final result.
Channel adapters such as HTTP, console, chat, UI, or background workers
translate their own protocols at this boundary; they do not become part of the
loop.

Input, output, receipt, event, and stream contracts live in
AgentKit.Abstractions. AgentKit.IO contains the first-party admission
coordinator, queued-input promotion, bounded live event publication, and final
result coordination. AddAgentIO registers keyed, replaceable runtime components;
each agent definition selects exactly one effective input coordinator and output
publisher.

AgentKit.IO does not become a second durable store. It records admission,
promotion, and settlement through AgentKit.Session contracts. Channel-specific
adapters remain leaves and may use names such as AgentKit.IO.AspNetCore or
AgentKit.IO.Console when their reusable behavior justifies a package.

## Human questions

`IHumanQuestionBroker` is the protected runtime boundary used by a question
tool. Its request carries the stable question, agent/session/tool-call
causality, authenticated asking identity, ordered bounded options, free-text
policy, deadline, and the exact publication grant. The first-party
`DefaultHumanQuestionBroker` recomputes concrete effect evidence and atomically
consumes the grant immediately before publication. A mismatch, stale grant,
revocation, expiry, or exhaustion fails closed without calling the application
channel.

The selected `IHumanQuestionChannel` receives a grant-free
`HumanQuestionPrompt`. The adapter owns presentation and authentication of the
answer as new input; rendering alone never authorizes or resolves the question.
Durable adapters persist pending state and exactly one terminal resolution
through session/application storage rather than treating the caller's in-memory
wait as the record of truth. Cancellation ends the caller's wait without
fabricating an answer, while timeout and unavailable-channel outcomes remain
explicit.

## Input admission

Input follows the
[admission and promotion contract](../concepts/input-admission-and-message-queues.md):
it is authorized, bounded, validated, and durably admitted before the caller
receives success. Each admission has a stable identity and is idempotent. A
repeated equivalent request returns the existing receipt; the same identity with
different content is a conflict.

Two delivery classes are preserved. Steering input becomes eligible at the next
safe turn boundary. Follow-up input waits until the current work would otherwise
finish. Promotion uses a captured cutoff and deterministic order, so concurrent
arrivals are never inserted into an in-flight provider request or between a tool
call and its result.

The queue defines capacity, ordering, leases where needed, retention, poison
input behavior, and backpressure. Full queues fail with a typed outcome rather
than silently dropping data or blocking forever. Durable queue state remains in
the session record so recovery cannot observe one conversation history and a
different input truth.

Admission resolves the lane before persistence. Receipts and queued items retain
that lane; promotion can consume only eligible inputs for the named lane and
expected operation. A cutoff is a typed `SessionSequence` from durable admission
order, not a live run-event sequence. Duplicate equivalent input returns the
original authorized receipt before capacity is reserved; replay cannot fail just
because the queue is now full or consume a second slot.

Promotion atomically records the selected admission IDs, their target turn, and
their consumed state with the corresponding history transition. A lost
acknowledgement is reconciled using that promotion identity. It cannot consume
input twice, change its target lane, or leave history committed while the same
input is still eligible for another turn.

## Live output

Live output follows the
[typed event grammar](../concepts/streaming-and-event-protocol.md): response and
part boundaries, content deltas, tool progress, usage updates, lifecycle
transitions, and diagnostics. Deltas are provisional. Consumers cannot mutate
the candidate message, and abandoning a subscription does not cancel the run
unless the API explicitly grants subscription ownership.

Fan-out is bounded. Required consumers may apply backpressure; best-effort
consumers may receive coalesced deltas, a dropped-event marker, or
disconnection. Durable semantic events are never silently dropped.

## Final output

The final result identifies the run and session, terminal outcome, newly
committed messages, usage, deferred requests, and validated application output.
Success, idle completion, deferral, cancellation, policy halt, limit exhaustion,
and failure are distinct outcomes.

[Structured output](structured-output.md) is locally validated even when a
provider claims native schema enforcement. Text, schema-backed data, synthetic
output tools, provider-native output, media, and named union alternatives remain
distinct modes. Validation retries use their own budget. Provisional streamed
data never becomes final application output before terminal validation.

AgentKit.Output owns extraction and validation and returns a typed decision to
the loop. AgentKit.IO publishes the resulting events and final value; neither
component calls back into the other.

## Boundary rules

Input adapters cannot append arbitrary history or bypass session authorization.
Output adapters cannot infer state from display text or treat live deltas as
durable facts. The runtime remains usable without any specific UI, transport, or
hosting model.

The loop owns state transitions. The I/O component owns how accepted work enters
those transitions and how provisional and final activity reaches consumers.

## Normative minimal input contracts

The following C# shapes are normative and minimal rather than exhaustive. Every
named type lives in a matching source file. Shared IDs use the canonical pattern
from [composition and configuration](composition-and-configuration.md).
`InputId` is the caller-visible idempotency identity; `AdmissionId` identifies
the one durable acceptance produced by the runtime.

```csharp
namespace AgentKit;

public enum InputDelivery
{
    Steer,
    FollowUp
}

public sealed record AgentInput(
    InputId Id,
    InputDelivery Delivery,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions);

public sealed record InputAdmissionRequest(
    AgentId AgentId,
    SessionId SessionId,
    ExecutionLaneId ExecutionLaneId,
    ExecutionIdentity Identity,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization,
    AgentInput Input,
    SessionVersion? ExpectedVersion);

public sealed record AdmittedInput(
    AdmissionId AdmissionId,
    InputId InputId,
    AgentId AgentId,
    SessionId SessionId,
    ExecutionLaneId ExecutionLaneId,
    ExecutionIdentity Identity,
    SessionSequence AdmittedSequence,
    InputDelivery Delivery,
    AgentInput Payload,
    DateTimeOffset AdmittedAt,
    SessionSequence? PromotedSequence);

public sealed record AdmissionReceipt(
    AdmissionId AdmissionId,
    InputId InputId,
    AgentId AgentId,
    SessionId SessionId,
    ExecutionLaneId ExecutionLaneId,
    SessionSequence AdmittedSequence,
    bool Existing);

public abstract record InputAdmissionResult;

public sealed record AcceptedInput(AdmissionReceipt Receipt)
    : InputAdmissionResult;

public sealed record InputConflict(InputId InputId, string Reason)
    : InputAdmissionResult;

public sealed record QueueCapacityExceeded(
    InputCapacityLimit Limit,
    TimeSpan? RetryAfter) : InputAdmissionResult;

public sealed record RejectedInput(InputRejection Rejection)
    : InputAdmissionResult;

public sealed record InputPromotionRequest(
    AgentId AgentId,
    SessionId SessionId,
    ExecutionLaneId ExecutionLaneId,
    RunId RunId,
    TurnId? PreviousTurnId,
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization,
    SessionSequence CutoffSequence,
    PromotionBoundary Boundary);

public sealed record InputPromotionResult(
    ImmutableArray<AdmittedInput> Promoted,
    SessionSequence CutoffSequence,
    SessionVersion SessionVersion);

public interface IInputCoordinator
{
    ValueTask<InputAdmissionResult> AdmitAsync(
        InputAdmissionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<InputPromotionResult> PromoteAsync(
        InputPromotionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IInputQueue
{
    ValueTask<InputAdmissionResult> AppendAsync(
        InputAdmissionRequest request,
        AdmissionId admissionId,
        DateTimeOffset admittedAt,
        CancellationToken cancellationToken = default);

    ValueTask<InputPromotionResult> PromoteAsync(
        InputPromotionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IInputPromotionPolicy
{
    ValueTask<InputPromotionPlan> PlanAsync(
        InputPromotionContext context,
        CancellationToken cancellationToken = default);
}
```

These hot contracts use `ValueTask` because in-memory and no-op paths commonly
complete synchronously even though durable adapters may perform I/O. The
coordinator validates size, schema, session address, authority, capacity, and
idempotency before calling the queue. The queue owns atomic ordering and durable
promotion; the policy decides _which eligible inputs_ to request and cannot
append history directly.

`AdmissionId` is allocated by injected `IIdentifierGenerator<AdmissionId>` only
after validation and before the idempotent append. A duplicate `InputId` with
equivalent canonical content returns the original admission receipt. A
conflicting duplicate cannot consume a new sequence or replace existing content.

## Normative minimal output contracts

```csharp
namespace AgentKit;

public enum RunEventDurability
{
    Live,
    Durable
}

public abstract record RunEvent(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    RunId RunId,
    TurnId? TurnId,
    long Sequence,
    DateTimeOffset OccurredAt,
    RunEventDurability Durability);

public sealed record ContentDeltaEvent(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    RunId RunId,
    TurnId? TurnId,
    long Sequence,
    DateTimeOffset OccurredAt,
    ModelRequestId RequestId,
    int PartIndex,
    ContentDelta Delta)
    : RunEvent(
        AgentId,
        SessionId,
        ConversationId,
        RunId,
        TurnId,
        Sequence,
        OccurredAt,
        RunEventDurability.Live);

public sealed record MessageCommittedEvent(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    RunId RunId,
    TurnId? TurnId,
    long Sequence,
    DateTimeOffset OccurredAt,
    MessageId MessageId,
    SessionVersion SessionVersion)
    : RunEvent(
        AgentId,
        SessionId,
        ConversationId,
        RunId,
        TurnId,
        Sequence,
        OccurredAt,
        RunEventDurability.Durable);

public abstract record AgentRunOutcome;

public sealed record RunSucceeded : AgentRunOutcome;
public sealed record RunIdle : AgentRunOutcome;
public sealed record RunDeferred(
    ImmutableArray<DeferredOperationRequest> Requests)
    : AgentRunOutcome;
public sealed record RunCancelled(CancellationReason Reason) : AgentRunOutcome;
public sealed record RunLimitReached(RunLimitFailure Limit) : AgentRunOutcome;
public sealed record RunPolicyHalted(PolicyHalt Reason) : AgentRunOutcome;
public sealed record RunFailed(RunFailure Failure) : AgentRunOutcome;

public abstract record AgentRunResult<TOutput>;

public sealed record AgentRunRejected<TOutput>(
    AgentId AgentId,
    SessionId SessionId,
    AgentError Failure) : AgentRunResult<TOutput>;

public abstract record RunSettlementOutcome;

public sealed record RunSettlementCompleted : RunSettlementOutcome;

public sealed record RunSettlementRecoveryRequired(
    AgentError Failure) : RunSettlementOutcome;

public sealed record AgentRunFinished<TOutput>(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    RunId RunId,
    AgentRunOutcome Outcome,
    RunSettlementOutcome Settlement,
    TOutput? Output,
    MessageCursor PreviousCursor,
    ImmutableArray<AgentMessage> NewMessages,
    RunUsage Usage,
    ImmutableArray<DeferredOperationRequest> DeferredRequests,
    ExtensionData Metadata) : AgentRunResult<TOutput>;

public abstract record AgentRunStreamStartResult<TOutput>;

public sealed record AgentRunStreamStarted<TOutput>(
    IAgentRunStream<TOutput> Stream) : AgentRunStreamStartResult<TOutput>;

public sealed record AgentRunStreamRejected<TOutput>(
    AgentRunRejected<TOutput> Rejection) : AgentRunStreamStartResult<TOutput>;

public interface IOutputPublisher
{
    ValueTask PublishAsync(
        RunEvent runEvent,
        CancellationToken cancellationToken = default);

    ValueTask CompleteAsync<TOutput>(
        AgentRunFinished<TOutput> result,
        CancellationToken cancellationToken = default);
}

public interface IAgentRunStream<TOutput> : IAsyncDisposable
{
    AgentId AgentId { get; }
    SessionId SessionId { get; }
    ConversationId? ConversationId { get; }
    RunId RunId { get; }

    IAsyncEnumerable<RunEvent> ReadAllAsync(
        CancellationToken cancellationToken = default);

    Task<AgentRunFinished<TOutput>> Completion { get; }
}
```

An expected rejection before run acceptance returns `AgentRunRejected`, without
a `RunId`, terminal run event, or fabricated run state. It retains the requested
address and a safe typed error; unauthorized rejection does not disclose whether
a session exists. Invalid CLR arguments still fail at the argument boundary.
`StreamAsync` returns either a started subscription or that same rejection.
After acceptance, `RunAsync` returns `AgentRunFinished` and a started stream's
`Completion` returns the identical finished envelope. Callers must distinguish
pre-run rejection from a failed accepted run.

The semantic outcome, output, usage snapshot, and message order freeze at the
run terminal transition. The final envelope is created after the bounded
settlement attempt and additionally reports `Settlement`. A successful semantic
outcome with `RunSettlementRecoveryRequired` is not clean success. Required
persistence or audit failure cannot change earlier effects into failure-to-start
or mutate the terminal output. Recovery status is queried through the original
run/operation identity; a later recovery record never mutates a result already
returned to a caller.

`RunUsage` retains immutable, revisioned contribution evidence and exact
aggregates as defined by
[usage accounting](../concepts/usage-limits-and-budgets.md#run-usage-projection).
Each charged retry has its own entry identity. Unknown measurements stay
unknown, and later corrections create a new projection without mutating an
already returned result. The session usage ledger remains the durable owner.

`ContentDeltaEvent` and `MessageCommittedEvent` require a non-null `TurnId` at
construction; their positional type matches the nullable base property exactly.
A domain event's required correlation is validated before publication.

A run event's stable protocol identity is the composite `(RunId, Sequence)`.
Sequence is monotonic within that run and is not a process-global identity;
publishers, sinks, durable projections, and reconnecting subscribers use the
same composite for deduplication and resume.

The I/O publisher is the sole sequence allocator for one run. For recoverable
runs it reserves bounded sequence ranges through the session mutation boundary
before publishing from them. The persisted high-water mark survives drive loss;
a successor starts above every reserved range, including numbers allocated only
to lost live events. Durable event envelopes persist their assigned sequence
with their semantic entry or outbox intent, so redelivery preserves identity.
Consumers allow gaps and receive an explicit loss/resnapshot boundary; they
never treat a fresh in-process counter as continuation of the old stream.
Provider-attempt sequences are separate and do not become run-event sequences.

`ReadAllAsync` is genuine incremental streaming. Its cancellation token cancels
only that subscription by default; explicit run cancellation uses the engine or
agent control surface and produces a typed terminal outcome. `Completion` may be
awaited repeatedly and therefore remains `Task`. Disposing the stream releases
the subscription and buffers exactly once but does not imply ownership of the
run unless a separately named owning-stream option says so.

The outcome, not `Output is null`, states why the run ended. A `RunSucceeded`
finished result contains output accepted by the configured output contract; a
schema-backed untyped contract returns validated JSON. Partial streamed
structures remain provisional and never populate the final result before
terminal validation.

The additive `IRunEventSink` contract is owned by
[observability](observability.md). AgentKit.IO consumes those sinks for
delivery; it does not declare a competing observer abstraction.

Final-marker delivery and output completion follow the
[non-recursive settlement barrier](../concepts/run-lifecycle-and-settlement.md#result-availability).
Required preceding deliveries finish before the final marker commits; the marker
itself requires durable outbox acceptance, while its external delivery receipt
is separate. `CompleteAsync` exposes the frozen envelope and cannot add a new
required effect that changes its settlement status.

## First-party classes and dependencies

AgentKit.IO coordinates explicit abstractions; it does not inherit or implement
session storage:

```csharp
namespace AgentKit.IO;

internal sealed class DefaultInputCoordinator(
    IInputQueue inputQueue,
    IInputPromotionPolicy promotionPolicy,
    ISecurityAuthoritySelector securityAuthorities,
    IIdentifierGenerator<AdmissionId> admissionIdGenerator,
    TimeProvider timeProvider,
    ILogger<DefaultInputCoordinator> logger) : IInputCoordinator
{
}

internal sealed class DefaultOutputPublisher(
    IEnumerable<IRunEventSink> sinks,
    IOutputBackpressurePolicy backpressurePolicy,
    RunEventHub eventHub,
    TimeProvider timeProvider,
    ILogger<DefaultOutputPublisher> logger) : IOutputPublisher
{
}
```

The coordinator and publisher bodies are intentionally omitted from these
constructor/dependency shapes; their observable members are exactly the public
contracts above. The internal `RunEventHub` implements `IAsyncDisposable`; its
completion releases or settles every subscription owned by that run scope.

Admission captures the complete immutable `ExecutionIdentity` authenticated by
the trusted ingress. The coordinator validates that it equals
`SecurityAuthorizationContext.Identity`; the queue persists that snapshot with
the admitted item so delayed promotion cannot reconstruct identity from an
ambient principal or from tenant/principal projections. Promotion with a
different identity or authorization snapshot fails typed before reading or
mutating session state.

`IInputQueue` is a session-backed contract selected with the session store, so
admission and history cannot diverge. `RunEventHub` is an internal bounded
fan-out mechanism, not a second public event system or a base class for channel
adapters. Direct implementations of `IInputCoordinator` and `IOutputPublisher`
remain supported.

The input coordinator and output publisher are run-scoped. The hub and mutable
subscription state are run-scoped. Stateless, thread-safe promotion and
backpressure policies may be singleton. Sink lifetime is explicit: required
sinks may be scoped and are awaited through settlement; best-effort singleton
sinks may drain independently only when the selected policy permits it.

## DI, options, and unsupported behavior

```csharp
namespace AgentKit.IO;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentIO(
            ComponentKey<IInputCoordinator> inputKey,
            ComponentKey<IOutputPublisher> outputKey,
            Action<AgentIOOptions>? configure = null) =>
            AgentIORegistration.Add(services, inputKey, outputKey, configure);

        public IServiceCollection AddInputCoordinator<TCoordinator>(
            ComponentKey<IInputCoordinator> key)
            where TCoordinator : class, IInputCoordinator =>
            AgentIORegistration.AddInput<TCoordinator>(services, key);

        public IServiceCollection ReplaceInputCoordinator<TCoordinator>(
            ComponentKey<IInputCoordinator> key)
            where TCoordinator : class, IInputCoordinator =>
            AgentIORegistration.ReplaceInput<TCoordinator>(services, key);

        public IServiceCollection AddOutputPublisher<TPublisher>(
            ComponentKey<IOutputPublisher> key)
            where TPublisher : class, IOutputPublisher =>
            AgentIORegistration.AddOutput<TPublisher>(services, key);

        public IServiceCollection ReplaceOutputPublisher<TPublisher>(
            ComponentKey<IOutputPublisher> key)
            where TPublisher : class, IOutputPublisher =>
            AgentIORegistration.ReplaceOutput<TPublisher>(services, key);

        public IServiceCollection AddRunEventSink<TSink>(
            RunEventSinkRegistration registration)
            where TSink : class, IRunEventSink =>
            AgentIORegistration.AddRunEventSink<TSink>(services, registration);
    }
}
```

The default coordinator and publisher use `TryAddKeyedScoped`; they are singular
per key and have explicit per-key replacement. Sinks are additive with stable
identity, deterministic order, declared required/best-effort delivery, and
duplicate-conflict validation. `AddAgentIO` is idempotent for equivalent options
and never chooses a session store or channel adapter.

Typed options and agent/run profiles configure queue item/byte/age bounds,
promotion batch policy, admission and settlement deadlines, fan-out capacity,
coalescing, subscriber ownership, required-sink backpressure, and best-effort
drop/disconnect behavior. Defaults are bounded: one follow-up is promoted at an
idle boundary, durable events cannot drop, live deltas may coalesce only with an
observable marker, and no subscriber cancellation owns the run implicitly.

Build validates selected keys, queue/store compatibility, positive bounds, scope
safety, event-order constraints, and a terminal path for every publisher. Queue
full, conflicting input, invalid payload, unauthorized session, closed
subscription, slow consumer, unsupported output mode, and output-validation
failure are typed results. Channel adapters translate those outcomes; they do
not replace them with transport strings or bypass admission.

## Related concept specifications

- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
- [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)
- [Structured output](../concepts/structured-output.md)
