# Budgets and limits

**Role:** Provide one hierarchical, atomic authority for reserving and
accounting finite work across a run and its child operations.

The normative behavior is defined by
[usage limits and budgets](../concepts/usage-limits-and-budgets.md).
AgentKit.Budgets supplies the first-party implementation. Neutral identities,
dimensions, requests, reservations, usage, and outcomes live in
AgentKit.Abstractions.

## Ownership

The budget authority owns hierarchy, atomic reservation, release, commitment,
provider corrections, soft-limit notification, and hard-limit outcomes. The loop
decides what to do after a limit result; providers, tools, retrieval,
compaction, goals, hooks, and evaluation reserve their own expected work before
starting it.

No component keeps a private counter for a shared dimension. Specialized
policies may calculate estimates, but the common authority performs the atomic
reservation. Goal and operation budgets are child scopes, not unrelated copies.

`RunUsage`, `UsageAccountingEntry`, and their measurement/provenance values live
in `AgentKit.Abstractions`. They provide the immutable run-result and progress
projection defined by
[usage accounting](../concepts/usage-limits-and-budgets.md#run-usage-projection).
They do not reserve capacity or replace the budget authority. The session owner
retains append-only usage revisions; publishers expose captured projections.

## Normative minimal contract shape

Every named type belongs in its own matching source file.

```csharp
namespace AgentKit;

public readonly record struct BudgetScopeId(Guid Value);
public readonly record struct BudgetReservationId(Guid Value);
public readonly record struct BudgetProfileKey(string Value);
public readonly record struct BudgetProfileVersion(long Value);
public readonly record struct BudgetPolicyKey(string Value);
public readonly record struct BudgetDimension(string Value);
public readonly record struct BudgetUnit(string Value);

public enum BudgetAggregationKind
{
    Sum,
    Maximum,
    ConcurrentGauge,
    Duration
}

public sealed record BudgetDimensionDescriptor(
    BudgetDimension Dimension,
    BudgetAggregationKind Aggregation,
    ImmutableHashSet<BudgetUnit> AllowedUnits);

public static class BudgetDimensions
{
    public static BudgetDimension Turns { get; } = new("agentkit.turns");
    public static BudgetDimension Steps { get; } = new("agentkit.steps");
    public static BudgetDimension ModelRequests { get; } = new("agentkit.model.requests");
    public static BudgetDimension ConcurrentModelRequests { get; } = new("agentkit.model.concurrent_requests");
    public static BudgetDimension InputTokens { get; } = new("agentkit.tokens.input");
    public static BudgetDimension OutputTokens { get; } = new("agentkit.tokens.output");
    public static BudgetDimension ReasoningTokens { get; } = new("agentkit.tokens.reasoning");
    public static BudgetDimension CachedReadTokens { get; } = new("agentkit.tokens.cached_read");
    public static BudgetDimension CachedWriteTokens { get; } = new("agentkit.tokens.cached_write");
    public static BudgetDimension PerRequestInputTokens { get; } = new("agentkit.request.input_tokens");
    public static BudgetDimension PerRequestOutputTokens { get; } = new("agentkit.request.output_tokens");
    public static BudgetDimension Cost { get; } = new("agentkit.cost");
    public static BudgetDimension AttemptedToolCalls { get; } = new("agentkit.tools.attempted");
    public static BudgetDimension SuccessfulToolCalls { get; } = new("agentkit.tools.successful");
    public static BudgetDimension ConcurrentToolCalls { get; } = new("agentkit.tools.concurrent");
    public static BudgetDimension ToolRetries { get; } = new("agentkit.tools.retries");
    public static BudgetDimension OutputValidationRetries { get; } = new("agentkit.output.validation_retries");
    public static BudgetDimension Delegations { get; } = new("agentkit.delegations");
    public static BudgetDimension ConcurrentDelegations { get; } = new("agentkit.delegations.concurrent");
    public static BudgetDimension RunElapsedTime { get; } = new("agentkit.time.run");
    public static BudgetDimension OperationElapsedTime { get; } = new("agentkit.time.operation");
    public static BudgetDimension ContextBytes { get; } = new("agentkit.context.bytes");
    public static BudgetDimension ContextTokens { get; } = new("agentkit.context.tokens");
    public static BudgetDimension RetainedMediaBytes { get; } = new("agentkit.context.media_bytes");
    public static BudgetDimension QueuedInputCount { get; } = new("agentkit.queue.inputs");
    public static BudgetDimension QueuedInputBytes { get; } = new("agentkit.queue.bytes");
    public static BudgetDimension QueuedInputAge { get; } = new("agentkit.queue.age");
    public static BudgetDimension EventBytes { get; } = new("agentkit.events.bytes");
    public static BudgetDimension ResultBytes { get; } = new("agentkit.results.bytes");
    public static BudgetDimension ToolResultBytes { get; } = new("agentkit.tools.result_bytes");
    public static BudgetDimension RetrievedItemCount { get; } = new("agentkit.retrieval.items");
    public static BudgetDimension RetrievedBytes { get; } = new("agentkit.retrieval.bytes");
    public static BudgetDimension BufferedStreamBytes { get; } = new("agentkit.stream.buffered_bytes");
    public static BudgetDimension ArtifactBytes { get; } = new("agentkit.artifacts.bytes");
}

public enum BudgetLimitKind
{
    Soft,
    Hard
}

public sealed record BudgetLimit(
    BudgetDimension Dimension,
    decimal Value,
    BudgetUnit Unit,
    BudgetLimitKind Kind);

public sealed record BudgetScopeAddress(
    TenantId TenantId,
    PrincipalId PrincipalId,
    AgentId AgentId,
    SessionId? SessionId,
    RunId? RunId,
    OperationId? OperationId);

public sealed record BudgetReservationRequest(
    BudgetScopeId ScopeId,
    BudgetDimension Dimension,
    decimal Amount,
    BudgetUnit Unit,
    OperationId OperationId,
    DateTimeOffset? ExpiresAt,
    IdempotencyKey IdempotencyKey);

public abstract record BudgetReservationResult;

public sealed record BudgetReserved(IBudgetReservation Reservation)
    : BudgetReservationResult;

public sealed record BudgetRejected(BudgetLimitFailure Failure)
    : BudgetReservationResult;

public abstract record BudgetStartResult;

public sealed record BudgetStarted : BudgetStartResult;

public sealed record BudgetStartRejected(BudgetLimitFailure Failure)
    : BudgetStartResult;

public abstract record BudgetBatchReservationResult;

public sealed record BudgetBatchReserved(
    ImmutableArray<IBudgetReservation> Reservations)
    : BudgetBatchReservationResult;

public sealed record BudgetBatchRejected(BudgetLimitFailure Failure)
    : BudgetBatchReservationResult;

public interface IBudgetReservation : IAsyncDisposable
{
    BudgetReservationId Id { get; }
    BudgetScopeId ScopeId { get; }
    BudgetDimension Dimension { get; }
    decimal Reserved { get; }

    ValueTask<BudgetStartResult> MarkStartedAsync(
        CancellationToken cancellationToken = default);

    ValueTask<BudgetCommitResult> CommitAsync(
        decimal actual,
        CancellationToken cancellationToken = default);
}

public interface IBudgetScope
{
    BudgetScopeId Id { get; }
    BudgetScopeAddress Address { get; }

    ValueTask<BudgetReservationResult> ReserveAsync(
        BudgetReservationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
        ImmutableArray<BudgetReservationRequest> requests,
        CancellationToken cancellationToken = default);

    ValueTask<BudgetSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}

public interface IRunBudget : IBudgetScope;

public sealed record BudgetExecutionCapability(
    BudgetProfileKey ProfileKey,
    BudgetProfileVersion ProfileVersion,
    ExecutionIdentity Identity,
    OperationCorrelation Correlation,
    IBudgetScope Scope);

public interface IBudgetAuthority
{
    ValueTask<BudgetScopeResult> CreateChildScopeAsync(
        BudgetScopeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IBudgetDimensionCatalog
{
    bool TryGet(
        BudgetDimension dimension,
        [NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor);
}
```

Aggregation applies to amounts in the dimension's declared unit, not to ledger
row count. `Sum` and `Duration` add live reservations and committed actuals.
`Maximum` observes the maximum across live reserved amounts, committed actual
amounts, and candidate reservations. `ConcurrentGauge` adds the amounts of all
live capacity-retaining reservations and records zero committed usage after
proven completion or release. One gauge reservation of three slots therefore
uses the same capacity as three simultaneous one-slot reservations. An adapter
cannot substitute sum aggregation for another kind as a conservative fallback;
that changes the configured capacity semantics.

Ledger aggregates use `BudgetQuantity`, a canonical nonnegative base-ten value
with an arbitrary-size coefficient and at most 28 fractional digits. Addition,
comparison, equality, hashing, and formatting remain exact even after a total
exceeds `decimal` magnitude or precision. Individual request, reservation,
commit, correction, and configured-limit values remain `decimal` at their
existing caller boundaries; the ledger converts each row exactly before
aggregation. A decimal projection of an aggregate is an explicit compatibility
operation that fails on required rounding or overflow and never clamps the
recorded value.

This intentionally changes `BudgetDimensionUsage.Reserved`,
`BudgetDimensionUsage.Committed`, `BudgetLimitFailure.ObservedValue`, and
`BudgetLimitFailure.RequestedAmount` from `decimal` to `BudgetQuantity`.
Compatibility constructors still accept nonnegative decimal row values, while
consumers of aggregate properties must compare exact quantities or request a
checked decimal projection.

`BudgetStartExpired` is an additive start-result subtype. Consumers that switch
exhaustively over the closed result family must handle expiration separately
from `BudgetStartRejected`, which continues to carry only genuine budget-limit
evidence. The legacy in-memory reservation now throws
`InvalidOperationException` when start follows explicit release instead of
returning a fabricated `BudgetStartRejected`; callers that depended on that
misleading result must treat the state transition as invalid.

Disposal releases an unstarted reservation exactly once. Immediately before
starting charged work, its owner calls `MarkStartedAsync`; failure starts no
work. An unstarted reservation whose persisted deadline has passed returns
`BudgetStartExpired` with its reservation identity and effective expiry; it does
not fabricate a numeric hard-limit failure. Exact retries replay that expiration
receipt, while an explicitly released reservation remains an invalid start
state. A started reservation is committed with known actual usage or retained as
unresolved accounting. Disposal, timeout, lease expiry, and process loss do not
refund possibly consumed work. Reconciliation releases it only with evidence of
non-consumption, records an explicitly estimated charge, or keeps it unresolved
under the captured bounded reconciliation policy. A live concurrency gauge is
released only when work stops or its ownership and capacity are transferred.

Committing records actual usage and releases only proven excess. An actual value
above the reservation remains fully recorded, marks the hard ceiling exceeded,
sets available capacity to zero, and stops new work; it is never clamped to the
reservation or rejected as though the consumption did not occur. Corrections
identify the original attempt and adjustment revision. They replace provisional
accounting without double-counting and may reduce a measured aggregate; the
ledger sequence, rather than every numeric total, is monotonic.

`ReserveBatchAsync` atomically reserves all requested dimensions across all
enforced ancestors or reserves none. Requests must be initialized, nonempty,
finite, compatible in units, and share the scope and logical operation; item
idempotency keys must be unique. An equivalent replay returns the same
reservations, while changed content is a conflict. This is the tool batch
preflight boundary: independent `ReserveAsync` calls cannot prove all-or-none
admission. A consumer must not begin any effect before batch acceptance.

The ledger binds each item key to the full ordered batch fingerprint and returns
reservations in request order. Reusing a batch item in a different batch or an
independent reservation is a conflict; replay cannot splice an old reservation
into newly admitted work. All batch members name the batch operation, while the
owning executor records their assignment to individual calls separately.

Each charged dimension has one reservation owner. The loop owns turn admission;
the provider executor owns individual provider attempts; the tool executor owns
batch and per-call reservations. Passing a reservation to child work transfers
its use or subdivides its capacity explicitly, rather than charging the same
work again. A child scope attenuates the parent's capacity; it does not copy a
fresh allowance.

`BudgetExecutionCapability` is an invocation-only binding to the exact profile,
identity, operation correlation, and scope selected for one operation. It is
never serialized or retained by a consumer. Provider, tool, retrieval, output,
and delegation operations accept this capability explicitly, validate that its
scope address agrees with their request, reserve before each attempt, and commit
or durably transfer unresolved accounting before returning. Out-of-run work
receives a child operation scope from `IBudgetAuthority`; it never fabricates an
`IRunBudget`.

The capability validates its binding before it can be used. Profile identity,
positive profile version, scope identity, tenant, principal, and operation must
be initialized. The address's tenant and principal match the authenticated
identity; its operation matches `OperationCorrelation.OperationId`. Agent and
optional session identity remain part of the address and are checked against the
consumer's request. A valid parent ledger address may omit run or operation
identity, but an invocation capability always names its operation.

| Correlation stage | Required address binding                                       |
| ----------------- | -------------------------------------------------------------- |
| `BeforeRun`       | `RunId` is absent; an established session may be present.      |
| `InRun`           | `RunId` equals the correlation's active run.                   |
| `AfterRun`        | `RunId` is absent; `CausalRunId` remains correlation evidence. |

New work after settlement receives an authorized operation child scope under a
live non-run parent. It cannot reopen the settled run's allowance by copying
`CausalRunId` into the budget address. A late usage correction identifies and
reconciles the original reservation through the ledger; it is not a new
reservation against the old run. The component that creates a scope owns its
lifetime and reconciliation. Capability consumers borrow it for the invocation
and never dispose it or retain it in a singleton.

The operation initiator obtains the non-run parent from the selected
`IBudgetAuthority` using its captured profile, authenticated identity, and
established agent/session binding. The authority validates parent ownership and
liveness when admitting the child, and the ledger enforces live ancestor state
at reservation. A caller-supplied parent identifier or historical capability is
insufficient to bypass that validation. Every established agent/session
partition must agree with the new request; absence of a causal run from the
address does not permit moving work to another partition. Closed or revoked
parents reject admission or reservation before work starts. Any protected effect
still requires its separately selected security authority and grant; budget
capacity does not grant permission.

```csharp
namespace AgentKit;

public sealed record BudgetProfileSnapshot(
    BudgetProfileKey Key,
    BudgetProfileVersion Version,
    ImmutableArray<BudgetLimit> Limits,
    ImmutableArray<BudgetPolicyKey> Policies,
    ContentHash ConfigurationFingerprint);

public interface IBudgetLedger
{
    ValueTask<BudgetLedgerReservationResult> ReserveAsync(
        BudgetLedgerReservationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<BudgetLedgerSettlementResult> SettleAsync(
        BudgetLedgerSettlementRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(
        BudgetLedgerReconciliationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IBudgetProfileCatalog
{
    bool TryGet(
        BudgetProfileKey key,
        [NotNullWhen(true)] out BudgetProfileSnapshot? profile);
}

public interface IBudgetPolicyCatalog
{
    bool TryGet(
        BudgetPolicyKey key,
        [NotNullWhen(true)] out IBudgetPolicy? policy);
}

public interface IBudgetPolicy
{
    ValueTask<BudgetPolicyDecision> EvaluateAsync(
        BudgetPolicyRequest request,
        CancellationToken cancellationToken = default);
}

public interface IBudgetEventDispatcher
{
    ValueTask<BudgetEventDispatchResult> PublishAsync(
        BudgetEvent budgetEvent,
        CancellationToken cancellationToken = default);
}
```

Ledger reserve and settlement are atomic across every enforced parent and
idempotent by their typed mutation key. Reconciliation never guesses effect
completion. Catalogs expose immutable snapshots and exact keyed policies; they
do not resolve services. Required budget/audit event delivery fails closed,
while explicitly best-effort telemetry cannot change accounting.

## First-party implementation and DI

```csharp
namespace AgentKit.Budgets;

internal sealed class BudgetAuthority(
    IBudgetLedger ledger,
    IBudgetProfileCatalog profiles,
    IBudgetPolicyCatalog policies,
    IBudgetDimensionCatalog dimensions,
    IBudgetEventDispatcher events,
    TimeProvider timeProvider,
    IIdentifierGenerator<BudgetScopeId> scopeIds,
    IIdentifierGenerator<BudgetReservationId> reservationIds,
    AgentBudgetOptionsSnapshot options)
    : IBudgetAuthority
{
}

internal sealed record AgentBudgetOptionsSnapshot(
    int MaximumScopeDepth,
    int MaximumOpenReservationsPerScope,
    TimeSpan DefaultReservationLifetime,
    BudgetUnknownCostBehavior UnknownCostBehavior,
    BudgetOverrunBehavior OverrunBehavior);

public enum BudgetUnknownCostBehavior
{
    AllowOnlyWithoutCostLimit,
    Reject
}

public enum BudgetOverrunBehavior
{
    RecordAndBlockFurtherReservations,
    RequireOperatorReconciliation
}

public sealed class AgentBudgetOptions
{
    public int MaximumScopeDepth { get; set; } = 16;
    public int MaximumOpenReservationsPerScope { get; set; } = 256;
    public TimeSpan DefaultReservationLifetime { get; set; } =
        TimeSpan.FromMinutes(5);
    public BudgetUnknownCostBehavior UnknownCostBehavior { get; set; } =
        BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit;
    public BudgetOverrunBehavior OverrunBehavior { get; set; } =
        BudgetOverrunBehavior.RecordAndBlockFurtherReservations;
}

public sealed class BudgetProfileOptions
{
    public BudgetProfileVersion Version { get; set; } = new(1);
    public List<BudgetLimit> Limits { get; set; } = [];
    public List<BudgetPolicyKey> Policies { get; set; } = [];
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentBudgets(
            Action<AgentBudgetOptions>? configure = null) =>
            BudgetRegistration.AddDefault(services, configure);

        public IServiceCollection AddBudgetProfile(
            BudgetProfileKey key,
            Action<BudgetProfileOptions> configure) =>
            BudgetRegistration.AddProfile(services, key, configure);

        public IServiceCollection ReplaceBudgetProfile(
            BudgetProfileKey key,
            Action<BudgetProfileOptions> configure) =>
            BudgetRegistration.ReplaceProfile(services, key, configure);

        public IServiceCollection ReplaceBudgetAuthority<TAuthority>()
            where TAuthority : class, IBudgetAuthority =>
            BudgetRegistration.ReplaceAuthority<TAuthority>(services);

        public IServiceCollection ReplaceBudgetLedger<TLedger>()
            where TLedger : class, IBudgetLedger =>
            BudgetRegistration.ReplaceLedger<TLedger>(services);

        public IServiceCollection ReplaceBudgetProfileCatalog<TCatalog>()
            where TCatalog : class, IBudgetProfileCatalog =>
            BudgetRegistration.ReplaceProfileCatalog<TCatalog>(services);

        public IServiceCollection ReplaceBudgetPolicyCatalog<TCatalog>()
            where TCatalog : class, IBudgetPolicyCatalog =>
            BudgetRegistration.ReplacePolicyCatalog<TCatalog>(services);

        public IServiceCollection ReplaceBudgetDimensionCatalog<TCatalog>()
            where TCatalog : class, IBudgetDimensionCatalog =>
            BudgetRegistration.ReplaceDimensionCatalog<TCatalog>(services);

        public IServiceCollection ReplaceBudgetEventDispatcher<TDispatcher>()
            where TDispatcher : class, IBudgetEventDispatcher =>
            BudgetRegistration.ReplaceEventDispatcher<TDispatcher>(services);

        public IServiceCollection AddBudgetPolicy<TPolicy>(
            BudgetPolicyKey key)
            where TPolicy : class, IBudgetPolicy =>
            BudgetRegistration.AddPolicy<TPolicy>(services, key);

        public IServiceCollection ReplaceBudgetPolicy<TPolicy>(
            BudgetPolicyKey key)
            where TPolicy : class, IBudgetPolicy =>
            BudgetRegistration.ReplacePolicy<TPolicy>(services, key);

        public IServiceCollection AddBudgetDimension(
            BudgetDimensionDescriptor descriptor) =>
            BudgetRegistration.AddDimension(services, descriptor);

        public IServiceCollection ReplaceBudgetDimension(
            BudgetDimensionDescriptor descriptor) =>
            BudgetRegistration.ReplaceDimension(services, descriptor);
    }
}
```

The storage contract captures overrun policy independently on every admitted
scope boundary. Truthful settlement creates a composite hold generation from the
exact boundary reference, triggering reservation reference, and positive
accounting revision. Admission blocked by such state returns a dedicated typed
held result with exact hold facts rather than throwing a state exception or
inventing a `BudgetLimitFailure`.

Automatic reconciliation clears boundary holds only when no row currently
overruns that boundary and dimension and exact aggregate accounting is within
all applicable finite hard ceilings. Operator policy retains the generation
until the same eligibility test passes and an exact audited resolution commits.
The resolution receipt is immutable under replay, and its old composite
reference has no authority over a later cleared-then-overrun generation.
`IBudgetLedger` persists and structurally checks security-enforcement receipt
binding as audit evidence; the later runtime resolver must use the selected
security authority to authenticate and consume permission before calling this
authorization-neutral storage mutation.

The authority, ledger, immutable dimension/profile/policy catalogs, and event
dispatcher are thread-safe process singletons. Host, tenant, principal, agent,
session, run, and operation ownership are addresses inside that shared ledger,
not DI scopes for competing authorities. A run receives one owned `IRunBudget`
child handle in its compiled plan; child scopes and reservation handles are
run/operation-owned and asynchronously disposed, and no singleton captures them.
Concrete ledgers are explicit leaf packages. `AgentKit.Budgets.InMemory`
provides process-local atomic accounting; `AgentKit.Budgets.Sqlite` provides
durable local accounting; a distributed leaf must provide authoritative fencing
and atomic compare-and-reserve semantics across every enforced parent. The
common adapters run the same ledger conformance suite, but each advertises only
its actual durability and ownership domain.

`IBudgetLedger.Descriptor` is required immutable, side-effect-free composition
evidence. Adding it is an intentional source and binary compatibility change for
ledger implementers, which declare durability independently from the
process-local, host-local, or distributed concurrency domain. The distributed
domain includes authoritative fencing; it does not imply survival after loss of
the whole domain. Reading a descriptor never opens, probes, initializes, or
migrates storage.

`AddAgentBudgets` is idempotent and `TryAdd`s one singular, replaceable
authority, hierarchy policy, profile catalog, and event dispatcher. It expects a
ledger through explicit DI composition but registers no concrete ledger. The
host adds one ledger leaf and its persistence target. The runtime validates and
copies binding options into an immutable `AgentBudgetOptionsSnapshot`.
`AddBudgetProfile` publishes an immutable named profile selected by
`AgentComponentSelection.BudgetProfile`; its ordered policy keys are resolved
once when the run plan is compiled. Empty limits mean that only the
definition/run ceilings apply, not unlimited retries. First-party dimension
descriptors are registered under the stable `agentkit.*` names above; extensions
use their own stable namespace and declare aggregation and legal units. No
currency conversion, price, tenant quota, durable ledger, or credential is
invented. Duplicate profile, policy, or dimension keys, incompatible units, and
conflicting policies fail build unless the matching replacement API names that
exact axis.

## Dependency direction and cycle prevention

AgentKit.Budgets depends on AgentKit.Abstractions and shared diagnostic
infrastructure. It does not depend on the loop, providers, tools, goals,
context, sessions, or evaluation. Those consumers receive `IBudgetScope` or
`IRunBudget` through their operation context or the compiled run plan.

Budget events are immutable observations. A budget sink cannot call back into
the authority during delivery. Hooks may tighten a proposed amount before the
reservation boundary but cannot commit, release, or widen a limit. This keeps
the service graph one-way: consumer → budget abstraction → ledger/policy/sinks.

### Loop consumption

`AgentDefinition.BudgetLimits` and `AgentRunRequest.BudgetLimits` carry a run's
limits; `AgentRunServices.Budgets` carries the authority the run scope resolved.
When limits are declared, `DefaultAgentLoop` creates one run scope addressed by
tenant, principal, agent, session, and run, then reserves before it commits to
each turn (`agentkit.turns`), model request (`agentkit.model.requests`), and
tool call (`agentkit.tools.attempted`), and accounts provider-reported input,
output, and reasoning tokens and USD cost after each response. A refused or held
reservation settles the run as `AgentRunBudgetExhausted` naming the dimension; a
refused tool-call reservation settles that call as a rejected result with
`ToolTerminalStatus.ResourceLimitExceeded`. A budgeted request without a
composed authority fails closed. Pre-effect estimation of unknown token cost,
parent host/tenant scopes, and named budget profiles remain to be wired;
`AgentKit.Simple.WithBudget` composes the authority, the in-memory ledger when
no other is registered, and the default agent's limits.

## Validation and unsupported behavior

Composition rejects incompatible units, negative values, hierarchy cycles, child
limits wider than hard parents, non-atomic ledgers used with concurrent
reservations, singleton capture of run state, and missing identifier or time
providers. Unknown cost is not zero. A hard cost-only policy with unknown price
rejects or requires a secondary request/token limit according to explicit
policy.

An in-memory ledger cannot satisfy crash-safe or multi-process accounting. A
SQLite ledger must atomically reserve across the complete requested dimension
set and retain started reservations, settlements, corrections, idempotency, and
ordering through restart. SQLite remains one durable local ownership domain; it
does not claim distributed fencing or atomic settlement with a provider charge.
Composition rejects a profile whose requested durability or concurrency exceeds
the selected ledger descriptor.

## Related architecture

- [Agent runtime](agent-runtime.md)
- [Model and embedding providers](model-and-embedding-providers.md)
- [Tools](tools.md)
- [Goals and delegation](goals-and-delegation.md)
- [Context compaction](context-compaction.md)
- [Testing and evaluation](testing-and-evaluation.md)
