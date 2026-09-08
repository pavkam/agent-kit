# Goals and delegation

**Role:** Represent intended outcomes and delegated work as durable domain
state.

[Goals are durable domain state](../concepts/goals-and-multi-agent-delegation.md),
not prompt prefixes. They have stable identity, ownership, status, version,
budget, attempts, and evidence. Their transitions are part of the session record
and follow the same authorization, queueing, settlement, and recovery rules as
the rest of AgentKit.

AgentKit.Goals owns the first-party goal coordinator and delegation policies.
Goal values, stores, messages, leases, and join-policy contracts live in
AgentKit.Abstractions. The package remains optional until an application
registers goal behavior.

The coding-harness task tool uses a protected adapter boundary:
`DefaultTaskDelegationBroker` consumes the exact `Delegation/Create` grant
before forwarding a grant-free `TaskDelegationPrompt` to an explicitly selected
`ITaskDelegationChannel`. These `TaskDelegation*` contracts are intentionally
adapter-specific and do not claim to be the complete coordinator contracts
below. A channel is responsible for resolving an active parent goal, narrowing
authority and the child tool catalog, reserving budget, idempotently creating
durable child state, deterministic joining, and terminal settlement. No channel
is registered implicitly because those application facts cannot be fabricated.

`AgentEngine` is one process-level host for many `AgentDefinition` instances,
not one agent. Goal state and every target-selection decision therefore carry
typed `AgentId`, `SessionId`, `RunId`, and `OperationId`. Local delegation asks
the same engine to run another registered agent definition through public
runtime contracts; it never constructs a nested engine or resolves a service
provider as a locator.

## Normative minimal contract shape

The following C# 14 shapes are normative and minimal rather than exhaustive.
Each named type lives in its own file in AgentKit.Abstractions. Shared
`AgentId`, `SessionId`, `RunId`, `OperationId`,
`IIdentifierGenerator<TIdentifier>`, `VersionToken`, `IdempotencyKey`, security,
budget, and input-admission contracts are reused.

```csharp
namespace AgentKit;

public readonly record struct GoalId(Guid Value);

public readonly record struct GoalAttemptId(Guid Value);

public readonly record struct DelegationId(Guid Value);

public readonly record struct GoalStoreKey(string Value);

public readonly record struct DelegationDispatcherKey(string Value);

public readonly record struct GoalJoinStrategyKey(string Value);

public readonly record struct GoalProfileKey(string Value);

public readonly record struct GoalProfileVersion(long Value);
```

Goal, attempt, delegation, child-session, run, and operation identities come
from injected generators. State transitions use `TimeProvider`.

```csharp
namespace AgentKit;

public sealed record AgentGoal(
    GoalId Id,
    GoalId? ParentId,
    AgentId OwnerAgentId,
    SessionId SessionId,
    RunId OriginatingRunId,
    GoalProfileKey ProfileKey,
    GoalProfileVersion ProfileVersion,
    AgentDefinitionRevision AgentDefinitionRevision,
    GoalStatus Status,
    GoalDefinition Definition,
    GoalBudget Budget,
    GoalAttemptId? ActiveAttemptId,
    VersionToken Version,
    DateTimeOffset CreatedAt,
    ExtensionData Extensions);

public sealed record GoalAttempt(
    GoalAttemptId Id,
    GoalId GoalId,
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    int Number,
    GoalAttemptStatus Status,
    GoalBudgetReservation Reservation,
    GoalOutcomeReference? Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

public sealed record GoalTransition(
    GoalId GoalId,
    AgentId OwnerAgentId,
    SessionId SessionId,
    RunId RunId,
    OperationId OperationId,
    GoalStatus From,
    GoalStatus To,
    TransitionActor Actor,
    GoalTransitionReason Reason,
    VersionToken ExpectedVersion,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset OccurredAt);

public sealed record DelegationRequest(
    DelegationId Id,
    GoalId ParentGoalId,
    GoalAttemptId ParentAttemptId,
    AgentId ParentAgentId,
    SessionId ParentSessionId,
    RunId ParentRunId,
    GoalProfileKey ProfileKey,
    GoalProfileVersion ProfileVersion,
    AgentDefinitionRevision AgentDefinitionRevision,
    SecurityAuthorizationContext Authorization,
    OperationId OperationId,
    AgentId TargetAgentId,
    GoalDefinition ChildGoal,
    AcceptanceCriteria AcceptanceCriteria,
    DelegationScope Scope,
    GoalBudgetReservation Budget,
    DateTimeOffset Deadline,
    DelegationCancellationMode CancellationMode,
    GoalJoinStrategyKey JoinStrategyKey,
    IdempotencyKey IdempotencyKey);

public abstract record DelegationResult(
    DelegationId Id,
    ExtensionData Extensions);

public sealed record DelegationRejected(
    DelegationId Id,
    DelegationRejection Rejection,
    ExtensionData Extensions) : DelegationResult(Id, Extensions);

public sealed record DelegationChildResult(
    DelegationId Id,
    GoalId ChildGoalId,
    AgentId ChildAgentId,
    SessionId ChildSessionId,
    GoalAttemptId? ChildAttemptId,
    RunId? ChildRunId,
    DelegationStatus Status,
    StructuredGoalResult? Result,
    ImmutableArray<EvidenceReference> Evidence,
    GoalBudgetUsage Usage,
    SideEffectCertainty SideEffectCertainty,
    ExtensionData Extensions) : DelegationResult(Id, Extensions);
```

`DelegationResult` is untrusted agent-produced data until result-schema and
evidence validation succeeds. A rejection before child creation returns
`DelegationRejected` and therefore cannot invent child identities.
`DelegationChildResult` is available only after the child goal and session have
been durably created; attempt and run identities remain absent until those
states actually exist. No child status transition can mutate its parent goal or
session directly.

### State and lifecycle coordination

```csharp
namespace AgentKit;

public interface IGoalStore
{
    GoalStoreDescriptor Descriptor { get; }

    ValueTask<GoalCreateResult> CreateAsync(
        GoalCreateRequest request,
        CancellationToken cancellationToken);

    ValueTask<GoalLoadResult> LoadAsync(
        GoalLoadRequest request,
        CancellationToken cancellationToken);

    ValueTask<GoalTransitionResult> TransitionAsync(
        GoalTransitionRequest request,
        CancellationToken cancellationToken);

    ValueTask<GoalPageResult> ReadChildrenAsync(
        GoalChildrenRequest request,
        CancellationToken cancellationToken);
}

public interface IGoalStoreSelector
{
    ValueTask<GoalStoreSelectionResult> SelectAsync(
        GoalStoreSelectionRequest request,
        CancellationToken cancellationToken);
}

public interface IGoalCoordinator
{
    ValueTask<GoalCreateResult> CreateAsync(
        GoalCreateRequest request,
        CancellationToken cancellationToken);

    ValueTask<GoalAttemptResult> StartAttemptAsync(
        GoalAttemptRequest request,
        CancellationToken cancellationToken);

    ValueTask<GoalTransitionResult> TransitionAsync(
        GoalTransitionRequest request,
        CancellationToken cancellationToken);
}
```

The store owns optimistic, idempotent durable state. Its requests contain the
agent/session/run/operation address and an exact `SecurityGrant`, which the
store validates again. The selector binds a configured store to the goal
profile. The coordinator owns lifecycle validation, authorization, and
events—not persistence mechanics.

### Delegation discovery, selection, policy, and execution

```csharp
namespace AgentKit;

public interface IDelegationTargetProvider
{
    ValueTask<DelegationTargetSnapshot> DiscoverAsync(
        DelegationDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IDelegationTargetCatalog
{
    ValueTask<DelegationTargetCatalogSnapshot> CaptureAsync(
        DelegationDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IDelegationTargetSelector
{
    ValueTask<DelegationTargetSelectionResult> SelectAsync(
        DelegationRequest request,
        DelegationTargetCatalogSnapshot snapshot,
        CancellationToken cancellationToken);
}

public interface IDelegationPolicy
{
    ValueTask<DelegationPolicyDecision> EvaluateAsync(
        DelegationRequest request,
        DelegationPolicyContext context,
        CancellationToken cancellationToken);
}

public interface IDelegationPolicyPipeline
{
    ValueTask<DelegationPolicyDecision> EvaluateAsync(
        DelegationRequest request,
        DelegationPolicyContext context,
        CancellationToken cancellationToken);
}

public interface IDelegationDispatcher
{
    DelegationDispatcherDescriptor Descriptor { get; }

    Task<DelegationResult> DispatchAsync(
        AuthorizedDelegation request,
        CancellationToken cancellationToken);
}

public interface IDelegationCoordinator
{
    Task<DelegationResult> DelegateAsync(
        DelegationRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);

    ValueTask<GoalJoinDecision> JoinAsync(
        GoalJoinRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);
}

public interface IGoalJoinStrategy
{
    GoalJoinStrategyKey Key { get; }

    ValueTask<GoalJoinDecision> EvaluateAsync(
        GoalJoinRequest request,
        CancellationToken cancellationToken);
}

public interface IGoalEventSink
{
    ValueTask PublishAsync(
        GoalEvent goalEvent,
        CancellationToken cancellationToken);
}

public interface IGoalEventDispatcher
{
    ValueTask PublishAsync(
        GoalEvent goalEvent,
        CancellationToken cancellationToken);
}
```

Target providers are additive discovery sources for local registered agents or
remote workers. The catalog captures capabilities and definition versions. The
selector chooses; delegation policy intersects parent authority, resource/data
scope, and budget; the dispatcher performs one already-authorized handoff; and
join strategies decide deterministically from durable child results. Event sinks
observe immutable transitions only.

The local dispatcher commits an idempotent child-admission intent through the
goal/session contracts and returns its handoff receipt. It does not constructor-
depend on `AgentEngine`, `IAgentRunner`, or a callback that captures the engine.
A host-owned worker drains those intents through normal input admission and the
same engine's public runner. The reusable first-party hosting adapter belongs in
`AgentKit.Goals.Hosting`, an application leaf depending on Goals and the public
facade. The facade, loop, and Goals runtime never depend on that worker.

This separates the construction graph from the workflow graph:
`parent loop -> delegation dispatcher -> durable intent` and
`host worker -> public engine -> child run`. The worker activates after engine
readiness and replays an intent by its idempotency identity, without creating a
second child. A remote dispatcher follows the same durable handoff contract.
Every effecting dispatcher validates its own delegation grant; subsequent child
admission, state access, and communication obtain their own scoped grants.

The live caller supplies `HookDispatchContext` separately to the coordinator. It
is never persisted in `DelegationRequest`, `AgentGoal`, or a child result;
delayed dispatch without an active run passes `null` rather than reconstructing
or serializing a hook tracker.

AgentKit.Goals supplies sealed goal/delegation coordinators, target catalog and
selector, session-backed goal store, local dispatcher, and built-in join
strategies. The main coordinator dependencies are explicit:

```csharp
namespace AgentKit.Goals;

internal sealed class DelegationCoordinator(
    IGoalCoordinator goals,
    IDelegationTargetCatalog targetCatalog,
    IDelegationTargetSelector targetSelector,
    IDelegationPolicyPipeline policyPipeline,
    IDelegationDispatcherSelector dispatcherSelector,
    IGoalJoinStrategySelector joinStrategySelector,
    ISecurityAuthoritySelector securityAuthorities,
    IGoalBudgetManager budgets,
    IHookDispatcher hooks,
    IGoalEventDispatcher events,
    TimeProvider timeProvider,
    IIdentifierGenerator<DelegationId> delegationIds) : IDelegationCoordinator
{
}
```

There is no mandatory goal, dispatcher, or join-strategy base class. Direct
interface implementation is required; a leaf may provide a base only for proven
remote handoff or result-validation mechanics.

## Configuration and dependency injection

An agent definition selects a versioned `GoalProfileKey` containing its goal
store, discoverable target set, dispatcher, policy set, join strategies, limits,
budget source, and cancellation rules. The key, version, and agent-definition
revision are captured on every durable goal and delegation request. A delegation
also captures `SecurityAuthorizationContext`, so delayed dispatch selects the
same authority and security profile rather than the agent's latest definition.
Host options are hard ceilings. Parent or run values may reserve less authority
or budget but cannot widen the profile.

```csharp
namespace AgentKit.Goals;

public sealed class AgentGoalOptions
{
    public int MaximumDelegationDepth { get; set; } = 4;
    public int MaximumChildrenPerGoal { get; set; } = 8;
    public int MaximumConcurrentAttempts { get; set; } = 4;
    public GoalJoinStrategyKey DefaultJoinStrategy { get; set; } =
        GoalJoinStrategyKeys.All;
    public DelegationFailureMode FailureMode { get; set; } =
        DelegationFailureMode.SettleAllChildren;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentGoals(
            Action<AgentGoalOptions>? configure = null) =>
            GoalServiceRegistration.AddAgentGoals(
                services,
                configure);

        public IServiceCollection AddGoalProfile(
            GoalProfileKey key,
            Action<GoalProfileOptions> configure) =>
            GoalServiceRegistration.AddGoalProfile(services, key, configure);

        public IServiceCollection ReplaceGoalProfile(
            GoalProfileKey key,
            Action<GoalProfileOptions> configure) =>
            GoalServiceRegistration.ReplaceGoalProfile(
                services,
                key,
                configure);

        public IServiceCollection AddGoalStore<TStore>(GoalStoreKey key)
            where TStore : class, IGoalStore =>
            GoalServiceRegistration.AddGoalStore<TStore>(services, key);

        public IServiceCollection ReplaceGoalStore<TStore>(GoalStoreKey key)
            where TStore : class, IGoalStore =>
            GoalServiceRegistration.ReplaceGoalStore<TStore>(services, key);

        public IServiceCollection AddDelegationTargetProvider<TProvider>()
            where TProvider : class, IDelegationTargetProvider =>
            GoalServiceRegistration.AddDelegationTargetProvider<TProvider>(
                services);

        public IServiceCollection AddDelegationDispatcher<TDispatcher>(
            DelegationDispatcherKey key)
            where TDispatcher : class, IDelegationDispatcher =>
            GoalServiceRegistration.AddDelegationDispatcher<TDispatcher>(
                services,
                key);

        public IServiceCollection ReplaceDelegationDispatcher<TDispatcher>(
            DelegationDispatcherKey key)
            where TDispatcher : class, IDelegationDispatcher =>
            GoalServiceRegistration.ReplaceDelegationDispatcher<TDispatcher>(
                services,
                key);

        public IServiceCollection AddGoalJoinStrategy<TStrategy>(
            GoalJoinStrategyKey key)
            where TStrategy : class, IGoalJoinStrategy =>
            GoalServiceRegistration.AddGoalJoinStrategy<TStrategy>(services, key);

        public IServiceCollection ReplaceGoalJoinStrategy<TStrategy>(
            GoalJoinStrategyKey key)
            where TStrategy : class, IGoalJoinStrategy =>
            GoalServiceRegistration.ReplaceGoalJoinStrategy<TStrategy>(
                services,
                key);

        public IServiceCollection AddDelegationPolicy<TPolicy>(
            DelegationPolicyRegistration registration)
            where TPolicy : class, IDelegationPolicy =>
            GoalServiceRegistration.AddDelegationPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection ReplaceDelegationPolicy<TPolicy>(
            DelegationPolicyRegistration registration)
            where TPolicy : class, IDelegationPolicy =>
            GoalServiceRegistration.ReplaceDelegationPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection AddGoalEventSink<TSink>(
            GoalEventSinkRegistration registration)
            where TSink : class, IGoalEventSink =>
            GoalServiceRegistration.AddGoalEventSink<TSink>(
                services,
                registration);

        public IServiceCollection ReplaceGoalCoordinator<TCoordinator>()
            where TCoordinator : class, IGoalCoordinator =>
            GoalServiceRegistration.ReplaceGoalCoordinator<TCoordinator>(
                services);

        public IServiceCollection ReplaceGoalStoreSelector<TSelector>()
            where TSelector : class, IGoalStoreSelector =>
            GoalServiceRegistration.ReplaceGoalStoreSelector<TSelector>(
                services);

        public IServiceCollection
            ReplaceDelegationTargetCatalog<TCatalog>()
            where TCatalog : class, IDelegationTargetCatalog =>
            GoalServiceRegistration.ReplaceDelegationTargetCatalog<TCatalog>(
                services);

        public IServiceCollection
            ReplaceDelegationTargetSelector<TSelector>()
            where TSelector : class, IDelegationTargetSelector =>
            GoalServiceRegistration.ReplaceDelegationTargetSelector<TSelector>(
                services);

        public IServiceCollection
            ReplaceDelegationDispatcherSelector<TSelector>()
            where TSelector : class, IDelegationDispatcherSelector =>
            GoalServiceRegistration
                .ReplaceDelegationDispatcherSelector<TSelector>(services);

        public IServiceCollection
            ReplaceGoalJoinStrategySelector<TSelector>()
            where TSelector : class, IGoalJoinStrategySelector =>
            GoalServiceRegistration.ReplaceGoalJoinStrategySelector<TSelector>(
                services);

        public IServiceCollection ReplaceDelegationPolicyPipeline<TPipeline>()
            where TPipeline : class, IDelegationPolicyPipeline =>
            GoalServiceRegistration.ReplaceDelegationPolicyPipeline<TPipeline>(
                services);

        public IServiceCollection
            ReplaceDelegationCoordinator<TCoordinator>()
            where TCoordinator : class, IDelegationCoordinator =>
            GoalServiceRegistration.ReplaceDelegationCoordinator<TCoordinator>(
                services);

        public IServiceCollection ReplaceGoalBudgetManager<TManager>()
            where TManager : class, IGoalBudgetManager =>
            GoalServiceRegistration.ReplaceGoalBudgetManager<TManager>(
                services);

        public IServiceCollection ReplaceGoalEventDispatcher<TDispatcher>()
            where TDispatcher : class, IGoalEventDispatcher =>
            GoalServiceRegistration.ReplaceGoalEventDispatcher<TDispatcher>(
                services);
    }
}
```

The package-internal `GoalServiceRegistration` helper performs registrations
without building or resolving a service provider.

`AddAgentGoals` is idempotent and `TryAdd`s singular goal/delegation
coordinators, engine-wide store and dispatcher selectors, target
catalog/selector, budget manager, delegation-policy pipeline, default
deny-unless-authorized delegation policy, goal-event dispatcher, and built-in
join-strategy selector. The policy pipeline evaluates only the additive policies
selected by the captured profile in deterministic order. The documented
session-record goal store and local dispatcher are replaceable defaults; they
create no separate hidden history. The session-record store is a behavioral
projection over the explicitly selected session contracts, not a concrete
storage medium or persistence target; it therefore does not imply a separate
`AgentKit.Goals.Sqlite` database. An independently persistent goal-store
implementation remains an explicit leaf and must follow the common in-memory /
SQLite adapter policy. Explicit replacement methods replace singular axes.

Goal stores, target providers, dispatchers, policies, join strategies, and event
sinks are additive. Stores, dispatchers, profiles, and join strategies use typed
keys. Duplicate IDs or keys, policy-order cycles, or ambiguous targets fail
build unless an explicit replacement names the registration. Runtime services
receive selectors/catalogs, never a service provider.

The singleton coordinators capture `IGoalEventDispatcher`, never sink instances.
The dispatcher activates profile-selected sinks inside the current goal
operation scope according to each declared lifetime and disposes that activation
after delivery. A singleton sink must explicitly be thread-safe.

Catalogs, coordinators, and thread-safe stores may be singletons. Mutable goal
attempt, join, message, and budget state is durable or run-scoped. Dispatchers
declare whether they are singleton-safe; container-created clients are disposed
by the container. Different goals and agents may progress concurrently, while
one active attempt holds the goal's lease unless explicit parallel child work is
recorded. Cancellation propagates according to the captured relationship,
settles or durably hands off every child, and never erases completed effects.

## Composition validation and unsupported behavior

Goals remain optional. Once an agent selects a goal profile, validation requires
one effective coordinator and selectors, the referenced store and dispatcher,
available target definitions/capabilities, join strategies, budget manager,
security authority, session/input coordination, hook dispatcher, `TimeProvider`,
ID generators, and required audit/event delivery. It validates depth/concurrency
limits, captured goal-profile/version/definition references, target and store
keys, result schemas, lease/fencing requirements, policy order, budget
ownership, and dependency-graph acyclicity where statically knowable. Resume and
delayed joins use the persisted profile version rather than rediscovering the
latest agent definition.

An agent without a goal profile cannot create durable goals or delegate. Unknown
targets, missing capabilities, cyclic dependencies, exhausted budgets,
unsupported joins, stale leases, invalid child results, or unavailable durable
handoff return typed outcomes. Missing or widened authority, cross-agent/session
mutation, unresolved target ambiguity, and unavailable required audit fail
closed before child creation or communication. Completion timing never chooses a
winner unless the selected join strategy explicitly defines that deterministic
rule.

## Goal lifecycle

A goal moves explicitly through proposed, ready, active, waiting, completed,
failed, cancelled, or blocked states. Every transition records its actor,
reason, prior version, and time. Blocked means that progress requires external
authority or change; it does not mean the task is merely inconvenient.

Execution attempts have their own identities, assigned agent, associated runs,
budget usage, and outcomes. Retrying creates a new attempt without erasing old
evidence. Completion references a verified outcome rather than trusting an
assistant claim.

## Delegation

Delegation creates a child goal with a bounded objective, acceptance criteria,
required capabilities, data and resource scope, budget, deadline, cancellation
relationship, context references, and expected result shape.

A child receives the intersection of parent authority and its assigned scope.
Delegation cannot broaden permissions. The child uses the normal composition,
runtime, context, provider, tool, and permission components and cannot mutate
the parent's history or mark the parent complete.

## Communication and joins

Agent-to-agent communication enters through
[normal input admission](../concepts/input-admission-and-message-queues.md) with
sender, recipient, causal goal and attempt, delivery class, and idempotency
identity. Text remains content; routing and authority are typed metadata.

Independent child goals may run concurrently. The parent declares a join policy
such as all, first valid success, quorum, best effort, or a dependency graph.
The default all-results join orders children by their recorded delegation
ordinal. An ordinal-first-success policy waits until every earlier child is
terminally ineligible before selecting a later success. A fastest-valid-success
policy is a separate opt-in: it chooses the first eligible result in the durable
parent join-inbox sequence, records that winner once, and reuses it on replay.
It is timing-sensitive in live execution, though replay of its recorded evidence
is deterministic. Quorum and deadline joins similarly capture their cutoff and
ordered eligible result set before committing a decision. No join uses
unrecorded task completion order as recoverable state.

Child output is untrusted agent-produced data until the parent validates its
shape and evidence. The parent may accept, reject, or request revision.

A parent waiting for children must not hold a session mutation lock or an
executor permit that every child needs. It durably records the join, releases
only its active-worker/concurrency occupancy, and retains spent or reserved
child budget under the budget authority. Wake-up reacquires the parent lane for
the expected operation. Composition and admission reject a topology that cannot
make child progress within its declared capacity. A parent cannot wait for a
child that is queued behind that parent's own exclusive lane operation; local
children use separately admitted child sessions/lanes.

## Control and limits

Limits bound delegation depth, child count, concurrent attempts, messages, model
usage, tool usage, cost, and elapsed time. Budgets reserve from a parent or an
explicit shared pool. Cyclic dependencies are rejected.

Cancellation propagates according to the declared relationship. Already
performed effects remain truthful and visible. A child may be durably handed
off, but every attempt must eventually settle or require explicit external
action.

## Related concept specifications

- [Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md)
- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
- [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)
