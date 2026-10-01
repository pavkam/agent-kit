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

Storage adaptation and workflow ownership remain distinct. The
`AgentKit.Durability.Sqlite` leaf implements a durable local journal and only
those lease semantics SQLite can prove; it is not a workflow engine or
distributed owner. Its journal and lease manager share one host-supplied
database, which is what makes a fencing generation allocated by the lease
manager actually fence a journal write. The `AgentKit.Durability.Json` leaf
implements the same journal contract over a flushed append-only record log for
durable inspectable local files; it holds an advisory exclusive lock, rejects a
second writer, and therefore ships no lease manager. An
`AgentKit.Durability.InMemory` journal is useful for conformance and explicitly
ephemeral execution but cannot satisfy a crash-recovery profile. Every leaf runs
the same journal contract suite, and the host selects their key and persistence
target explicitly.

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
    SecurityAuthorizationContext Authorization)
{
    public SecurityAuthorizationScope AuthorizationScope => Authorization.Scope;
    public AgentDefinitionRevision AgentDefinitionRevision => Authorization.AgentDefinitionRevision;
    public ConfigurationVersion ConfigurationVersion => Authorization.ConfigurationVersion;
}

public sealed record DurableOperationBinding(
    DurableOperationAddress Address,
    DurableExecutionContext ExecutionContext);

public sealed record RecoverableOperationDescriptor(
    DurableOperationBinding Binding,
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
    DurableOperationBinding Binding,
    DurableCheckpointKind Kind,
    OperationPayload State,
    FencingToken FencingToken,
    DateTimeOffset RecordedAt,
    SchemaVersion SchemaVersion);

public sealed record RecoveryEvidence(
    DurableOperationBinding Binding,
    DurableOperationState State,
    bool StartDefinitelyAbsent,
    bool TerminalResultRecorded,
    SideEffectCertainty SideEffectCertainty,
    DurableOperationResult? RecordedResult,
    DateTimeOffset? NotBefore,
    IdempotencyKey? ExternalIdempotencyKey,
    ExternalOperationReference? ExternalReference,
    DurableCheckpoint? LatestCheckpoint,
    FencingToken? LastWriterToken);
```

Operation payloads use registered versioned codecs and preserve unknown
compatible fields. Runtime objects, service scopes, tasks, cancellation sources,
delegates, credentials, and providers are never serialized.

### Shared side-effect certainty

`SideEffectCertainty` is the shared numeric durable fact:
`DefinitelyNotPerformed` is `0`, `Unknown` is `1`, `DefinitelyPerformed` is `2`,
`PartiallyPerformed` is `3`, and `NotApplicable` is `4`. Its certainty concerns
the relevant external effect, never whether a result record was durably written;
`TerminalResultRecorded` carries that separate fact.

`DefinitelyNotPerformed` requires affirmative evidence that the relevant effect
did not occur, rather than merely an absent start record. `Unknown` says no
completion certainty exists. `DefinitelyPerformed` says the effect completed.
`PartiallyPerformed` requires affirmative evidence of partial completion and is
possibly started for replay safety. `NotApplicable` is valid only where an
operation has no relevant external-effect boundary; it is never an alternate
spelling of unstarted work.

For example, a host can know a payment completed while its result journal write
failed: the certainty is `DefinitelyPerformed` and `TerminalResultRecorded` is
false. A timeout after a confirmed partial upload is `PartiallyPerformed`, so
recovery retains idempotency or reconciliation protections instead of replaying
blindly.

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
    ComponentId SecurityAudience { get; }

    ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken);

    ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken);

    ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken);

    ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken);

    ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
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

### Handlers and mid-operation evidence

```csharp
namespace AgentKit;

public interface IDurableOperationHandler
{
    DurableOperationName OperationName { get; }

    ValueTask<DurableOperationResult> InvokeAsync(
        DurableInvocationContext context,
        CancellationToken cancellationToken);
}

public sealed record DurableInvocationContext
{
    RecoverableOperationDescriptor Operation { get; }
    IExecutionLease Lease { get; }
    IDurableCheckpointWriter Checkpoints { get; }
    HookDispatchContext? Hooks { get; }
}

public interface IDurableCheckpointWriter
{
    DurableOperationBinding Binding { get; }
    FencingToken FencingToken { get; }

    ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpointKind kind,
        OperationPayload state,
        CancellationToken cancellationToken);

    ValueTask<DurableRecordResult> RecordWaitingAsync(
        DurableWaitCondition condition,
        CancellationToken cancellationToken);
}
```

A handler owns the effect and nothing else. The coordinator owns the fenced
journal, the execution lease, the captured authorization, and checkpoint
identity, so it hands each invocation a writer bound to exactly that attempt
rather than any of those collaborators. A handler can therefore record a
semantic boundary it reached or a condition it is waiting on, but it cannot
write under a generation it never acquired, address a record to another
operation, or bypass the authority named by the operation's captured
authorization. The writer returns a typed `DurableRecordResult` rather than
throwing on refusal so a handler that lost ownership can see
`DurableRecordFenced` and stop. It is revoked when the invocation returns, so a
writer captured into background work refuses instead of committing under a lease
the coordinator may have released. A checkpoint replaces the operation's
complete recorded state; writes are not ordered relative to each other, so a
handler that needs order awaits each write.

This replaced an earlier `InvokeAsync(descriptor, lease)` shape that gave a
handler no way to write mid-operation evidence at all. Promotion, settlement,
activation, and approval waits are exactly mid-operation events, which is why
the break was made before any of them journaled.

Each checkpoint is authorized with the effect the journal recomputes and
enforces: start is `Create`, a checkpoint is `Append`, a wait is `Mutate`, and
the terminal record is `Append`. A grant for any other effect is refused at the
journal.

A boundary that must not proceed without its evidence calls
`DurableRecordResult.ThrowIfNotRecorded()` before its effect. The model attempt,
each tool call, and compaction activation do; a refused or fenced checkpoint
aborts the boundary before the provider, tool, or session append is reached. A
boundary whose checkpoint only confirms an effect that already happened, such as
settlement, does not undo that effect when the record is refused.

Turn identity is causal and optional because some durable work occurs between
turns; it is persisted unchanged on descriptors, checkpoints, evidence, and
leases whenever present. A durable binding accepts an
`InRunOperationCorrelation` only when agent, session, operation, run, and
optional turn match exactly. It also accepts an `AfterRunOperationCorrelation`
when the address run is its causal run and the address has no turn; that causal
relationship never claims an active run. The present address shape cannot
represent `BeforeRunOperationCorrelation` or sessionless work, so those cases
are rejected until a future typed address evolves the contract.

That rejection is a deliberate design, not a gap, for the one first-party
boundary it affects: explicit maintenance compaction (`Agent.CompactAsync`),
which runs under a `BeforeRunOperationCorrelation` because no run exists. It has
no in-run address, and the only way to journal it would be to fabricate a
`RunId`, which would make recovery report the work as belonging to a run that
never contained it. A typed address that models run-less work would change the
address every journal, lease manager, fence, recovery policy, and storage codec
is keyed by, for work that has no recovery obligation: the engine never resumes
maintenance on its own, because it is caller-initiated and caller-retried, and
its activation append is already idempotent and reconcilable without a journal
(the compaction's identity keys the append, a retry replays the identical
request, and a reconciliation read of the branch tip proves whether the record
committed). Explicit maintenance therefore stays unjournaled even when the
agent's compaction profile is paired with a durability profile that enables
`agentkit.compaction.activation`; the profile's journaling applies to every
activation whose capture can be durably addressed, namely in-run compaction and
compaction that causally follows a settled run (`AfterRunOperationCorrelation`).
A process lost during maintenance leaves either a committed activation record or
none, never a half-applied checkpoint, and the caller reissues the identical
request to learn which. The lease manager allocates fencing tokens atomically,
records the injected `WorkerId`, and computes and checks `ExpiresAt` only
through `TimeProvider`. Renewal may advance expiry but never changes operation
identity or reuses an older fencing token.

An enforcement intent carries that exact existing fencing generation only when
its immediate protected action operates under an owner already acquired by this
worker. Durable writes and renewal therefore require their current fence and
fail closed when it is absent or stale. An authorized evidence read is unfenced
only when its selected access contract does not require ownership; acquisition
atomically seeks a new generation, whether first ownership or takeover, without
supplying one this worker has not acquired. Lease-store expiry/CAS and
reconciliation govern takeover, and a stale prior fence never starts a new
effect. An absent fence is not a claim that backing storage is local and does
not weaken captured-grant, audience, resource, fingerprint, fresh-receipt, or
required-audit validation. The first-party journals implement these protected
ingress checks: every write consumes its grant, presents its fence, and is
audited before it is acknowledged.

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

A SQLite journal registration supplies no invented database path and no hidden
backend selection. Composition may pair it only with a backend and lease manager
whose ownership domain is compatible with its local durability and transaction
capabilities. Atomic journal commits do not include session, grant, budget, or
external-effect stores unless an adapter explicitly proves one shared
transaction beneath all participating contracts.

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

## Simple composition

`AgentKit.Simple` offers `WithDurability` for the local path. It registers the
process-local in-memory journal, lease manager, and backend, the default
recovery policy, and one profile enabling all seven first-party boundaries, then
selects that profile on every hosted definition. It also registers the boundary
handlers the profile needs whose owning package the simple composition does not
otherwise compose (`AgentKit.IO` for promotion and settlement, compaction for
activation), idempotently with each owner's own registration.

The adapters are explicitly ephemeral: boundaries, checkpoints, and recovery
decisions are real and inspectable, but nothing survives the process, so the
sugar claims no crash recovery. `WithSqliteDurability(databasePath)` and
`WithJsonDurability(directoryPath)` select the durable adapters over the same
profile, handlers, and recovery policy at an explicit absolute path that is
never implied; the sugar is the trusted bootstrap, so it validates, creates, and
binds the store (and replays the JSON journal) when the engine first resolves
the journal. SQLite supplies the journal, lease manager, and backend and is
durable local storage, authoritative on one host. JSON supplies only the
journal, so leases and backend ownership remain process-local and it claims no
multi-process coordination; its root must be canonical because the store refuses
a root that traverses a symbolic link. A composition selects one store: a
second, different choice throws. An application that needs cross-process
ownership registers its own keyed adapters and calls `AddDurabilityProfile` on
`AgentEngineBuilder.Services` instead. The journal is a protected boundary and
needs a grant store and audit dispatcher, so the sugar is paired with
`UseLocalDevelopmentDefaults` or an explicit security composition.

## Composition validation and unsupported behavior

Durability remains optional. Once an agent definition selects a durability
profile, validation requires one effective coordinator, journal, lease manager,
runtime selector, recovery policy, referenced keyed backend, codec for every
enabled operation version, security authority, hook dispatcher, `TimeProvider`,
ID generators, and required audit/event persistence. Distributed ownership
requires backend, journal, and store fencing support.

Validation also requires a registered `IDurableOperationHandler` for every
operation name a selected profile enables
(`agentkit.durability.handler.missing`). A profile that lists a boundary
promises evidence for it, and without a handler the coordinator could only
refuse that boundary in the middle of a run.

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

First-party components journal seven boundaries through one shared mechanism,
`DurableBoundaryScope` in `AgentKit.Abstractions`. A component resolves its
definition's profile once, asks whether the profile enables the boundary's name,
and wraps the work in the coordinator's acceptance, dispatch, and terminal
commit. The scope publishes the live continuation into an engine-wide
`DurableBoundaryRegistry` for exactly one operation identity; the
`DurableBoundaryHandler` subclasses bridge the coordinator to it. A recovering
process holds no continuation and refuses rather than inventing a terminal
record, which is what stops a recovered operation from being replayed without
its live state.

| Operation name                       | Owner and call site                           | Checkpoint written                   |
| ------------------------------------ | --------------------------------------------- | ------------------------------------ |
| `agentkit.engine.run_admission`      | `AgentKit` facade, after acceptance commits   | `InputAdmitted`                      |
| `agentkit.io.input_promotion`        | loop, around `IInputCoordinator.PromoteAsync` | `InputAdmitted` when input committed |
| `agentkit.loop.model_request`        | loop, around each model attempt               | `ContextManifestCreated`             |
| `agentkit.loop.tool_call`            | loop, around each requested call              | `ToolCallRecorded`                   |
| `agentkit.compaction.activation`     | compaction activation coordinator             | `CompactionActivated`, before append |
| `agentkit.permissions.approval_wait` | approval-wait recorder                        | a waiting record, not a checkpoint   |
| `agentkit.io.run_settlement`         | loop, after the run's outcome is determined   | `RunSettled`                         |

A boundary is journaled only when the selected profile enables its name, so
durability is additive and never changes what a component computes. Payloads are
manifests of identities and counts, because prompts, tool arguments, results,
approval prompts, and requested resources are content and never enter a durable
record. Every boundary is declared non-idempotent: a started attempt with no
terminal record has an unknown effect, so recovery escalates to an operator
instead of replaying it.

The approval wait records `DefinitelyNotPerformed` certainty through
`RecordWaitingAsync`, naming the pending approval as its external reference. It
is evidence, not authority: it grants, widens, and consumes nothing.

**Recorder deviation.** The approval wait is recorded by an
`IApprovalWaitRecorder` (`DurableApprovalWaitRecorder` in
`AgentKit.Permissions`) that the component observing a
`SecurityApprovalRequired` decision calls; the first-party tool executor is that
component. It is deliberately not recorded inside `SecurityAuthority`. The
coordinator authorizes every one of its own journal writes through the security
authority, so an authority that itself called the coordinator would make the two
construct each other (a service-graph cycle the composition rules forbid, and
one that deadlocks the container) and would let a deferred journal write journal
its own wait. Consumers that observe a deferral and want a durable wait inject
the recorder; the authority never does.

**Tool-call recording and the journal.** The `agentkit.loop.tool_call` boundary
and the tool runtime's `IToolCallRecorder` write different facts to different
stores, and neither repeats the other. The journal's `ToolCallRecorded`
checkpoint is a manifest of identities written before a requested call enters
the executor, so recovery can tell a call that never reached the tool pipeline
from one that did. The accepted `ToolCallAcceptedSessionEntry` is the call's
authoritative acceptance fact in the session record: it is committed after
authorization and immediately before the invoker starts, and carries the
resolved tool, declared effects, external idempotency key, grant identity, and
validated-argument fingerprint. A `ToolCallTerminalSessionEntry` closes it. Read
together, an accepted entry with no terminal entry is the "effect may have
started" evidence this document's recovery table reconciles, and a journal
checkpoint with no accepted entry proves the call was rejected or never
authorized. The recorder never writes through the journal and the journal never
writes session entries, so the coordinator's own authorization remains acyclic.

Useful checkpoints occur after admission and promotion, context manifest
creation, provider terminal validation, tool-call recording, every terminal tool
result, message commit, compaction activation, and settlement. High- frequency
stream deltas are not required to reconstruct stable state.

## Recovery

Recovery follows evidence. Work definitely not started may begin. An operation
with a supported idempotency key may be reconciled or retried. A terminal result
whose commit is missing may be committed without reinvocation. An operation
whose evidence already shows a settled state (`Completed` or `Faulted`) recovers
as its recorded result and writes nothing: re-stamping a committed terminal
record under a newer fencing generation would ask the journal to replace it with
a different one. Work accepted by an external durable owner resumes through that
owner.

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
