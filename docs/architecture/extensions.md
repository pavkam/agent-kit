# Hooks and extensions

**Role:** Let applications inspect and modify named stages of the agentic
process without exposing unrestricted runtime internals.

AgentKit calls these extension points hooks. They cover the jobs often described
as middleware in other .NET systems and as hooks in coding agents. Hook
interfaces and their `EventArgs` models live in AgentKit.Abstractions for
built-in points and in an owning feature's contract assembly for third-party
points. AgentKit.Hooks supplies the first-party dispatch kernel, ordering,
activation, lifetime handling, and diagnostics. AddAgentHooks registers that
infrastructure; applications may register any number of hook implementations.

There is no universal plugin object. A package becomes composable by registering
one or more narrow hooks, capabilities, contributors, providers, strategies, or
other component contracts.

`AgentEngine` hosts many agent definitions and concurrent runs, but not every
hook point occurs after all of those identities exist. Engine construction can
precede `AgentId`; agent registration can precede `SessionId`; admission can
precede `RunId`. Hook contracts carry only identities established at their
stage. They never fabricate sentinel identities or make universally nullable
properties merely to fit one base class. There is no engine-global current hook
chain or ambient current agent.

Hook infrastructure distinguishes four identities:

- `HookPointId` names one stable, namespaced lifecycle boundary;
- `HookRegistrationId` names one configured hook registration within a profile;
- `HookDispatchId` names one emission of one point and spans every hook invoked
  for that emission; and
- `HookInvocationId` names one registration's individual execution within a
  dispatch.

The causal operation is separate from all four. Dispatch and invocation IDs come
from injected generators. Registration identity is host- or package-supplied and
stable across equivalent catalog captures. Point identity is part of the typed
point contract, not an arbitrary string selected by a model or hook.

## Normative minimal contract shape

The following C# 14 shapes are normative and minimal rather than an exhaustive
list of hook points. Every named type lives in its own same-named file. Shared
`AgentId`, `SessionId`, `RunId`, and other domain identity contracts are reused
by derived arguments when their stage has established them. Causality uses the
shared `OperationCorrelation` contract; time and generated hook identities use
`TimeProvider` and `IIdentifierGenerator<TIdentifier>`.

```csharp
namespace AgentKit;

public readonly record struct HookRegistrationId(Guid Value);

public readonly record struct HookPointId(string Value);

public readonly record struct HookDispatchId(Guid Value);

public readonly record struct HookInvocationId(Guid Value);

public readonly record struct HookProfileKey(string Value);

public readonly record struct HookCatalogVersion(string Value);
```

Registration IDs are host-supplied stable identity. Dispatch and invocation IDs
come from injected generators. Hook time, deadlines, and duration use
`TimeProvider`.

```csharp
namespace AgentKit;

public sealed record HookRegistrationDescriptor(
    HookRegistrationId Id,
    HookPointId Point,
    HookProfileKey ProfileKey,
    HookOrder Order,
    HookLifetime Lifetime,
    HookFailureMode RequestedFailureMode,
    HookReentrancyPolicy Reentrancy,
    ImmutableArray<HookRegistrationId> Before,
    ImmutableArray<HookRegistrationId> After,
    ImmutableArray<HookRegistrationId> DependsOn);

public sealed record HookCatalogSnapshot(
    HookProfileKey ProfileKey,
    HookCatalogVersion Version,
    ImmutableArray<HookRegistrationDescriptor> Registrations);

public sealed record HookDispatchMetadata(
    HookPointId Point,
    HookDispatchId DispatchId,
    OperationCorrelation Correlation,
    DateTimeOffset Timestamp,
    DateTimeOffset Deadline);

public sealed record HookInvocationContext(
    HookRegistrationId RegistrationId,
    HookInvocationId InvocationId,
    HookDispatchId DispatchId,
    int Depth);

public sealed record HookDispatchContext(
    HookCatalogSnapshot Catalog,
    HookDispatchMetadata Dispatch,
    IHookActivationLease Activation);

public interface IHookInvocationTracker
{
    HookInvocationTrackingResult TryEnter(HookInvocationAttempt attempt);
    void Exit(HookInvocationId invocationId);
}

public interface IHookActivationLease : IAsyncDisposable
{
    IHookInvocationTracker InvocationTracker { get; }

    ValueTask<HookInstanceResolution<THook>> ResolveAsync<THook>(
        HookRegistrationId registrationId,
        CancellationToken cancellationToken)
        where THook : class;
}

public interface IHookInstanceFactory
{
    ValueTask<IHookActivationLease> CreateAsync(
        HookCatalogSnapshot catalog,
        CancellationToken cancellationToken);
}

public abstract class AgentHookEventArgs(
    HookDispatchMetadata dispatch) : EventArgs
{
    public HookPointId Point { get; } = dispatch.Point;
    public HookDispatchId DispatchId { get; } = dispatch.DispatchId;
    public OperationCorrelation Correlation { get; } = dispatch.Correlation;
    public DateTimeOffset Timestamp { get; } = dispatch.Timestamp;
    public DateTimeOffset Deadline { get; } = dispatch.Deadline;
}
```

The shared base contains only point, dispatch, causality, and timing because
those facts exist for every dispatch. A derived type adds `AgentId`,
`SessionId`, `RunId`, `TurnId`, an immutable `AgentRunView`, or other identities
only after the owning stage has established them. A stage that has not
established an identity omits it rather than publishing a default or nullable
placeholder. Identity, causality, principal, durable state, prior audit, and
authority-bearing values remain read-only in every derived event-argument type.

One event-argument instance is shared across the hooks in a dispatch, so it
cannot truthfully expose one `HookInvocationId`. The dispatcher creates an
immutable `HookInvocationContext` for each individual execution and supplies it
to the dedicated hook contract. The same identity is used by diagnostics,
reentrancy tracking, and paired unwind bookkeeping.

A concrete point gets a dedicated interface and event arguments. For example:

```csharp
namespace AgentKit;

public sealed class BeforeToolInvocationEventArgs(
    HookDispatchMetadata dispatch,
    AgentId agentId,
    SessionId sessionId,
    RunId runId,
    AgentRunView runView,
    ValidatedToolCall call,
    ToolInvocationOptions options) : AgentHookEventArgs(dispatch)
{
    public AgentId AgentId { get; } = agentId;
    public SessionId SessionId { get; } = sessionId;
    public RunId RunId { get; } = runId;
    public AgentRunView RunView { get; } = runView;
    public ValidatedToolCall Call { get; } = call;
    public ToolInvocationOptions Options { get; set; } = options;
    public ToolInvocationShortCircuit? ShortCircuit { get; set; }
}

public interface IBeforeToolInvocationHook
{
    ValueTask InvokeAsync(
        BeforeToolInvocationEventArgs eventArgs,
        HookInvocationContext invocation,
        CancellationToken cancellationToken);
}

public sealed class ToolResultHookEventArgs(
    HookDispatchMetadata dispatch,
    AgentId agentId,
    SessionId sessionId,
    RunId runId,
    ToolCallResult result) : AgentHookEventArgs(dispatch)
{
    public AgentId AgentId { get; } = agentId;
    public SessionId SessionId { get; } = sessionId;
    public RunId RunId { get; } = runId;
    public ToolCallResult OriginalResult { get; } = result;
    public ToolResultContentReplacement? ContentReplacement { get; set; }
}

public interface IToolResultHook
{
    ValueTask InvokeAsync(
        ToolResultHookEventArgs eventArgs,
        HookInvocationContext invocation,
        CancellationToken cancellationToken);
}
```

The writable replacement types expose only permitted fields. Validation rejects
identity, security, side-effect-certainty, or terminal-state changes. A short
circuit is a point-specific discriminated result; it cannot turn denial,
cancellation, unknown effects, or failure into success.

### Discovery, selection, ordering, and dispatch

```csharp
namespace AgentKit;

public interface IHookRegistrationSource
{
    ValueTask<HookRegistrationSnapshot> DiscoverAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken);
}

public interface IHookProfileSelector
{
    ValueTask<HookProfileSelectionResult> SelectAsync(
        HookProfileSelectionRequest request,
        CancellationToken cancellationToken);
}

public interface IHookOrderResolver
{
    HookOrderResult Resolve(
        ImmutableArray<HookRegistrationDescriptor> registrations);
}

public interface IHookCatalog
{
    ValueTask<HookCatalogSnapshot> CaptureAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken);
}

public sealed record HookPointDefinition<THook, TEventArgs>(
    HookPointId Id,
    HookPointKind Kind,
    HookFailureMode FailureInvariant,
    IHookMutationValidator<TEventArgs> Validator,
    HookInvoker<THook, TEventArgs> Invoke)
    where THook : class
    where TEventArgs : AgentHookEventArgs;

public delegate ValueTask HookInvoker<THook, TEventArgs>(
    THook hook,
    TEventArgs eventArgs,
    HookInvocationContext invocation,
    CancellationToken cancellationToken)
    where THook : class
    where TEventArgs : AgentHookEventArgs;

public interface IHookDispatcher
{
    ValueTask DispatchAsync<THook, TEventArgs>(
        HookPointDefinition<THook, TEventArgs> point,
        HookDispatchContext context,
        TEventArgs eventArgs,
        HookFailureMode? failurePolicyTightening,
        CancellationToken cancellationToken)
        where THook : class
        where TEventArgs : AgentHookEventArgs;
}

public interface IBeforeToolInvocationHookDispatcher
{
    ValueTask DispatchAsync(
        HookDispatchContext context,
        BeforeToolInvocationEventArgs eventArgs,
        CancellationToken cancellationToken);
}

public interface IHookDiagnosticSink
{
    ValueTask PublishAsync(
        HookInvocationDiagnostic diagnostic,
        CancellationToken cancellationToken);
}

public interface IHookDiagnosticDispatcher
{
    ValueTask PublishAsync(
        HookInvocationDiagnostic diagnostic,
        CancellationToken cancellationToken);
}
```

`IHookDispatcher` is a typed generic dispatch kernel, not a generic hook. A
point definition closes the hook interface, event-argument type, stable point
identity, validator, mutability classification, and minimum failure behavior as
one contract, including the typed invocation delegate. The owning feature may
expose a dedicated closed dispatcher such as
`IBeforeToolInvocationHookDispatcher`; otherwise its runtime calls the kernel
with the same immutable static point definition. Runtime code therefore cannot
pair an arbitrary point name or invocation delegate with an arbitrary object
payload.

Before activation, the kernel verifies that the definition ID, dispatch-context
point, and event-argument point match, that both contexts carry the same
`HookDispatchId`, and that the captured registrations were validated against
that closed definition. A mismatch fails before any hook is resolved or invoked.

This split lets a third-party feature package add a dedicated hook interface,
event arguments, point definition, validator, registration extension, optional
closed adapter, and conformance cases without editing `IHookDispatcher` or
adding a switch to AgentKit.Hooks. Built-in contracts live in
AgentKit.Abstractions; third-party contracts live in the feature's own contract
assembly, which may reference AgentKit.Abstractions but never the concrete hooks
package. Point definitions and registration sources are additive. Duplicate
point identities with different closed types or invariants fail composition.

There is no public `DispatchAsync(string, object)`, open event-name/object
registration, or universal `IHook<TEventArgs>` that erases the named boundary.
The generic kernel is infrastructure used only with a feature-owned closed point
definition, directly or through an adapter. The profile selector chooses one
configured profile using a `HookProfileSelectionRequest` containing only scope
identities available at that stage; the catalog snapshots and orders it; the
kernel executes; diagnostic sinks observe immutable outcomes.

AgentKit.Hooks supplies sealed registration catalog, profile selector, order
resolver, dispatch kernel, invocation tracker, and diagnostic dispatcher
classes. Owning features supply their typed point definitions, validators, and
closed adapters. Only `AgentHookEventArgs` is a shared base class, justified by
point, dispatch, causal, and timing mechanics. Hook implementations have no
required base class and may implement dedicated interfaces directly.

```csharp
namespace AgentKit.Hooks;

internal sealed class HookDispatcher(
    IHookDiagnosticDispatcher diagnostics,
    TimeProvider timeProvider,
    IIdentifierGenerator<HookInvocationId> invocationIds) : IHookDispatcher
{
}
```

`IHookInstanceFactory` is a typed DI activation boundary created by the hooks
package; runtime components do not receive or query `IServiceProvider`. It
creates one `IHookActivationLease` for the captured catalog and its declared
engine, agent, run, turn, or operation scope. The lease owns the scoped tracker
and hook instances, exposes only typed hook resolution, and is asynchronously
disposed by that scope's owner. `HookDispatchContext` carries that lease with
the immutable catalog and dispatch metadata. It is runtime-only state and is
never serialized. The singleton dispatcher therefore captures no scope, tracker,
hook instance, diagnostic sink, or run state. The tracker synchronously guards
only in-memory depth, cycle, and reentrancy state; hook work itself remains
asynchronous and cancellable. Invocation tracking is not `AsyncLocal` authority.

## Configuration and dependency injection

Runnable agent definitions select a `HookProfileKey`; points that occur before
an agent definition exists use an explicitly configured host profile. Host
options define hard depth, time, allocation, and diagnostic ceilings plus a
minimum failure mode. Named profiles choose registrations, ordering requests,
requested failure modes, and reload boundaries. Run configuration may disable
optional observation hooks or tighten bounds but cannot disable required
security hooks or relax host policy.

```csharp
namespace AgentKit.Hooks;

public sealed class AgentHookOptions
{
    public int MaximumInvocationDepth { get; set; } = 8;
    public TimeSpan DefaultHookTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public HookMutationDispatchMode MutationDispatchMode { get; set; } =
        HookMutationDispatchMode.Sequential;
    public HookFailureMode MinimumFailureMode { get; set; } =
        HookFailureMode.IsolateAndDiagnose;
    public HookReloadBoundary ReloadBoundary { get; set; } =
        HookReloadBoundary.NextRun;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentHooks(
            Action<AgentHookOptions>? configure = null) =>
            HookServiceRegistration.AddAgentHooks(
                services,
                configure);

        public IServiceCollection AddHookProfile(
            HookProfileKey key,
            Action<HookProfileOptions> configure) =>
            HookServiceRegistration.AddHookProfile(services, key, configure);

        public IServiceCollection ReplaceHookProfile(
            HookProfileKey key,
            Action<HookProfileOptions> configure) =>
            HookServiceRegistration.ReplaceHookProfile(
                services,
                key,
                configure);

        public IServiceCollection AddHookDiagnosticSink<TSink>()
            where TSink : class, IHookDiagnosticSink =>
            HookServiceRegistration.AddHookDiagnosticSink<TSink>(services);

        public IServiceCollection ReplaceHookProfileSelector<TSelector>()
            where TSelector : class, IHookProfileSelector =>
            HookServiceRegistration.ReplaceHookProfileSelector<TSelector>(
                services);

        public IServiceCollection ReplaceHookOrderResolver<TResolver>()
            where TResolver : class, IHookOrderResolver =>
            HookServiceRegistration.ReplaceHookOrderResolver<TResolver>(
                services);

        public IServiceCollection ReplaceHookCatalog<TCatalog>()
            where TCatalog : class, IHookCatalog =>
            HookServiceRegistration.ReplaceHookCatalog<TCatalog>(services);

        public IServiceCollection ReplaceHookDispatcher<TDispatcher>()
            where TDispatcher : class, IHookDispatcher =>
            HookServiceRegistration.ReplaceHookDispatcher<TDispatcher>(
                services);

        public IServiceCollection
            ReplaceHookDiagnosticDispatcher<TDispatcher>()
            where TDispatcher : class, IHookDiagnosticDispatcher =>
            HookServiceRegistration
                .ReplaceHookDiagnosticDispatcher<TDispatcher>(services);
    }
}
```

The owning feature supplies point-specific registration without changing the
central hooks package:

```csharp
namespace AgentKit.Tools;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBeforeToolInvocationHook<THook>(
            HookRegistrationDescriptor registration)
            where THook : class, IBeforeToolInvocationHook =>
            ToolHookServiceRegistration.AddBeforeToolInvocationHook<THook>(
                services,
                registration);

        public IServiceCollection ReplaceBeforeToolInvocationHook<THook>(
            HookRegistrationDescriptor registration)
            where THook : class, IBeforeToolInvocationHook =>
            ToolHookServiceRegistration
                .ReplaceBeforeToolInvocationHook<THook>(
                    services,
                    registration);

        public IServiceCollection AddToolResultHook<THook>(
            HookRegistrationDescriptor registration)
            where THook : class, IToolResultHook =>
            ToolHookServiceRegistration.AddToolResultHook<THook>(
                services,
                registration);

        public IServiceCollection ReplaceToolResultHook<THook>(
            HookRegistrationDescriptor registration)
            where THook : class, IToolResultHook =>
            ToolHookServiceRegistration.ReplaceToolResultHook<THook>(
                services,
                registration);
    }
}
```

Failure modes form an explicit strictness order:
`IsolateAndDiagnose < FailOperation`. For each invocation, the effective mode is
the strictest of the hook point's invariant, the host's minimum mode, the
profile default, that registration's request, and any run- or operation-level
tightening. A narrower scope may select `FailOperation`; it cannot select
isolation when any broader source requires failure. Transforming and
security-relevant point definitions always declare `FailOperation`, so
configuration cannot isolate them.

The package-internal `HookServiceRegistration` and owning feature helpers
perform registrations without building or resolving a service provider.

Every hook point has its own registration method following the shown shape.
Third-party features register their closed point definition, validator, adapter,
and hook implementations from their own service extension. `AddAgentHooks` is
idempotent and `TryAdd`s the engine-wide dispatch kernel, profile selector,
catalog, order resolver, point-definition catalog, instance factory, and
diagnostic dispatcher. It creates one `IHookInvocationTracker` inside each
activation lease. Explicit replacement methods replace singular axes. Hook
registrations, registration sources, closed point definitions, and diagnostic
sinks are additive; the diagnostic dispatcher activates sinks according to their
declared lifetimes.

Profiles are keyed by `HookProfileKey`; registrations have stable
`HookRegistrationId`. Reusing an identity within the same point, profile, and
catalog is a composition error unless an explicit replacement API names it.
Registration order is only the final deterministic tie-breaker after topological
constraints. Runtime components receive the captured `HookCatalogSnapshot`, not
keyed-container access.

Catalogs, profile selectors, order resolvers, and the dispatcher are thread-safe
singletons. Immutable thread-safe hook implementations may be singleton; mutable
hooks are scoped or transient as declared by their registration. One captured
catalog is immutable for its documented engine, agent, run, turn, or operation
scope, while each `HookDispatchContext` has isolated invocation and reentrancy
state through its activation lease. The scope owner disposes the lease, tracker,
and hook instances exactly once. Cancellation is passed to every hook; required
work participates in settlement, and hooks cannot detach untracked tasks.

## Composition validation and unsupported behavior

A runnable engine requires one engine-wide hook profile selector, catalog, order
resolver, dispatch kernel, and point-definition catalog even when no application
hooks are registered. Each definition resolves one hook profile, and every
activated catalog receives one isolated invocation tracker. Build,
agent-definition, and catalog-capture validation checks profile references and
versions, scoped activation-lease creation and disposal, closed point type
compatibility, point-identity collisions, unique registration IDs, ordering
self-references, contradictions, cycles, and missing hard dependencies,
effective failure policies, isolation eligibility, reentrancy bounds, lifetime
capture, reload boundaries, diagnostic delivery, `TimeProvider`, and dispatch
and invocation ID generators.

An empty hook profile is supported. An unknown profile or point, a point ID
bound to incompatible closed types, a missing validator, invalid mutation,
impossible ordering, unsupported reentrancy, expired deadline, a point contract
that requires an identity before its stage establishes it, or attempted
failure-policy relaxation returns a typed hook/owning-operation failure.
Transform and security-relevant failures fail the owning operation. Isolation is
valid only for observation or read-only points. If such a point permits bounded
diagnostic annotation, the dispatcher stages the annotation and commits it only
after successful invocation and validation, or restores a snapshot before
continuing; an isolated hook never leaks partial mutation. No hook can grant,
widen, forge, cache, consume, or mint authority. If hook-permitted mutation
changes a protected input, execution discards the old grant and returns through
security evaluation before the effect.

## Hook contract

The normative
[hooks and extensions specification](../concepts/extensions-hooks-and-middleware.md)
defines mutation, validation, short-circuit, and isolation semantics for these
boundaries.

Each hook point has a dedicated interface and a dedicated class derived from
`EventArgs`. Shared base event arguments expose only point identity, dispatch
identity, causal correlation, and timing. Boundary-specific derived classes
expose `AgentId`, `SessionId`, `RunId`, immutable views, and other values only
where the owning stage has established them.

Identity, causality, principal, prior security decisions, durable sequence, and
already committed state are read-only. A derived class makes individual
properties writable only when that hook is allowed to replace them. A context
assembly hook may change context candidates; a provider-request hook may change
approved request options; a tool-result hook may normalize safe result content.
None receives a general mutable engine object.

Mutation happens in place on the event arguments so later hooks see earlier
changes. The dispatcher validates the event arguments after every hook. Invalid
mutation fails at the owning boundary instead of leaking corrupted state into
the next component.

A hook may stop or replace an operation only when its event arguments expose a
typed short-circuit result. There is no universal cancel flag that can silently
turn denial, cancellation, protocol failure, or budget exhaustion into success.

## Hook points

Hook families may cover:

- engine and run creation, start, completion, failure, and settlement;
- input admission and promotion;
- session load, append, branch, checkpoint, and compaction;
- history validation, context contribution, assembly, and final manifests;
- model catalog preparation, selection, request preparation, response, and
  stream events;
- tool discovery, schema validation, security authorization, execution, and
  result normalization;
- memory proposal, storage, retrieval, and model exposure;
- goal creation, delegation, joining, and completion;
- output validation and final-result publication; and
- security request presentation, decision observation, approval resolution, and
  audit publication.

Not every point permits mutation. Observation-only hooks receive event arguments
with read-only operation data and may add bounded diagnostic metadata. Security
hooks may improve or redact an approval presentation, request stricter handling,
or observe a decision. They cannot grant authority, weaken scope, or replace the
security authority.

These families are not a closed central list. A third-party feature may add a
namespaced point through the same dedicated-interface, event-arguments,
definition, validator, optional-adapter, and conformance contract. It cannot
register an arbitrary event name and object payload or ask the dispatch kernel
to infer a point's semantics at runtime.

## Ordering and dispatch

Mutating hooks execute sequentially. Registration order is the deterministic
tie-breaker after all constraints. Ordering is resolved independently for one
point, profile, and captured catalog; a relationship never reaches into another
point, profile, or catalog version.

`Before` and `After` are soft constraints. They add an ordering edge when the
named registration exists in that resolution scope and do nothing when it does
not. `DependsOn` is the hard relation (an API named `Requires` has the same
semantics): the named registration must exist in that scope and must run first,
or catalog validation fails.

`First` and `Last` are singleton anchors, not priority bands. At most one
registration may claim each anchor in a resolution scope. `First` precedes every
other registration and `Last` follows every other registration. A second claim,
one registration claiming both anchors, any self-reference, the same target
named in contradictory relations, an anchor contradicted by another edge, or any
direct or transitive cycle is a composition failure. The resolver does not
silently discard a contradictory edge to produce an order.

The engine captures an immutable hook catalog at the point's documented engine,
agent, run, turn, or operation boundary. Dynamic configuration may produce a new
catalog only at a declared reload boundary; it cannot change the hook sequence
already executing.

Before hooks run in resolved order. Corresponding after and error hooks unwind
in reverse order when they belong to the same operation scope. Independent
notification hooks use their documented forward order.

## Lifetimes and reentrancy

Hook registrations are additive. Immutable thread-safe hooks may be singleton;
mutable hooks are scoped to the lifetime their point and registration declare,
or transient. Hook instances receive explicit dependencies through constructor
injection and typed event arguments, never a general service provider.

Nested operations carry dispatch identity, individual invocation identity, and
depth. A hook does not re-enter the same hook point merely because it calls an
allowed lower-level service. Explicit reentrancy must be declared, bounded, and
protected against cycles. Hook state does not use static or ambient current-run
storage.

All hooks are asynchronous where the boundary can perform asynchronous work,
receive cancellation, share the operation deadline, and consume bounded time and
allocation budgets. Hooks cannot detach untracked work from the owning scope's
settlement.

## Failure and security

Every protected effect still passes through the
[security authority](../concepts/permissions-approvals-and-trust.md); hook
registration grants no operational permission.

Failure resolution is monotonic. For each invocation, the dispatcher takes the
strictest of the point invariant, host minimum, selected profile default,
current registration request, and any narrower-scope tightening. No dispatch
caller, run override, hook, or point adapter may relax a broader requirement.
Transforming and security-relevant points always fail the owning operation. An
exception, cancellation, or invalid mutation at either kind therefore fails
closed.

Cancellation always propagates to the owning operation and is never converted
into an isolated hook failure.

Isolation is available only to observation or read-only points whose definition
explicitly allows it, and every isolated failure remains observable. Operation
state is read-only at such a point. If bounded diagnostic mutation is allowed,
each invocation stages it until successful validation or the dispatcher
snapshots and restores it on failure. Continuing after an isolated exception
must present later hooks and the owning operation with the last validated state,
never the failed hook's partial mutation.

Hooks are trusted executable application code, but trust does not grant runtime
authority. A hook that attempts protected file, network, process, memory, model,
or remote operations must use the same security authority as every other
component. Hook registration cannot replace a denial or mint an approval grant.

Every invocation records registration, point, dispatch, and invocation
identities, duration, outcome, effective failure policy, and the names of
changed fields without logging protected values. Sensitive before/after data is
captured only under explicit diagnostic policy.

## Related documentation

- [Extensions, hooks, and middleware](../concepts/extensions-hooks-and-middleware.md)
- [Permissions and human control](permissions-and-human-control.md)
- [Architecture and dependency boundaries](../concepts/architecture-and-dependency-boundaries.md)
- [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)
