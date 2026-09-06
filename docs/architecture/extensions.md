# Hooks and extensions

**Role:** Let applications inspect and modify named stages of the agentic
process without exposing unrestricted runtime internals.

AgentKit calls these extension points hooks. They cover the jobs often described
as middleware in other .NET systems and as hooks in coding agents. Hook
interfaces and their `EventArgs` models live in AgentKit.Abstractions.
AgentKit.Hooks supplies the first-party dispatcher, ordering, validation,
lifetime handling, and diagnostics. AddAgentHooks registers that infrastructure;
applications may register any number of hook implementations.

There is no universal plugin object. A package becomes composable by registering
one or more narrow hooks, capabilities, contributors, providers, strategies, or
other component contracts.

`AgentEngine` hosts many agent definitions and concurrent runs. Hook discovery,
profile selection, captured catalogs, invocation state, and diagnostics are
therefore keyed by typed `AgentId`, `SessionId`, `RunId`, and `OperationId`.
There is no engine-global current hook chain or ambient current agent.

## Normative minimal contract shape

The following C# 14 shapes are normative and minimal rather than an exhaustive
list of hook points. Every named type lives in its own same-named file. Shared
`AgentId`, `SessionId`, `RunId`, `OperationId`, `TimeProvider`, and
`IIdentifierGenerator<TIdentifier>` contracts are reused.

```csharp
namespace AgentKit;

public readonly record struct HookRegistrationId(Guid Value);

public readonly record struct HookInvocationId(Guid Value);

public readonly record struct HookProfileKey(string Value);

public readonly record struct HookCatalogVersion(string Value);
```

Registration IDs are host-supplied stable identity. Invocation and operation IDs
come from injected generators. Hook time, deadlines, and duration use
`TimeProvider`.

```csharp
namespace AgentKit;

public sealed record HookRegistrationDescriptor(
    HookRegistrationId Id,
    HookPoint Point,
    HookProfileKey ProfileKey,
    HookOrder Order,
    HookLifetime Lifetime,
    HookFailureMode FailureMode,
    HookReentrancyPolicy Reentrancy,
    ImmutableArray<HookRegistrationId> Before,
    ImmutableArray<HookRegistrationId> After);

public sealed record HookCatalogSnapshot(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    HookProfileKey ProfileKey,
    HookCatalogVersion Version,
    ImmutableArray<HookRegistrationDescriptor> Registrations);

public sealed record HookDispatchContext(
    HookCatalogSnapshot Catalog,
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
    AgentId agentId,
    SessionId sessionId,
    RunId runId,
    OperationId operationId,
    HookInvocationId invocationId,
    DateTimeOffset timestamp,
    DateTimeOffset deadline,
    AgentRunView runView) : EventArgs
{
    public AgentId AgentId { get; } = agentId;
    public SessionId SessionId { get; } = sessionId;
    public RunId RunId { get; } = runId;
    public OperationId OperationId { get; } = operationId;
    public HookInvocationId InvocationId { get; } = invocationId;
    public DateTimeOffset Timestamp { get; } = timestamp;
    public DateTimeOffset Deadline { get; } = deadline;
    public AgentRunView RunView { get; } = runView;
}
```

`AgentRunView` is an immutable bounded view, not a mutable engine, service
provider, credential accessor, or history editor. Identity, causality,
principal, durable state, prior audit, and authority-bearing values remain
read-only in every derived event-argument type.

A concrete point gets a dedicated interface and event arguments. For example:

```csharp
namespace AgentKit;

public sealed class BeforeToolInvocationEventArgs(
    AgentHookContext context,
    ValidatedToolCall call,
    ToolInvocationOptions options) : AgentHookEventArgs(
        context.AgentId,
        context.SessionId,
        context.RunId,
        context.OperationId,
        context.InvocationId,
        context.Timestamp,
        context.Deadline,
        context.RunView)
{
    public ValidatedToolCall Call { get; } = call;
    public ToolInvocationOptions Options { get; set; } = options;
    public ToolInvocationShortCircuit? ShortCircuit { get; set; }
}

public interface IBeforeToolInvocationHook
{
    ValueTask InvokeAsync(
        BeforeToolInvocationEventArgs eventArgs,
        CancellationToken cancellationToken);
}

public sealed class ToolResultHookEventArgs(
    AgentHookContext context,
    ToolCallResult result) : AgentHookEventArgs(
        context.AgentId,
        context.SessionId,
        context.RunId,
        context.OperationId,
        context.InvocationId,
        context.Timestamp,
        context.Deadline,
        context.RunView)
{
    public ToolCallResult OriginalResult { get; } = result;
    public ToolResultContentReplacement? ContentReplacement { get; set; }
}

public interface IToolResultHook
{
    ValueTask InvokeAsync(
        ToolResultHookEventArgs eventArgs,
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
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        HookProfileKey profileKey,
        AgentDefinitionRevision agentDefinitionRevision,
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

public interface IHookDispatcher
{
    ValueTask DispatchBeforeToolInvocationAsync(
        HookDispatchContext context,
        BeforeToolInvocationEventArgs eventArgs,
        CancellationToken cancellationToken);

    ValueTask DispatchToolResultAsync(
        HookDispatchContext context,
        ToolResultHookEventArgs eventArgs,
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

The two dispatcher methods are representative. Every stabilized hook point adds
its own typed dispatcher member, hook interface, event arguments, validator, and
conformance cases. There is no public `DispatchAsync(string, object)` or generic
hook interface. Registration sources are additive discovery; the profile
selector chooses one configured profile; the catalog snapshots and orders it;
the dispatcher executes; diagnostic sinks observe immutable outcomes.

AgentKit.Hooks supplies sealed registration catalog, profile selector, order
resolver, dispatcher, mutation validators, invocation tracker, and diagnostic
dispatcher classes. Only `AgentHookEventArgs` is a shared base class, justified
by immutable identity, timing, and run-view mechanics. Hook implementations have
no required base class and may implement dedicated interfaces directly.

```csharp
namespace AgentKit.Hooks;

internal sealed class HookDispatcher(
    IHookMutationValidatorCatalog validators,
    IHookDiagnosticDispatcher diagnostics,
    TimeProvider timeProvider,
    IIdentifierGenerator<HookInvocationId> invocationIds) : IHookDispatcher
{
}
```

`IHookInstanceFactory` is a typed DI activation boundary created by the hooks
package; runtime components do not receive or query `IServiceProvider`. It
creates one `IHookActivationLease` for the captured catalog and run scope. The
lease owns the scoped tracker and hook instances, exposes only typed hook
resolution, and is asynchronously disposed by the run owner.
`HookDispatchContext` carries that lease with the immutable catalog. It is
runtime-only state and is never serialized. The singleton dispatcher therefore
captures no scope, tracker, hook instance, diagnostic sink, or run state. The
tracker synchronously guards only in-memory depth, cycle, and reentrancy state;
hook work itself remains asynchronous and cancellable. Invocation tracking is
not `AsyncLocal` authority.

## Configuration and dependency injection

Agent definitions select a `HookProfileKey`. Host options define hard depth,
time, allocation, and diagnostic ceilings. Named profiles choose registrations,
ordering, failure modes, and reload boundaries. Run configuration may disable
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
    public HookFailureMode TransformFailureMode { get; set; } =
        HookFailureMode.FailOperation;
    public HookFailureMode ObservationFailureMode { get; set; } =
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

        public IServiceCollection AddBeforeToolInvocationHook<THook>(
            HookRegistrationDescriptor registration)
            where THook : class, IBeforeToolInvocationHook =>
            HookServiceRegistration.AddBeforeToolInvocationHook<THook>(
                services,
                registration);

        public IServiceCollection AddToolResultHook<THook>(
            HookRegistrationDescriptor registration)
            where THook : class, IToolResultHook =>
            HookServiceRegistration.AddToolResultHook<THook>(
                services,
                registration);

        public IServiceCollection AddHookDiagnosticSink<TSink>()
            where TSink : class, IHookDiagnosticSink =>
            HookServiceRegistration.AddHookDiagnosticSink<TSink>(services);

        public IServiceCollection ReplaceHookDispatcher<TDispatcher>()
            where TDispatcher : class, IHookDispatcher =>
            HookServiceRegistration.ReplaceHookDispatcher<TDispatcher>(
                services);
    }
}
```

The package-internal `HookServiceRegistration` helper performs registrations
without building or resolving a service provider.

Every hook point has its own registration method following the shown shape.
`AddAgentHooks` is idempotent and `TryAdd`s the engine-wide dispatcher, profile
selector, catalog, order resolver, validator catalog, instance factory, and
diagnostic dispatcher. It registers `IHookInvocationTracker` separately as one
run-scoped service created inside each activation lease. Explicit replacement
methods replace singular axes. Hook registrations, registration sources,
validators for distinct points, and diagnostic sinks are additive; the
diagnostic dispatcher activates sinks according to their declared lifetimes.

Profiles are keyed by `HookProfileKey`; registrations have stable
`HookRegistrationId`. Reusing an identity within a profile is a startup error
unless an explicit replacement API names it. Registration order is only the
final deterministic tie-breaker after topological constraints. Runtime
components receive the captured `HookCatalogSnapshot`, not keyed-container
access.

Catalogs, profile selectors, order resolvers, and the dispatcher are thread-safe
singletons. Immutable thread-safe hook implementations may be singleton; mutable
hooks are run-scoped or transient as declared by their registration. One
captured catalog is immutable for a run, while each `HookDispatchContext` has
isolated invocation and reentrancy state through its activation lease. The
owning run disposes the lease, tracker, and hook instances exactly once.
Cancellation is passed to every hook; required work participates in settlement,
and hooks cannot detach untracked tasks.

## Composition validation and unsupported behavior

A runnable engine requires one engine-wide hook profile selector, catalog, order
resolver, dispatcher, and validator catalog even when no application hooks are
registered. Each definition resolves one hook profile, and each run activates
one isolated invocation tracker. Build or agent-definition validation checks
profile references and versions, scoped activation-lease creation and disposal,
dedicated point compatibility, unique registration IDs, ordering cycles and
missing dependencies, failure modes, reentrancy bounds, lifetime capture, reload
boundaries, diagnostic delivery, `TimeProvider`, and ID generators.

An empty hook profile is supported. An unknown profile or hook point, missing
validator, invalid mutation, impossible ordering, unsupported reentrancy, or
expired hook deadline returns a typed hook/owning-operation failure. Transform
and security-critical failures fail the owning operation; configured best-
effort observation failure is isolated and diagnosed. No hook can grant, widen,
forge, cache, consume, or mint authority. If hook-permitted mutation changes a
protected input, execution discards the old grant and returns through security
evaluation before the effect.

## Hook contract

The normative
[hooks and extensions specification](../concepts/extensions-hooks-and-middleware.md)
defines mutation, validation, short-circuit, and isolation semantics for these
boundaries.

Each hook point has a dedicated interface and a dedicated class derived from
`EventArgs`. Shared base event arguments expose stable agent, run, session,
operation, correlation, timing, and an immutable run view. Boundary-specific
derived classes expose only the values relevant at that point.

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

## Ordering and dispatch

Mutating hooks execute sequentially. Registration order is the deterministic
tie-breaker; optional stable hook identities may declare before, after, first,
last, or dependency constraints. AddAgentHooks validates cycles, missing
dependencies, duplicate identities, incompatible scopes, and unsupported hook
points during composition.

The engine captures an immutable hook catalog for each run. Dynamic
configuration may produce a new catalog at a documented run or turn boundary,
but it cannot change the hook sequence already executing.

Before hooks run in resolved order. Corresponding after and error hooks unwind
in reverse order when they belong to the same operation scope. Independent
notification hooks use their documented forward order.

## Lifetimes and reentrancy

Hook registrations are additive. Immutable thread-safe hooks may be singleton;
mutable hooks are run-scoped or transient. Hook instances receive explicit
dependencies through constructor injection and typed event arguments, never a
general service provider.

Nested operations carry hook invocation identity and depth. A hook does not
re-enter the same hook point merely because it calls an allowed lower-level
service. Explicit reentrancy must be declared, bounded, and protected against
cycles. Hook state does not use static or ambient current-run storage.

All hooks are asynchronous where the boundary can perform asynchronous work,
receive cancellation, share the operation deadline, and consume bounded time and
allocation budgets. Hooks cannot detach untracked work from run settlement.

## Failure and security

Every protected effect still passes through the
[security authority](../concepts/permissions-approvals-and-trust.md); hook
registration grants no operational permission.

Transforming hooks fail their owning operation by default. Best-effort
observation hooks may be isolated, but their failure remains observable.
Security-critical hook failure always fails closed.

Hooks are trusted executable application code, but trust does not grant runtime
authority. A hook that attempts protected file, network, process, memory, model,
or remote operations must use the same security authority as every other
component. Hook registration cannot replace a denial or mint an approval grant.

Every invocation records hook identity, point, duration, outcome, and the names
of changed fields without logging protected values. Sensitive before/after data
is captured only under explicit diagnostic policy.

## Related documentation

- [Extensions, hooks, and middleware](../concepts/extensions-hooks-and-middleware.md)
- [Permissions and human control](permissions-and-human-control.md)
- [Architecture and dependency boundaries](../concepts/architecture-and-dependency-boundaries.md)
- [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)
