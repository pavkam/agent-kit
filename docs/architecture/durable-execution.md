# Durable execution

**Role:** Resume useful work after process loss without replaying unsafe
effects.

[Durability is an optional capability with a stable core boundary](../concepts/durable-execution-and-recovery.md).
AgentKit defines recoverable operations and evidence; leaf adapters map them to
workflow engines or durable task systems. The core does not depend on a
particular backend SDK, and an in-memory run never pretends to provide crash
recovery.

Durability contracts live in AgentKit.Abstractions. Concrete integrations use
AgentKit.Durability.BackendName, such as a future Temporal or Restate package.
They register durable operation ownership without changing AgentEngine or the
loop contract.

The optional provider-neutral coordinator lives in AgentKit.Durability.
`AgentEngine` remains one process-level host for many agents; durability
selection and every record are keyed by typed `AgentId`, `SessionId`, `RunId`,
and `OperationId`. There is no engine-wide active workflow or ambient current
agent.

## Normative minimal contract shape

These C# 14 shapes are normative and minimal, not an exhaustive API listing.
Each named type lives in its own file in AgentKit.Abstractions. Shared
`AgentId`, `SessionId`, `RunId`, `OperationId`, `IdempotencyKey`,
`IIdentifierGenerator<TIdentifier>`, and `TimeProvider` contracts are reused.

```csharp
namespace AgentKit;

public readonly record struct CheckpointId(Guid Value);

public readonly record struct DurableBackendKey(string Value);

public readonly record struct DurabilityProfileKey(string Value);

public readonly record struct DurabilityProfileVersion(long Value);

public readonly record struct DurableJournalKey(string Value);

public readonly record struct DurableLeaseManagerKey(string Value);

public readonly record struct RecoveryPolicyKey(string Value);

public readonly record struct ExecutionLeaseId(Guid Value);

public readonly record struct WorkerId(Guid Value);

public readonly record struct FencingToken(long Value);

public readonly record struct DurableOperationVersion(string Value);
```

Checkpoint and lease IDs come from injected generators. Fencing tokens are
allocated atomically by the lease store, never by an in-process counter.

```csharp
namespace AgentKit;

public sealed record DurableExecutionContext(
    DurabilityProfileKey ProfileKey,
    DurabilityProfileVersion ProfileVersion,
    DurableBackendKey BackendKey,
    DurableJournalKey JournalKey,
    DurableLeaseManagerKey LeaseManagerKey,
    RecoveryPolicyKey RecoveryPolicyKey,
    AgentDefinitionRevision AgentDefinitionRevision,
    ConfigurationVersion ConfigurationVersion,
    SecurityAuthorizationContext Authorization);

public sealed record RecoverableOperationDescriptor(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId? TurnId,
    OperationId OperationId,
    DurableExecutionContext ExecutionContext,
    OperationId? CausalParentId,
    DurableOperationName Name,
    DurableOperationVersion Version,
    IdempotencyKey IdempotencyKey,
    OperationPayload Input,
    DurableRetryOwner RetryOwner,
    DurableTimeoutOwner TimeoutOwner,
    CancellationSemantics Cancellation,
    SecurityEffect Effect,
    IdempotencyClassification Idempotency,
    DateTimeOffset Deadline,
    ExtensionData Extensions);

public sealed record DurableCheckpoint(
    CheckpointId Id,
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId? TurnId,
    OperationId OperationId,
    DurableExecutionContext ExecutionContext,
    DurableCheckpointKind Kind,
    OperationPayload State,
    FencingToken FencingToken,
    DateTimeOffset RecordedAt,
    SchemaVersion SchemaVersion);

public sealed record RecoveryEvidence(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId? TurnId,
    OperationId OperationId,
    DurableExecutionContext ExecutionContext,
    DurableOperationState State,
    bool StartDefinitelyAbsent,
    bool TerminalResultRecorded,
    SideEffectCertainty SideEffectCertainty,
    IdempotencyKey? ExternalIdempotencyKey,
    ExternalOperationReference? ExternalReference,
    DurableCheckpoint? LatestCheckpoint,
    FencingToken? LastWriterToken);
```

Operation payloads use registered versioned codecs and preserve unknown
compatible fields. Runtime objects, service scopes, tasks, cancellation sources,
delegates, credentials, and providers are never serialized.

### Backend discovery and selection

```csharp
namespace AgentKit;

public sealed record DurableBackendDescriptor(
    DurableBackendKey Key,
    DurableBackendCapabilities Capabilities,
    ImmutableArray<DurableOperationName> SupportedOperations,
    bool SupportsFencing,
    bool SupportsReconciliation);

public interface IDurableBackendCatalog
{
    ImmutableArray<DurableBackendDescriptor> GetDescriptors();
}

public interface IDurableBackendSelector
{
    ValueTask<DurableBackendSelectionResult> SelectAsync(
        RecoverableOperationDescriptor operation,
        CancellationToken cancellationToken);
}

public interface IDurabilityRuntimeSelector
{
    ValueTask<DurabilityRuntimeActivationResult> ActivateAsync(
        DurableExecutionContext context,
        CancellationToken cancellationToken);
}

public interface IDurabilityRuntimeLease : IAsyncDisposable
{
    DurableExecutionContext Context { get; }
    IDurableExecutionBackend Backend { get; }
    IDurableOperationJournal Journal { get; }
    IDurableLeaseManager LeaseManager { get; }
    IRecoveryPolicy RecoveryPolicy { get; }
}

public interface IDurableOperationCodec<TState>
{
    DurableOperationName OperationName { get; }
    DurableOperationVersion Version { get; }
    OperationPayload Encode(TState value);
    DurableDecodeResult<TState> Decode(OperationPayload payload);
}
```

Backends and codecs are additive, described capabilities. The backend selector
chooses one immutable descriptor for an agent/run operation; activation occurs
later through the runtime lease and never through live DI discovery during
replay. Duplicate keys, operation names, or codec versions are composition
errors unless explicitly replaced.

### Execution, state, policy, and observation

```csharp
namespace AgentKit;

public interface IDurableExecutionBackend
{
    DurableBackendDescriptor Descriptor { get; }

    ValueTask<DurableDispatchResult> DispatchAsync(
        DurableDispatchRequest request,
        CancellationToken cancellationToken);

    ValueTask<DurableReconciliationResult> ReconcileAsync(
        DurableReconciliationRequest request,
        CancellationToken cancellationToken);
}

public interface IDurableOperationJournal
{
    ValueTask<DurableRecordResult> RecordStartAsync(
        DurableOperationStart start,
        CancellationToken cancellationToken);

    ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpoint checkpoint,
        CancellationToken cancellationToken);

    ValueTask<DurableRecordResult> RecordTerminalAsync(
        DurableOperationResult result,
        CancellationToken cancellationToken);

    ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        DurableOperationAddress address,
        CancellationToken cancellationToken);
}

public interface IDurableLeaseManager
{
    ValueTask<ExecutionLeaseResult> AcquireAsync(
        ExecutionLeaseRequest request,
        CancellationToken cancellationToken);
}

public interface IExecutionLease : IAsyncDisposable
{
    ExecutionLeaseId LeaseId { get; }
    WorkerId OwnerWorkerId { get; }
    AgentId AgentId { get; }
    SessionId SessionId { get; }
    TurnId? TurnId { get; }
    OperationId OperationId { get; }
    FencingToken FencingToken { get; }
    DateTimeOffset ExpiresAt { get; }

    ValueTask<LeaseRenewalResult> RenewAsync(
        CancellationToken cancellationToken);
}

public interface IRecoveryPolicy
{
    ValueTask<RecoveryDecision> DecideAsync(
        RecoverableOperationDescriptor operation,
        RecoveryEvidence evidence,
        CancellationToken cancellationToken);
}

public interface IDurableExecutionCoordinator
{
    Task<DurableOperationResult> ExecuteAsync(
        RecoverableOperationDescriptor operation,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);

    Task<DurableOperationResult> RecoverAsync(
        DurableOperationAddress address,
        DurableExecutionContext context,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);
}

public interface IDurableExecutionEventSink
{
    ValueTask PublishAsync(
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken);
}

public interface IDurableExecutionEventDispatcher
{
    ValueTask PublishAsync(
        DurableExecutionContext context,
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken);
}
```

The backend performs one handoff or reconciliation attempt. The journal owns
checkpoint and terminal truth. The lease manager owns fencing state. Recovery
policy decides start, reconcile, retry, commit-only, require-operator, or fail;
it does not execute. Event sinks observe immutable transitions. External
handoff, journal access, and reconciliation are protected operations and use the
authority selected from the captured `DurableExecutionContext.Authorization`;
their effecting adapters validate the supplied grant again.

Turn identity is causal and optional because some durable work occurs between
turns; it is persisted unchanged on descriptors, checkpoints, evidence, and
leases whenever present. The lease manager allocates fencing tokens atomically,
records the injected `WorkerId`, and computes and checks `ExpiresAt` only
through `TimeProvider`. Renewal may advance expiry but never changes operation
identity or reuses an older fencing token.

AgentKit.Durability supplies sealed backend catalog/selector, coordinator,
recovery coordinator, and fenced journal decorator classes. The central
dependency shape is explicit:

```csharp
namespace AgentKit.Durability;

internal sealed class DurableExecutionCoordinator(
    IDurabilityRuntimeSelector runtimeSelector,
    ISecurityAuthoritySelector securityAuthorities,
    IHookDispatcher hooks,
    IDurableExecutionEventDispatcher events,
    TimeProvider timeProvider,
    IIdentifierGenerator<CheckpointId> checkpointIds) :
    IDurableExecutionCoordinator
{
}
```

`IDurableBackendSelector` performs descriptor/capability selection only;
`IDurabilityRuntimeSelector` activates the backend, journal, lease manager, and
recovery policy named by the captured context through typed catalogs. Its
returned `IDurabilityRuntimeLease` owns the attempt scope and the selected
scoped or singleton services. It is the only runtime activation boundary for
those keys; the coordinator never performs a keyed `IServiceProvider` lookup.
The selected services and context version remain fixed until the lease is
asynchronously disposed. The event dispatcher similarly activates only the
additive sinks selected for that context and never exposes their scopes to the
singleton coordinator.

The separate nullable `HookDispatchContext` belongs only to the live invocation;
it is never serialized into a descriptor, checkpoint, context, or recovery
evidence. Recovery without an active run passes `null` and does not fabricate a
run-scoped tracker.

There is no required backend base class. Temporal, Restate, local journal, and
other adapters own different lifecycle mechanics and implement the contract
directly. A protocol-family package may offer an optional base only for proven
codec or polling reuse.

## Configuration and dependency injection

Durability is selected by an agent definition through a `DurabilityProfileKey`.
Global options are host ceilings; named profiles choose the backend, operation
allowlist, checkpoints, lease policy, and recovery policy. Per-run overrides may
tighten limits but cannot weaken the durability required by accepted work or
select a backend or retry mode excluded by the profile. An explicitly ephemeral
new operation is permitted only when the profile advertises that separate mode;
an existing durable operation never changes mode during recovery.

```csharp
namespace AgentKit.Durability;

public sealed class AgentDurabilityOptions
{
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int MaximumRecoveryAttempts { get; set; } = 3;
    public UnknownEffectRecoveryMode UnknownEffectMode { get; set; } =
        UnknownEffectRecoveryMode.RequireOperator;
    public DurableCheckpointMode CheckpointMode { get; set; } =
        DurableCheckpointMode.SemanticBoundaries;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentDurability(
            Action<AgentDurabilityOptions>? configure = null) =>
            DurabilityServiceRegistration.AddAgentDurability(
                services,
                configure);

        public IServiceCollection AddDurabilityProfile(
            DurabilityProfileKey key,
            Action<DurabilityProfileOptions> configure) =>
            DurabilityServiceRegistration.AddDurabilityProfile(
                services,
                key,
                configure);

        public IServiceCollection ReplaceDurabilityProfile(
            DurabilityProfileKey key,
            Action<DurabilityProfileOptions> configure) =>
            DurabilityServiceRegistration.ReplaceDurabilityProfile(
                services,
                key,
                configure);

        public IServiceCollection AddDurabilityBackend<TBackend>(
            DurableBackendKey key)
            where TBackend : class, IDurableExecutionBackend =>
            DurabilityServiceRegistration.AddDurabilityBackend<TBackend>(
                services,
                key);

        public IServiceCollection ReplaceDurabilityBackend<TBackend>(
            DurableBackendKey key)
            where TBackend : class, IDurableExecutionBackend =>
            DurabilityServiceRegistration.ReplaceDurabilityBackend<TBackend>(
                services,
                key);

        public IServiceCollection AddDurableJournal<TJournal>(
            DurableJournalKey key)
            where TJournal : class, IDurableOperationJournal =>
            DurabilityServiceRegistration.AddJournal<TJournal>(services, key);

        public IServiceCollection ReplaceDurableJournal<TJournal>(
            DurableJournalKey key)
            where TJournal : class, IDurableOperationJournal =>
            DurabilityServiceRegistration.ReplaceJournal<TJournal>(
                services,
                key);

        public IServiceCollection AddDurableLeaseManager<TLeaseManager>(
            DurableLeaseManagerKey key)
            where TLeaseManager : class, IDurableLeaseManager =>
            DurabilityServiceRegistration.AddLeaseManager<TLeaseManager>(
                services,
                key);

        public IServiceCollection ReplaceDurableLeaseManager<TLeaseManager>(
            DurableLeaseManagerKey key)
            where TLeaseManager : class, IDurableLeaseManager =>
            DurabilityServiceRegistration.ReplaceLeaseManager<TLeaseManager>(
                services,
                key);

        public IServiceCollection AddRecoveryPolicy<TPolicy>(
            RecoveryPolicyKey key)
            where TPolicy : class, IRecoveryPolicy =>
            DurabilityServiceRegistration.AddRecoveryPolicy<TPolicy>(
                services,
                key);

        public IServiceCollection AddDurableOperationCodec<TState, TCodec>()
            where TCodec : class, IDurableOperationCodec<TState> =>
            DurabilityServiceRegistration
                .AddDurableOperationCodec<TState, TCodec>(services);

        public IServiceCollection ReplaceDurableOperationCodec<TState, TCodec>()
            where TCodec : class, IDurableOperationCodec<TState> =>
            DurabilityServiceRegistration
                .ReplaceDurableOperationCodec<TState, TCodec>(services);

        public IServiceCollection AddDurableExecutionEventSink<TSink>(
            DurableExecutionEventSinkRegistration registration)
            where TSink : class, IDurableExecutionEventSink =>
            DurabilityServiceRegistration.AddEventSink<TSink>(
                services,
                registration);

        public IServiceCollection ReplaceRecoveryPolicy<TPolicy>(
            RecoveryPolicyKey key)
            where TPolicy : class, IRecoveryPolicy =>
            DurabilityServiceRegistration.ReplaceRecoveryPolicy<TPolicy>(
                services,
                key);

        public IServiceCollection
            ReplaceDurabilityRuntimeSelector<TSelector>()
            where TSelector : class, IDurabilityRuntimeSelector =>
            DurabilityServiceRegistration.ReplaceRuntimeSelector<TSelector>(
                services);

        public IServiceCollection
            ReplaceDurableExecutionCoordinator<TCoordinator>()
            where TCoordinator : class, IDurableExecutionCoordinator =>
            DurabilityServiceRegistration.ReplaceCoordinator<TCoordinator>(
                services);

        public IServiceCollection ReplaceDurableBackendCatalog<TCatalog>()
            where TCatalog : class, IDurableBackendCatalog =>
            DurabilityServiceRegistration.ReplaceBackendCatalog<TCatalog>(
                services);

        public IServiceCollection ReplaceDurableBackendSelector<TSelector>()
            where TSelector : class, IDurableBackendSelector =>
            DurabilityServiceRegistration.ReplaceBackendSelector<TSelector>(
                services);

        public IServiceCollection
            ReplaceDurableExecutionEventDispatcher<TDispatcher>()
            where TDispatcher : class, IDurableExecutionEventDispatcher =>
            DurabilityServiceRegistration.ReplaceEventDispatcher<TDispatcher>(
                services);
    }
}
```

The package-internal `DurabilityServiceRegistration` helper performs the
registration without building or resolving a service provider.

`AddAgentDurability` is idempotent and `TryAdd`s the singular coordinator and
engine-wide backend catalog, backend selector, durability-runtime selector, and
event dispatcher. Backends, journals, lease managers, recovery policies, codecs,
and operation handlers are additive keyed registrations with explicit per-key
replacement. Event sinks are additive ordered registrations activated through
the dispatcher under their declared profile filters and lifetimes. A journal
decorator may be added only around an explicitly selected journal; the
provider-neutral package supplies no persistence target. Concrete leaf packages
expose methods such as `AddTemporalDurability` and `AddRestateDurability`; the
provider-neutral registration never installs a backend or pretends process
memory is durable.

Catalogs, selectors, and the coordinator are thread-safe singletons. The
profile-selected journal, lease manager, recovery policy, and backend declare
their own compatible lifetimes. Each execution/recovery attempt owns one
`IDurabilityRuntimeLease` for its activation scope and one `IExecutionLease` for
fenced ownership. Disposing the execution lease stops renewal and cannot revoke
already completed effects; disposing the runtime lease releases selected scoped
services. Backend clients and journal stores are container-owned. Operations for
different agent/session keys may run concurrently; writes for one execution
lease require its current fencing token. Cancellation stops local awaiting and
renewal, then records the true external state. It does not imply a handed-off
backend operation was cancelled.

## Composition validation and unsupported behavior

Durability remains optional. Once an agent definition selects a durability
profile, validation requires one effective coordinator, journal, lease manager,
runtime selector, recovery policy, referenced keyed backend, codec for every
enabled operation version, security authority, hook dispatcher, `TimeProvider`,
ID generators, and required audit/event persistence. Distributed ownership
requires backend, journal, and store fencing support.

Validation also requires the captured profile version and every backend,
journal, lease-manager, recovery-policy, definition, configuration, and security
reference in `DurableExecutionContext` to resolve exactly once. Resume uses the
persisted context; it never substitutes the agent's latest profile silently.

Missing durability on an ordinary in-memory run is not an error. Requesting
crash recovery without a validated profile returns `DurabilityUnavailable`
before the run starts. Unsupported operation versions return
`RecoveryIncompatible`; unknown non-idempotent effects return
`OperatorActionRequired`; stale fencing returns `LeaseLost`. Backend selection
never silently falls back, changed code never reinterprets payloads, and a
workflow retry setting cannot override the operation's side-effect safety.

## Durable operations

A recoverable operation has stable operation and idempotency identities, causal
run and turn references, versioned serializable input and result, declared retry
and timeout ownership, cancellation behavior, side-effect classification, and a
checkpoint or terminal record.

Model requests, tool calls, compaction, approval waits, and selected hooks may
become durable operations. Pure deterministic preparation normally replays from
captured manifests rather than serializing the runtime object graph.

Useful checkpoints occur after admission and promotion, context manifest
creation, provider terminal validation, tool-call recording, every terminal tool
result, message commit, compaction activation, and settlement. High- frequency
stream deltas are not required to reconstruct stable state.

## Recovery

Recovery follows evidence. Work definitely not started may begin. An operation
with a supported idempotency key may be reconciled or retried. A terminal result
whose commit is missing may be committed without reinvocation. Work accepted by
an external durable owner resumes through that owner.

A started non-idempotent operation with unknown outcome is not retried. It
requires reconciliation or operator action. Exactly-once is not achieved by
adding optimism to a retry loop, darling.

Recovery treats these boundaries independently:

| Last verified durable evidence                                     | Permitted next action                                                 |
| ------------------------------------------------------------------ | --------------------------------------------------------------------- |
| Accepted intent; enforcement not started                           | Reauthorize and start only after proving no prior driver can start it |
| Grant use consumed or effect may have started; no terminal outcome | Reconcile, use receiver idempotency, or require operator action       |
| Full terminal outcome recorded; history projection absent          | Reproject and append under the captured policy; never invoke          |
| Terminal run outcome; settlement/outbox pending                    | Finish required commits and delivery; preserve semantic outcome       |
| Settled run; later approval or child result arrives                | Append after-run causality and admit new work if authorized           |

Absence of a record is evidence of non-start only when the enforced protocol
requires that record before every start and the prior owner is fenced out. A
missing checkpoint in an unavailable or stale store is not such evidence.

## Determinism and versioning

Replay uses captured configuration and catalog versions plus injected time,
identities, and randomness. It performs no live container discovery or ambient
environment reads. Schema and operation changes require migration, pinned old
behavior, or a typed incompatibility result.

## Distributed ownership

Distributed execution has one authoritative owner for each claimed lane or
operation scope. Session mutation serialization remains a separate shared
boundary across lanes. Leases include expiry, renewal, owner identity, and a
monotonically increasing fencing token. The lease service is authoritative for
expiry; workers use injected monotonic time for local renewal/deadline handling
and cannot extend ownership based on their own wall clock. Every durable write
verifies the active token, so a stale worker cannot append after takeover.

A storage fence does not stop an already-running external process or remote
request. Safe takeover of an effect requires receiver-enforced fencing,
receiver-side idempotency, or reconciliation that proves it cannot duplicate
work. Without such evidence, a successor reconciles or requires operator action;
it does not invoke again simply because it acquired a newer lease. Required
budget/grant/journal stores must support the same distributed ownership domain.
A durable session store paired with process-local grant or budget state is not a
crash-safe distributed composition.

Wake signals are hints and may be duplicated or lost. Durable admitted input is
the source of truth. Settlement is complete only when run-owned work is
terminal, safely handed off, or
[durably represented for recovery](../concepts/run-lifecycle-and-settlement.md).

## Related concept specifications

- [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)
- [Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md)
- [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)
