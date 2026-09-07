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

public interface IBudgetReservation : IAsyncDisposable
{
    BudgetReservationId Id { get; }
    BudgetScopeId ScopeId { get; }
    BudgetDimension Dimension { get; }
    decimal Reserved { get; }

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

Disposing an uncommitted reservation releases it exactly once. Committing
records actual usage and releases any excess; an actual value above the
reservation follows the configured overrun policy and never silently makes a
hard limit negative. Provider corrections identify the original request so they
replace provisional accounting rather than double-count it.

`BudgetExecutionCapability` is an invocation-only binding to the exact profile,
identity, operation correlation, and scope selected for one operation. It is
never serialized or retained by a consumer. Provider, tool, retrieval, output,
and delegation operations accept this capability explicitly, validate that its
scope address agrees with their request, reserve before each attempt, and commit
or release before returning. Out-of-run work receives a child operation scope
from `IBudgetAuthority`; it never fabricates an `IRunBudget`.

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

The authority, ledger, immutable dimension/profile/policy catalogs, and event
dispatcher are thread-safe process singletons. Host, tenant, principal, agent,
session, run, and operation ownership are addresses inside that shared ledger,
not DI scopes for competing authorities. A run receives one owned `IRunBudget`
child handle in its compiled plan; child scopes and reservation handles are
run/operation-owned and asynchronously disposed, and no singleton captures them.
Durable or distributed ledgers are explicit leaf packages and must provide
atomic compare-and-reserve semantics across every enforced parent.

`AddAgentBudgets` is idempotent and `TryAdd`s one singular, replaceable
authority, hierarchy policy, profile catalog, event dispatcher, and in-memory
ledger. It validates and copies binding options into an immutable
`AgentBudgetOptionsSnapshot`. `AddBudgetProfile` publishes an immutable named
profile selected by `AgentComponentSelection.BudgetProfile`; its ordered policy
keys are resolved once when the run plan is compiled. Empty limits mean that
only the definition/run ceilings apply, not unlimited retries. First-party
dimension descriptors are registered under the stable `agentkit.*` names above;
extensions use their own stable namespace and declare aggregation and legal
units. No currency conversion, price, tenant quota, durable ledger, or
credential is invented. Duplicate profile, policy, or dimension keys,
incompatible units, and conflicting policies fail build unless the matching
replacement API names that exact axis.

## Dependency direction and cycle prevention

AgentKit.Budgets depends only on AgentKit.Abstractions. It does not depend on
the loop, providers, tools, goals, context, sessions, or evaluation. Those
consumers receive `IBudgetScope` or `IRunBudget` through their operation context
or the compiled run plan.

Budget events are immutable observations. A budget sink cannot call back into
the authority during delivery. Hooks may tighten a proposed amount before the
reservation boundary but cannot commit, release, or widen a limit. This keeps
the service graph one-way: consumer → budget abstraction → ledger/policy/sinks.

## Validation and unsupported behavior

Composition rejects incompatible units, negative values, hierarchy cycles, child
limits wider than hard parents, non-atomic ledgers used with concurrent
reservations, singleton capture of run state, and missing identifier or time
providers. Unknown cost is not zero. A hard cost-only policy with unknown price
rejects or requires a secondary request/token limit according to explicit
policy.

## Related architecture

- [Agent runtime](agent-runtime.md)
- [Model and embedding providers](model-and-embedding-providers.md)
- [Tools](tools.md)
- [Goals and delegation](goals-and-delegation.md)
- [Context compaction](context-compaction.md)
- [Testing and evaluation](testing-and-evaluation.md)
