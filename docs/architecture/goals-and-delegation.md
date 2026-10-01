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

The coding-harness `task` tool builds one canonical `DelegationRequest` and
hands it to `IDelegationCoordinator`. The coordinator owns the ordered gauntlet,
the exact `Delegation/Create` grant, the durable child goal, and the wait; the
tool authorizes nothing itself and no channel is registered implicitly, because
stores, dispatchers, and profiles are application facts that cannot be
fabricated.

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

## Implementation notes and recorded deviations

The shapes above are normative and minimal. The landed implementation refines
them as follows; each refinement narrows a contract or names a mechanism the
minimal shape left open, and none widens authority.

### Packages and dependency direction

- `AgentKit.Abstractions` holds every goal, delegation, join, event, policy,
  budget, parking, intent-signal, child-runner, and agent-message contract.
- `AgentKit.Goals` is the behavioral runtime (coordinators, registry, selectors,
  policy pipeline, budget manager, event dispatcher, join strategies, local
  dispatcher, session-backed store). It references only Abstractions and
  Observability.
- `AgentKit.Goals.InMemory`, `.Json`, and `.Sqlite` are store leaves. They share
  one pure reducer and planner compiled from the source-only folder
  `AgentKit.Goals.Storage.Shared` into each assembly as internal code, so the
  three adapters cannot drift; the durable leaves also compile
  `AgentKit.Goals.Storage.Durable` (document shapes over `AgentKit.Storage.Json`
  evidence). These folders are not packages and register nothing. All stores run
  the same conformance suite.
- `AgentKit.Goals.Hosting` is an application leaf depending on Abstractions,
  Observability, and the `AgentKit` facade. It supplies the worker, the slot
  pool and wait parking, the engine-backed child runner, and the engine-backed
  agent-message channel. The facade and the Goals runtime never reference it.

### Contracts

- `IGoalCoordinator` takes `...Command` values that carry the captured
  `SecurityAuthorizationContext` and obtains its own single-use grants from the
  captured authority; `IGoalStore` takes `...Request` values that carry the
  exact `SecurityGrant`. The coordinator also exposes `LoadAsync`,
  `ReadChildrenAsync`, and `StartAttemptAsync`. Attempt changes ride on
  transitions (`GoalAttemptStart`, `GoalAttemptSettlement`) so a status change
  and its attempt commit atomically. `GoalRecord` carries the durable
  `Sequence`, `ChildOrdinal`, `SettledSequence`, and the stored `Delegation`.
- `IGoalStore.ReadIntentsAsync` scans open delegated children across tenants for
  a host worker. It is authorized by a scanner identity the host configured on
  the store, not by a grant, because no caller holds a tenant-spanning grant.
- `IGoalJoinStrategy` evaluates a `GoalJoinEvaluationRequest` (the join request,
  the children in ordinal order, and whether the wait cutoff elapsed).
  Strategies are pure and never read completion order or a clock.
- A run's implicit root goal uses the run's identity (`RunRootGoal`), is
  materialized Proposed, Ready, Active by the delegation coordinator on first
  use, and stamps delegation depth in goal extension data.
- Child goal, delegation, attempt, and creation-key identities derive
  deterministically from the parent goal and the request's idempotency key
  (`DelegationIdentity`), so a retried request resolves to the one child. Replay
  equivalence deliberately ignores creation time, originating run, deadline,
  budget, and authorization on goal creation, and occurrence time, run, and
  operation on transitions.
- The delegation coordinator's dependencies differ from the sketch: it takes the
  profile catalog, grant issuer, dispatcher selector, wait parking, and options,
  and takes no hook dispatcher. The live `HookDispatchContext` is passed only to
  the security authority when the grant is requested.

### Child ownership, claim, and run identity

- The child goal is owned by the parent's agent and session, which is what lets
  the parent read it under its own authorization; the attempt records the agent
  that actually executes it and the session provisioned for that attempt.
- `DelegationChildResult` leaves session, attempt, and run absent while the
  status is `Dispatched`, and the coordinator reports them from durable state
  once the worker has recorded them.
- The worker claims a ready child with one atomic transition that records the
  attempt before anything runs, so a duplicate intent can never start a second
  run. The engine assigns the run identity after admission, so the attempt's
  `RunId` is the identity reserved at claim time and the settled
  `GoalOutcomeReference.RunId` is the authoritative identity of the run that
  executed; results report the outcome's run.
- A running attempt left by a previous incarnation is settled as failed with
  unknown side effects. There is no automatic retry, because the effects of the
  lost run are unknown; a retry is an explicit new attempt.
- The dispatcher's delegation grant is consumed by the dispatcher; the worker
  then mutates goals through the coordinator under the delegation's captured
  authorization, so it never widens authority and never writes a store directly.

### Waiting, parking, and hosting

- The parent waits by re-reading durable child state through the injected clock.
  While it waits, the coordinator parks the waiting run's session. The worker
  binds each claimed attempt's session to its slot, so a waiting child gives its
  slot back and a one-slot worker can run a chain of nested delegations.
- The wake-up signal is best effort and lossy; durable state is truth. The
  worker scans the configured profiles' durable intents at startup and
  periodically, which recovers work after process loss. A store that cannot
  discover intents (the session-backed store never persists delegation
  authorization) is reached only by the signal.
- A host starts the hosted worker at startup. A standalone engine has no host,
  so the first committed intent starts it and disposing the engine's provider
  stops it.
- The child budget is reserved as a child scope through `IBudgetAuthority` when
  one is composed, but the engine exposes no per-run budget injection, so the
  child run is bounded by its turn limit and the delegation deadline rather than
  by that scope. The recorded `AllowedTools` scope is narrowed and audited, but
  the engine has no per-run tool filter, so the child's tool surface is its own
  definition's.
- `IDelegationChildRunner` provisions a session and runs one claimed attempt.
  The engine-backed runner awaits `Agent.RunAsync`, which works with every
  output publisher; replace it to run children elsewhere.

### Communication

`IAgentMessageChannel` admits a message as steering or follow-up input through
the recipient's public input path. The input identity derives deterministically
from the sender, recipient session, and idempotency key, and the sender,
recipient, causal goal and attempt, and key travel as input extension data
marked `instruction_authority: false`. The recipient session's store owns
idempotency and conflict detection. No model-facing messaging tool is provided.

### Composition validation

The facade's goals validator proves, for every definition that selects a goal
profile, that the profile is published and that the singular coordinators,
selectors, catalog, pipeline, budget manager, and event dispatcher are
registered, and that the store, dispatcher, and each join strategy the profile
names are registered under exactly those keys. It reads descriptors and the
profile catalog only and never activates a store, dispatcher, or worker. It does
not prove delegation-policy identities or the behavior of a policy; the pipeline
fails closed when a selected policy is unregistered or the ordering is cyclic.

## Related concept specifications

- [Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md)
- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
- [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)
