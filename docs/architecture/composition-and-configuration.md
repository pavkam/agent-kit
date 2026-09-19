# Composition and configuration

**Role:** Build one valid process-level AgentEngine that can resolve and run
many independently configured agents from swappable components.

The AgentKit package is a dependency-light facade. It owns the public engine,
builder, hosted registration, composition validation, and lifecycle boundary. It
does not contain the loop, session coordinator, provider, permission engine,
storage, or tools.

## Public facade

The
[public API and dependency-injection contract](../concepts/public-api-and-dependency-injection.md)
defines this facade's ownership and replacement semantics; this page assigns
those semantics to packages.

AgentEngine is the immutable application-facing runtime facade for the whole
built composition. One engine hosts a versioned catalog of many immutable agent
definitions and may run different agents and sessions concurrently. The engine
is not itself an agent, does not carry one agent's mutable state, and does not
make a process-wide singleton out of a run scope.

The facade exposes operations to enumerate or resolve configured agents, create
or resume their sessions, submit input, stream a run, and await a final result
without exposing the dependency container. Public run operations address an
agent by `AgentId`; they do not accept an arbitrary service provider or let a
caller bypass definition validation.

AgentEngine.CreateBuilder returns a separate mutable AgentEngineBuilder. The
builder collects configuration and exposes its service collection. Build
validates the complete graph, constructs the provider in standalone mode, and
returns an immutable engine. A built engine cannot be reconfigured.

The same registrations support standard .NET hosting. AddAgentKit registers the
facade and validation into an existing service collection. The host then owns
provider creation, scopes, shutdown, and disposal.

## Normative minimal contract shape

The following C# shapes are the normative minimum, not an exhaustive frozen API.
Snippets group related types for readability; every named type belongs in its
own matching file in the real source tree. All public and internal members
receive complete XML documentation when implemented.

### Typed identity

Local domain identities are dedicated values, never interchangeable strings or
integers. Ordering uses separate sequence values; a sequence number is not an
identity. Provider-supplied identifiers remain wrapped or preserved in typed
extension data rather than being confused with AgentKit identities.

```csharp
namespace AgentKit;

public readonly record struct AgentId(Guid Value);
public readonly record struct SessionId(Guid Value);
public readonly record struct ConversationId(Guid Value);
public readonly record struct RunId(Guid Value);
public readonly record struct TurnId(Guid Value);
public readonly record struct MessageId(Guid Value);
public readonly record struct ToolCallId(Guid Value);
public readonly record struct InputId(Guid Value);
public readonly record struct AdmissionId(Guid Value);
public readonly record struct OperationId(Guid Value);
public readonly record struct ModelRequestId(Guid Value);

public readonly record struct AgentDefinitionRevision(long Value);
public readonly record struct AgentCatalogVersion(long Value);
public readonly record struct AgentDefinitionSourceVersion(long Value);
public readonly record struct ConfigurationVersion(long Value);

public readonly record struct TenantId(string Value);
public readonly record struct PrincipalId(string Value);
public readonly record struct ComponentId(string Value);
public readonly record struct IdempotencyKey(string Value);
public readonly record struct VersionToken(string Value);
public readonly record struct SchemaVersion(string Value);
public readonly record struct InputFingerprint(ContentHash Hash);

public readonly record struct CapabilityId(string Value);
public readonly record struct CapabilityProfileId(string Value);

public readonly record struct ProviderId(string Value);
public readonly record struct ModelId(string Value);
public readonly record struct ProviderRequestId(string Value);
public readonly record struct ProviderResponseId(string Value);
public readonly record struct ProviderToolCallId(string Value);
public readonly record struct ExternalRequestId(string Value);
public readonly record struct AgentDefinitionSourceId(string Value);

public sealed record AgentCapabilityReference(
    CapabilityId CapabilityId,
    CapabilityProfileId ProfileId);

public abstract record OperationCorrelation(OperationId OperationId);

public sealed record BeforeRunOperationCorrelation(
    OperationId OperationId,
    AdmissionId? AdmissionId) : OperationCorrelation(OperationId);

public sealed record InRunOperationCorrelation(
    OperationId OperationId,
    RunId RunId,
    TurnId? TurnId) : OperationCorrelation(OperationId);

public sealed record AfterRunOperationCorrelation(
    OperationId OperationId,
    RunId CausalRunId) : OperationCorrelation(OperationId);
```

The positional declarations above document storage and value-equality shape;
they are not permission to skip invariants. A value that supports direct public
construction and must reject malformed representation uses an explicit
validating constructor or factory in its own source file. Every public-boundary
consumer, parser, serializer, and options validator also rejects empty or
default identities before any observable effect. Like every value type, a
default instance can exist in memory; it is never accepted as a valid domain
address. Closed `IIdentifierGenerator<TIdentifier>` registrations generate only
AgentKit-owned identities. They are injected, thread-safe for their registered
lifetime, and replaceable with deterministic generators in tests. External
provider/model identities come from validated configuration or responses and are
never fabricated by a local ID generator.

`ContentHash`, `IRandomizerFactory`, operation-owned `IRandomizer`, and
`IContentHasher` have one canonical definition in
[foundation contracts](foundation-contracts.md). Composition reuses those
versioned algorithm/canonicalization values rather than redeclaring a digest
string or registering mutable random state as an engine service. These
foundation services depend on no runtime component, so provider selection,
security, artifact integrity, and retry jitter consume them without reverse
dependency edges.

### Agent definitions and catalog

An agent definition is declarative and immutable. It selects behavior already
registered in DI through typed component keys; it does not contain service
instances. Different definitions in one engine may choose different keyed loops,
context assemblers, model selectors, stores, policies, tools, and output
contracts.

```csharp
namespace AgentKit;

public readonly record struct ComponentKey<TContract>(string Value)
    where TContract : class;

public sealed record AgentComponentSelection(
    ComponentKey<IAgentLoop> Loop,
    ComponentKey<IRunContinuationPolicy> ContinuationPolicy,
    ComponentKey<IInputCoordinator> Input,
    ComponentKey<IOutputPublisher> Output,
    ComponentKey<IOutputProcessor> OutputProcessor,
    ComponentKey<IContextAssembler> Context,
    ComponentKey<IModelSelector> ModelSelector,
    ComponentKey<IModelRequestExecutor> ModelExecutor,
    BudgetProfileKey BudgetProfile);

public sealed record AgentOptionalCapabilitySelection(
    ComponentKey<IToolExecutor>? ToolExecutor,
    ComponentKey<IArtifactCoordinator>? ArtifactCoordinator,
    DurabilityProfileKey? DurabilityProfile,
    MemoryProfileKey? MemoryProfile,
    GoalProfileKey? GoalProfile,
    ImmutableArray<AgentCapabilityReference> Capabilities);

public sealed record AgentDefinition(
    AgentId Id,
    AgentDefinitionRevision Revision,
    string DisplayName,
    AgentComponentSelection Components,
    SessionProfileKey SessionProfile,
    HookProfileKey HookProfile,
    SecurityProfileKey SecurityProfile,
    AgentOptionalCapabilitySelection OptionalCapabilities,
    ModelSelectionPolicy Models,
    ImmutableArray<InstructionSource> Instructions,
    ImmutableArray<ToolsetReference> Toolsets,
    RunPolicyDefaults RunDefaults,
    OutputDefinition Output,
    ExtensionData Extensions);

public sealed record AgentCatalogSnapshot(
    AgentCatalogVersion Version,
    ImmutableArray<AgentDefinition> Definitions);

public sealed record AgentDefinitionSourceSnapshot(
    AgentDefinitionSourceId SourceId,
    AgentDefinitionSourceVersion Version,
    int Precedence,
    ImmutableArray<AgentDefinition> Definitions);

public abstract record AgentDefinitionResolution;

public sealed record ResolvedAgentDefinition(
    AgentDefinition Definition,
    AgentCatalogVersion CatalogVersion) : AgentDefinitionResolution;

public sealed record AgentDefinitionNotFound(AgentId AgentId)
    : AgentDefinitionResolution;

public sealed record InvalidAgentDefinition(
    AgentId AgentId,
    ImmutableArray<CompositionDiagnostic> Diagnostics)
    : AgentDefinitionResolution;

public interface IAgentDefinitionSource
{
    AgentDefinitionSourceId SourceId { get; }

    ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface IAgentDefinitionCatalog
{
    AgentCatalogSnapshot? CurrentSnapshot { get; }

    bool SupportsDynamicPublication { get; }

    ValueTask<AgentCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);

    ValueTask<AgentDefinitionResolution> ResolveAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default);
}
```

The first-party catalog in `AgentKit` composes additive definition sources into
one immutable, versioned snapshot. It is thread-safe, resolves source precedence
under the rules below, and publishes a new snapshot only after complete
validation. Invalid reloads leave the previous snapshot active. A custom catalog
may load definitions remotely, but it must preserve the same snapshot,
validation, cancellation, and concurrency semantics.

`CurrentSnapshot` is a synchronous, memory-only view of the last completely
validated publication. It returns `null` when no initial publication is ready;
it never invokes a source, starts a refresh, blocks on a task, or resolves a
secret. Build and host readiness consume this property. Async reads and refresh
remain separate operations and cannot satisfy readiness by being synchronously
waited inside build, even when a particular source usually completes inline.

The first-party catalog composes explicit bootstrap source snapshots before
exposing a runnable engine. Code registrations provide their immutable snapshot
alongside their source. A host loads remote sources asynchronously through its
trusted bootstrap boundary, then registers their `AgentDefinitionSourceSnapshot`
values before build. Bootstrap must cover every configured source identity;
missing coverage is not-ready, while duplicate or mismatched source identities
fail composition. Bootstrap and refresh apply the same publication validation
and precedence rules. A fixed catalog still participates in admission
revalidation; its capability flag never grants permanent execution permission.

For each `AgentId`, the highest declared source precedence wins.
Equal-precedence sources must supply identical revision and canonical content or
publication fails; registration order never breaks that tie. Within one source,
duplicate IDs are invalid except equivalent idempotent registration. A published
`(AgentId, Revision)` is never rebound to different content. Replacing content
advances the revision, including an intentional rollback to an earlier design.
Removal in a newer snapshot prevents new engine-level resolution.

A resolved `Agent` pins its definition, not permission to execute it forever.
Before each new admission, activation checks that the pinned revision remains
enabled and that its exact component/profile versions are retained. Replacement
may leave an older revision explicitly enabled; removal or revocation rejects
new work through existing handles. Running operations keep their captured
snapshots, subject to live revocation. Recovery may resolve a retained disabled
revision only to reconcile or settle its existing operation, or under an
explicit authorized continuation policy. Reload never silently upgrades a
handle, an open operation, or an existing session's store.

### Process-level execution surface

```csharp
namespace AgentKit;

public sealed record AgentRunRequest(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    ExecutionIdentity Identity,
    AgentInput Input,
    AgentRunOptions? Options = null,
    ExecutionLaneId? ExecutionLaneId = null);

public sealed record AgentSessionCreateRequest(
    AgentId AgentId,
    ExecutionIdentity Identity,
    ConversationId? ConversationId,
    IdempotencyKey IdempotencyKey,
    ExtensionData Extensions);

public abstract record AgentSessionCreationResult;

public sealed record AgentSessionCreated(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    bool Existing) : AgentSessionCreationResult;

public sealed record AgentSessionCreationFailed(
    AgentId AgentId,
    SessionCreationFailure Failure) : AgentSessionCreationResult;

public abstract record AgentResolution;

public sealed record ResolvedAgent(Agent Agent) : AgentResolution;

public sealed record AgentNotFound(AgentId AgentId) : AgentResolution;

public sealed record InvalidAgent(
    AgentId AgentId,
    ImmutableArray<CompositionDiagnostic> Diagnostics) : AgentResolution;

public sealed class Agent
{
    private readonly AgentEngineRuntime runtime;

    internal Agent(
        AgentEngineRuntime runtime,
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion)
    {
        this.runtime = runtime;
        Definition = definition;
        CatalogVersion = catalogVersion;
    }

    public AgentId Id => Definition.Id;
    public AgentDefinition Definition { get; }
    public AgentCatalogVersion CatalogVersion { get; }

    public Task<AgentSessionCreationResult> CreateSessionAsync(
        ExecutionIdentity identity,
        IdempotencyKey idempotencyKey,
        ConversationId? conversationId = null,
        ExtensionData? extensions = null,
        CancellationToken cancellationToken = default) =>
        runtime.CreateSessionAsync(
            new AgentSessionCreateRequest(
                Id,
                identity,
                conversationId,
                idempotencyKey,
                extensions ?? ExtensionData.Empty),
            cancellationToken);

    public Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ConversationId? conversationId = null,
        AgentRunOptions? options = null,
        ExecutionLaneId? executionLaneId = null,
        CancellationToken cancellationToken = default) =>
        runtime.RunAsync<TOutput>(
            this,
            sessionId,
            conversationId,
            identity,
            input,
            options,
            executionLaneId,
            cancellationToken);

    public Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ConversationId? conversationId = null,
        AgentRunOptions? options = null,
        ExecutionLaneId? executionLaneId = null,
        CancellationToken cancellationToken = default) =>
        runtime.StreamAsync<TOutput>(
            this,
            sessionId,
            conversationId,
            identity,
            input,
            options,
            executionLaneId,
            cancellationToken);
}

public sealed class AgentEngine : IAsyncDisposable
{
    private readonly AgentEngineRuntime runtime;

    internal AgentEngine(AgentEngineRuntime runtime)
    {
        this.runtime = runtime;
    }

    public static AgentEngineBuilder CreateBuilder() => new();

    public ValueTask<AgentCatalogSnapshot> GetAgentsAsync(
        CancellationToken cancellationToken = default) =>
        runtime.GetAgentsAsync(cancellationToken);

    public ValueTask<AgentResolution> GetAgentAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default) =>
        runtime.GetAgentAsync(agentId, cancellationToken);

    public Task<AgentSessionCreationResult> CreateSessionAsync(
        AgentSessionCreateRequest request,
        CancellationToken cancellationToken = default) =>
        runtime.CreateSessionAsync(request, cancellationToken);

    public Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken = default) =>
        runtime.RunAsync<TOutput>(request, cancellationToken);

    public Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken = default) =>
        runtime.StreamAsync<TOutput>(request, cancellationToken);

    public ValueTask DisposeAsync() => runtime.DisposeAsync();
}

public sealed class AgentEngineBuilder
{
    public IServiceCollection Services { get; } = new ServiceCollection();

    public AgentEngine Build() => AgentKitRegistration.Build(Services);
}
```

`RunAsync` is inherently asynchronous and awaits settlement. `StreamAsync`
asynchronously creates a live subscription whose completion returns the same
settled result. `GetAgentAsync` returns a public immutable `Agent` handle bound
to one validated definition revision and catalog version. The handle is safe for
concurrent use, owns no mutable session or run state, delegates through the
engine's internal run-scope boundary, and never exposes `IServiceProvider`. The
engine-level run methods are convenience forms of resolving that handle and
running it.

`AgentEngineRuntime` is a package-internal lifecycle coordinator in its own
file. It owns the standalone or host-managed scope boundary and is not a public
extension point; all behavior it invokes remains selected through the public
abstractions, definitions, options, and DI registrations described here.

### Session creation failure evidence

```csharp
namespace AgentKit;

public enum SessionCreationFailureKind
{
    AgentUnavailable,
    AuthorizationUnavailable,
    StoreRejected
}

public sealed record SessionCreationFailure(
    SessionCreationFailureKind Kind,
    string SafeMessage);
```

`SessionCreationFailure` is the safe, closed evidence
`AgentSessionCreationFailed` carries. `AgentUnavailable` reports that the
catalog no longer enables the requested agent (removed, disabled, or replaced
since the caller last resolved it). `AuthorizationUnavailable` reports that
fresh authorization could not be captured for the creating identity.
`StoreRejected` reports that the selected `ISessionCoordinator` returned a
result other than `SessionCreated`. The kind is a closed enum, not open text, so
a caller can branch on cause without parsing `SafeMessage`; `SafeMessage` itself
never repeats caller-supplied content and is safe to log or surface to an end
user. This mirrors `ContextPreparationFailure` and
`OutputSchemaConfigurationFailure`'s kind-plus-safe-message shape rather than
introducing a new failure-evidence pattern.

### Current admission surface

The process-level surface above is the target shape. The facade currently ships
its reduced form, `Agent.SendAsync(AgentSendRequest)`, which already gives one
engine the coordination role the invariant requires:

- `AgentSendRequest` carries the identity, the user content parts, an optional
  `SessionId`, narrowing overrides, and an `IAgentRunObserver`.
- The engine revalidates the pinned definition and publication, then creates a
  session owned by the identity or loads the named one and verifies the agent,
  tenant, principal, and active state own it. A session that is not visible is
  an `AgentAdmissionRejectedException`, not a silent new session.
- A process-local lane per `(AgentId, SessionId)` is entered before anything is
  appended and applies the pinned session profile's `SessionBusyBehavior`:
  `Reject` fails the contender with `AgentSessionBusyException` naming the
  active run, `Wait` serializes it. Different sessions, of the same or different
  agents, run concurrently on the same engine. Distributed exclusion remains the
  session run coordinator's durable lease.
- The user message is appended under the run's identity with an idempotency key
  derived from it, the run request is compiled in a fresh scope with the keyed
  loop and services (including the optional output processor), and the loop
  settles the run. `AgentLoopResult.SessionId` is the resume token.

`AgentKit.Simple` exposes this through `AddAgent(agentId, configure)` for
additional definitions on the same engine and `engine.Identity` for the composed
identity; `engine.Conversation` remains the single-session convenience.
Queue-backed admission through `AgentKit.IO`, engine-level run attachment and
cancellation by `RunId`, and durable lane acceptance are the remaining steps to
the full surface.

### Compiled run activation

The facade creates one DI scope per run, then materializes every keyed choice
from the captured definition into an explicit immutable plan. This is the bridge
Microsoft DI does not provide automatically: a key selected for one agent never
implicitly flows into a constructor graph.

```csharp
namespace AgentKit.Internal;

internal sealed record AgentRunPlan(
    AgentDefinition Definition,
    AgentCatalogVersion CatalogVersion,
    IAgentLoop Loop,
    AgentRunServices Services,
    SessionExecutionCapability Session,
    HookDispatchContext Hooks,
    SecurityAuthorizationContext Authorization,
    AgentOptionalCapabilitySelection OptionalCapabilities);

internal abstract record AgentRunPlanCompilationResult;

internal sealed record CompiledAgentRunPlan(AgentRunPlan Plan) :
    AgentRunPlanCompilationResult;

internal sealed record InvalidAgentRunPlan(
    ImmutableArray<CompositionDiagnostic> Diagnostics) :
    AgentRunPlanCompilationResult;

internal interface IAgentRunPlanCompiler
{
    ValueTask<AgentRunPlanCompilationResult> CompileAsync(
        ResolvedAgentDefinition definition,
        AgentRunRequest request,
        CancellationToken cancellationToken);
}

internal interface IAgentRunScopeFactory
{
    ValueTask<AgentRunScopeLease> CreateAsync(
        ResolvedAgentDefinition definition,
        AgentRunRequest request,
        CancellationToken cancellationToken);
}

internal sealed class AgentRunScopeLease : IAsyncDisposable
{
    internal AgentRunScopeLease(
        AgentRunPlan plan,
        IAsyncDisposable ownedScope)
    {
        Plan = plan;
        OwnedScope = ownedScope;
    }

    public AgentRunPlan Plan { get; }

    private IAsyncDisposable OwnedScope { get; }

    public ValueTask DisposeAsync() => OwnedScope.DisposeAsync();
}
```

`AgentRunScopeFactory` is the sole owner of `IServiceScopeFactory` and arbitrary
keyed contract resolution. It creates the scope, resolves the scoped
`IAgentRunPlanCompiler`, and returns only the plan and an opaque lifetime lease.
Feature packages may use narrow package-internal activators for prevalidated
hook, tool, or contributor registrations within that scope; those activators
execute compiled factories and cannot query an arbitrary contract or key. The
compiler validates each contract/key pair, profile, optional capability, and
catalog version before any session mutation or provider traffic. Loops,
providers, tools, contributors, policies, and hooks receive the resolved
collaborators or typed execution contexts; none receives `IServiceProvider`,
`IKeyedServiceProvider`, or an ambient current-agent accessor.

## Package boundary

AgentKit references AgentKit.Abstractions, the shared exporter-free
AgentKit.Observability infrastructure, and the required Microsoft.Extensions
dependency-injection, options, configuration, and logging abstractions. It does
not reference behavioral runtime or integration packages. Applications add
AgentKit.Loop, AgentKit.Budgets, AgentKit.Context, AgentKit.Output,
AgentKit.Hooks, AgentKit.Session, a session store, the security implementation,
the provider runtime, the I/O coordinator, and one or more providers explicitly.
Every storage-owning runtime and its concrete adapter are separate explicit
registrations; no runtime package contributes an in-memory store or persistence
target as a convenience default.

Feature packages expose service collection extensions such as AddAgentLoop,
AddAgentBudgets, AddAgentHooks, AddAgentOutput, AddAgentPermissions, AddAgentIO,
AddAgentProviders, AddAgentSession, AddOpenAI, AddReadTool, and AddWriteTool.
They use the same registration path whether the application starts with
AgentEngineBuilder, ASP.NET Core, a worker host, or a custom service collection.

## Build validation

Validation is staged. Registration helpers collect and validate local
descriptors without I/O. Synchronous `Build()` validates a materialized initial
catalog, registered graphs, options, and declared capabilities; it never blocks
on `ReadAsync`, fetches secrets, probes a model, or starts application work. A
host needing remote configuration materializes it asynchronously under its
trusted bootstrap policy before building or exposing the engine. Hosted
composition follows the same readiness rule.

| Boundary                    | Required evidence                                                                                                                       | Failure behavior                                              |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| Build or host readiness     | Nonempty validated definition snapshot, complete selected graphs, explicit external bindings, compatible declared capabilities          | No runnable engine is exposed                                 |
| Catalog/profile publication | Complete candidate snapshot, unambiguous references, valid versions and lifetimes                                                       | Keep the last valid snapshot                                  |
| Admission and activation    | Enabled captured definition, exact profile set, authenticated identity, valid lane/session address, requested output-type compatibility | Typed rejection before run acceptance or protected effects    |
| Each protected effect       | Current grant state, exact concrete request, deadline, budget and fencing evidence                                                      | Deny, conflict, limit, or recovery outcome before new effects |

Build proves configuration consistency, not remote availability or the behavior
of arbitrary application code. Structural graph validation does not execute user
factories to discover hidden dependencies or prove that every custom loop
terminates. Factories declare dependencies; contract tests verify their actual
activation and lifecycle. External availability and expiring credentials are
checked at their operation boundary, with typed failures.

Base registration of an optional subsystem does not enable every operation it
can implement. Validate the base services when registered and the complete
dependency closure of each enabled profile, operation, and published definition.
For example, registering the provider runtime does not require an embedding
model until an embedding operation is configured. Invalid explicitly configured
operations still fail validation even if no current agent selects them.

A valid engine has one effective engine-wide definition catalog, run-scope
factory, composition validator, session directory/store catalog/selector, hook
dispatch kernel/point-definition catalog/profile selector, security authority
selector/policy catalog, approval broker, model catalog, provider-profile
runtime selector, budget authority, `TimeProvider`, `IRandomizerFactory`, and
`IContentHasher`. For every published agent definition, key and profile
resolution must produce exactly one effective loop, continuation policy, input
coordinator, output publisher, output processor, context assembler, run budget
profile, session coordinator/run coordinator/profile and store, hook profile,
security authority/profile, model selector, and model request executor, plus at
least one compatible conversational model. Several keyed implementations and
profiles may coexist; ambiguity means a definition failed to select one, not
that the whole process must use one global implementation.

The effective security composition also resolves exactly one grant store, and
every enabled storage capability resolves its selected adapter and immutable
capability descriptor. Build rejects missing or ambiguous adapters, absent
targets, an ephemeral adapter selected for durable work, and transaction or
fencing requirements stronger than the adapter advertises. It never substitutes
an in-memory adapter or selects the last registration.

`TimeProvider.System` is a replaceable `TryAdd` default when the host has not
supplied another instance. The first-party security package registers
fail-closed policy and approval defaults that deny or defer rather than invent
authority. Provider credentials, endpoints, principal identity, external
resource authority, and production persistence never receive fabricated
defaults.

Every configured provider operation must resolve one immutable descriptor, one
keyed/versioned endpoint and service-surface profile, and one keyed/versioned
credential/account profile. Missing, ambiguous, or cross-provider bindings fail
composition validation. Those profiles are captured with the operation factory;
runtime adapters do not resolve an unkeyed credential/options service whose
meaning depends on registration order.

A selected tool runtime also resolves one terminal recorder, one versioned
result-projection policy catalog, and one deterministic projector. Every
captured projection policy remains resolvable for the retention period of calls
that may still require history materialization; a missing version fails
publication rather than selecting the current policy or repeating the effect.

Tools, skills, memory, embeddings, reranking, goals, MCP, identity adapters,
artifacts, evaluation, and extra contributors are optional. An enabled optional
capability requires its complete selected collaborators; registering a base
runtime does not enable every optional operation. Build fails with
component-specific diagnostics for missing services, duplicate singular
registrations, invalid scopes, ambiguous keys, impossible limits, unsafe retry
combinations, or incompatible capabilities.

Build validation also constructs the declared closed component dependency graph
from typed registration descriptors. Each descriptor records contract, key,
implementation, lifetime, direct dependencies, and any explicit operation-owned
factory boundary. The validator combines those descriptors with Microsoft DI
scope/build validation, rejects every strongly connected component, and reports
the complete cycle path.

Selectable components registered through opaque factories must supply an
equivalent descriptor. `Lazy<T>`, `Func<T>`, nested scopes, or a
service-provider lookup do not make a dependency cycle valid; they only postpone
it. An explicit factory is allowed when it creates a separately acyclic
operation graph with a documented owner and disposal boundary.

`AgentOptionalCapabilitySelection` makes demonstrated optional runtime axes
explicit instead of smuggling them through `ExtensionData`. A non-null
durability, memory, or goal profile and every neutral capability reference must
resolve to its registered package, profile, implementation, store or transport,
security policy, and required collaborators. Selection without registration
fails definition validation; omission means the agent does not have that
capability. A capability reference has no `Required` flag: selecting a profile
is itself the requirement to resolve it completely. The former three-argument
reference is intentionally removed; callers omit unavailable capabilities before
publishing a definition rather than tolerate a missing selected profile. MCP
endpoints are selected inside an MCP-owned capability profile, so the core
definition does not depend on an MCP package type. Tools remain the typed
`ToolsetReference` collection because tool selection and authorization have
their own catalog contract; the optional executor key selects the one top-level
`IToolExecutor` used by the loop. Toolsets without that key, or an executor key
without toolsets, fail definition validation.

Embedding and reranking used by memory are configured only by the selected
`MemoryProfileKey`; their selector, executor, and operation policy are captured
as one profile snapshot. Other packages expose their own purpose-specific
capability profiles through `AgentCapabilityReference`. The same operation is
never configured once in core and again in a feature profile.

Validation runs before the first request. Hosted applications receive the same
checks during host validation or initial facade resolution; they do not discover
a missing provider halfway through a run.

Static definitions are validated during `Build()` or host startup. Dynamic
definition sources validate before publishing each new catalog version. A run
against an unknown, removed, or invalid definition returns a typed resolution
outcome before creating session mutations or provider traffic.

Validation requires at least one runnable agent definition. A catalog may
publish later versions, but dynamic publication does not turn an empty catalog
into a runnable engine or defer the initial composition proof. A deployment that
must wait for remote definitions owns that bootstrap state outside `AgentEngine`
and builds or exposes the engine only after a valid snapshot is available.

## Agent definition and run scope

An agent definition is a reusable blueprint in the engine catalog. It identifies
the loop, models, context contributors, tool sources, policies, stores, output
contract, limits, and extensions that form one agent. It is immutable after
validation and safe to share between concurrent runs. Resolving a definition
captures its revision and catalog version; a later reload affects only new runs
or named next-turn boundaries, never an in-flight operation by accident.

A run scope contains mutable execution state: current turn, budget reservations,
run-scoped extension state, request snapshots, and cancellation. Nothing in the
run scope leaks into another run. Session state is loaded through the session
component; it is not kept indefinitely in a singleton agent object.

Two agents may run concurrently in one engine, and one definition may support
many concurrent sessions. The session executor enforces one active operation per
execution lane and one serialized durable mutation line per session. Different
lanes may overlap effects. An omitted lane selects the persisted default lane
only when the session profile defines one unambiguously; it never means
whichever lane is idle. A host exposing multiple lanes includes a typed
`ExecutionLaneId` in its admission and control requests.

## Registration model

Registrations declare one of three shapes:

- singular registrations have one effective implementation and an explicit
  replacement path;
- additive registrations preserve deterministic order for context contributors,
  tool providers, hooks, observers, policies, and similar collections; and
- named or keyed registrations support multiple providers, models, stores, or
  other selectable implementations without losing stable identity.

Repeated package registration is idempotent where practical. Collisions are
either rejected or resolved by documented precedence. Registration extensions
return the service collection and never build or resolve a provider.

Singular means one effective service **for a selected key** unless the contract
is explicitly engine-wide. Feature defaults use `TryAdd` at their documented
default key. Additive registrations carry stable identity and deterministic
order. Keyed replacement replaces exactly one contract/key pair; it cannot
silently evict a different provider operation or another agent's selection.

The minimal composition surface has this C# 14 shape; method bodies delegate to
package-internal registration helpers and never call `BuildServiceProvider`:

```csharp
namespace AgentKit;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentKit(
            Action<AgentEngineOptions>? configure = null) =>
            AgentKitRegistration.Add(services, configure);

        public IServiceCollection AddAgentDefinition(AgentDefinition definition) =>
            AgentKitRegistration.AddDefinition(services, definition);

        public IServiceCollection AddAgentDefinitionSource<TSource>()
            where TSource : class, IAgentDefinitionSource =>
            AgentKitRegistration.AddDefinitionSource<TSource>(services);

        public IServiceCollection AddAgentDefinitionSnapshot(
            AgentDefinitionSourceSnapshot snapshot) =>
            AgentKitRegistration.AddDefinitionSnapshot(services, snapshot);

        public IServiceCollection ReplaceAgentDefinitionCatalog<TCatalog>()
            where TCatalog : class, IAgentDefinitionCatalog =>
            AgentKitRegistration.ReplaceDefinitionCatalog<TCatalog>(services);

        public IServiceCollection ReplaceIdentifierGenerator<
            TIdentifier,
            TGenerator>()
            where TIdentifier : struct
            where TGenerator : class, IIdentifierGenerator<TIdentifier> =>
            AgentKitRegistration.ReplaceIdentifierGenerator<
                TIdentifier,
                TGenerator>(services);
    }
}
```

`AddAgentDefinition` is additive and idempotent only for the same identity,
revision, and canonical content. Conflicting registrations fail validation.
`IAgentDefinitionCatalog` is an engine-wide singular with an explicit
replacement path. Definition sources are additive. Closed identity generators
are singular per ID type and replaceable independently.

## Configuration

The [configuration merge contract](../concepts/configuration-and-overrides.md)
defines how these layers combine and when a captured value may change.

Configuration is layered immutable input. Library defaults, host settings,
managed policy, external-resource settings, agent definition, composed
capabilities, run options, and next-turn overrides have explicit precedence.
Each value also declares how it combines: replacement, append, keyed merge, deep
merge, ordered rules, or explicit reset.

Security constraints are not ordinary overridable values. Untrusted
external-resource configuration cannot load executable extensions, inject
credentials, or widen tool, filesystem, network, or model authority. Invalid
reloads leave the last known-good snapshot active. In-flight work continues with
its captured snapshot.

Reload changes immutable definitions, profiles, and selections among registered
components. It does not mutate the DI implementation behind a captured key in
the built engine. Replacement implementations require a new composition with
explicit ownership of old and new scopes. Security selection follows the
[publication and activation contract](permissions-and-human-control.md#selection-publication-and-activation),
including retained implementation bindings for delayed work.

The effective result is a complete semantic snapshot, not a configuration
version paired with an extension bag:

```csharp
namespace AgentKit;

public readonly record struct ConfigurationSourceId(string Value);

public readonly record struct ConfigurationPath(string Value);

public readonly record struct ConfigurationSourceVersion(long Value);

public enum ConfigurationLayerKind
{
    LibraryDefaults,
    HostGlobal,
    TrustedOrganizationPolicy,
    ApplicationOrExternalResource,
    AgentDefinition,
    ComposedCapabilities,
    RunInvocation,
    NamedNextTurnOverride
}

public enum ConfigurationMergeOperation
{
    Replace,
    DeepMerge,
    Append,
    KeyedMerge,
    RuleList,
    Reset
}

public enum ConfigurationTrustClass
{
    Untrusted,
    HostEstablished
}

public sealed record ConfigurationSourceReference(
    ConfigurationSourceId SourceId,
    ConfigurationSourceVersion SourceVersion,
    ConfigurationLayerKind Layer,
    ConfigurationTrustClass Trust,
    ContentHash Fingerprint);

public sealed record EffectiveConfigurationEntry(
    ConfigurationPath Path,
    ConfigurationMergeOperation MergeOperation,
    ConfigurationSemanticValue Value,
    ImmutableArray<ConfigurationSourceReference> Contributors);

public abstract record ConfigurationSemanticValue;

public sealed record ConfigurationJsonValue(
    JsonElement Value) : ConfigurationSemanticValue;

public sealed record SecurityProfileConfigurationValue(
    SecurityProfilePublication Publication) : ConfigurationSemanticValue;

public sealed record SessionProfileConfigurationValue(
    SessionProfileReference Profile) : ConfigurationSemanticValue;

public sealed record ModelSelectionConfigurationValue(
    ModelSelectionPolicy Policy) : ConfigurationSemanticValue;

public sealed record ToolsetConfigurationValue(
    ToolsetPublication Publication) : ConfigurationSemanticValue;

public sealed record EffectiveConfigurationSnapshot(
    ConfigurationVersion Version,
    ContentHash Fingerprint,
    ImmutableArray<EffectiveConfigurationEntry> Entries,
    ImmutableArray<ConfigurationSourceReference> Sources);
```

`ConfigurationSourceId` and `ConfigurationPath` are nonblank ordinal identity
values. A source ID identifies one publisher; a path identifies one
namespace-qualified semantic setting or selection. The compiler validates the
path's declared namespace and setting grammar, rather than treating a slash or
dot convention as authority. `ConfigurationSourceVersion` is a positive,
monotonically published long revision. `ConfigurationLayerKind` is exactly the
eight layers named by the configuration concept, and
`ConfigurationMergeOperation` is exactly its six declared operations.

`ConfigurationTrustClass.Untrusted` and `HostEstablished` are the initial closed
classes. The host-owned source binding establishes this value before content is
accepted; a file, remote response, or its extension data cannot self-promote.
Trust classifies source handling only: it neither grants authority nor overrides
a managed constraint.

`ConfigurationPath` is a nonblank, namespace-qualified semantic setting or
selection identity. `ConfigurationSemanticValue` is an owned closed family, not
arbitrary `ExtensionData`. `ConfigurationJsonValue` owns a clone of one bounded
JSON scalar, array, or object used only for a setting whose declared schema
admits document data. The initial typed selection cases retain the actual
immutable contracts used by compiled run publication:

- `SecurityProfileConfigurationValue` retains the complete
  `SecurityProfilePublication`, including its agent, definition and
  configuration coordinates, profile key/version, policy snapshot reference, and
  authority key;
- `SessionProfileConfigurationValue` retains the exact `SessionProfileReference`
  key and version;
- `ModelSelectionConfigurationValue` retains the immutable
  `ModelSelectionPolicy`; and
- `ToolsetConfigurationValue` retains one complete `ToolsetPublication`,
  including its key/version, execution-policy reference, source selection, and
  aliases.

No case converts a string into a profile, component, or catalog selection. A new
selection family adds an explicit typed value case, canonical codec support, and
merge-schema coverage in its owning contract; until then, compilation fails
rather than omitting the selection or placing it in JSON. Provider endpoints,
service surfaces, credential accounts, security policies, stores, and similar
external facts remain exact references to their owning classified profile
snapshots. Existing typed references retain their own exact key/version or
fingerprint evidence; the configuration layer does not invent an additional
fingerprint where their contract defines none. Secret material never enters a
configuration value, source reference, fingerprint input, diagnostic, or durable
manifest.

`Entries` is the complete effective semantic configuration after merge, ordered
canonically by path. Each entry retains the ordered source contributors that
participated in its merge or reset. `Sources` is the complete distinct source
publication closure, ordered by each run's configured layer precedence and then
stable source identity. The eight-layer list is the default, not a hidden
universal order. Equal-layer contributor order is deterministic and retained;
source order and complete contributor closure are inputs to canonical
fingerprinting. The snapshot fingerprint is computed from the canonical semantic
entries and source publication references; it does not depend on dictionary
enumeration, object identity, secret resolution, or wall-clock time. Equal
version and fingerprint values cannot be rebound to different content.

Construction checks local value shape, initialized ownership, canonical path
order, duplicate paths, and complete source/contributor closure. It does not
prove that an arbitrary entry is legal for a setting. The configuration compiler
consumes declared setting schemas and merge metadata to prove that every
effective path is present exactly when required, uses its permitted typed value
case, follows its declared merge operation, and satisfies cross-setting
selection constraints before publication.

One configuration publication authority coordinates narrow replaceable
collaborators for source discovery, schema-aware merge, canonical encoding and
fingerprinting, and retained snapshot storage. It does not absorb those
contracts into one implementation interface. Run and named turn overrides
request a new publication through that authority; callers do not edit a captured
snapshot. Invalid candidates leave the last known-good snapshot published. A
compiled run publication binds the exact `EffectiveConfigurationSnapshot`
instance alongside its definition, session, hook, security, model, toolset,
budget, and output selections. Admission revalidates that publication and passes
the same snapshot into the run scope. It never reconstructs effective
configuration from live options, `IConfiguration`, an extension bag, or current
profile catalogs.

A durable operation records or can derive the snapshot version, fingerprint, and
complete source publication closure. Recovery resolves that retained exact
snapshot or applies an explicit authorized migration; it never substitutes the
latest configuration merely because the earlier publisher is unavailable.

Credential profile selection is explicit provider composition, while secret
material is resolved by the selected leaf integration for each send. Secrets
never enter AgentEngineBuilder, agent definitions, options display, context
manifests, messages, durable records, or profile keys. A branded package may
offer an opt-in well-known endpoint profile, but absence of endpoint
configuration never selects it implicitly.

Every behaviorally meaningful choice has one declared configuration home:

- process behavior and safe feature defaults use typed DI options validated on
  build or host start;
- per-agent selections and defaults use immutable `AgentDefinition` values;
- invocation-specific changes use immutable `AgentRunOptions`;
- next-turn changes use named, expiring override values; and
- implementation replacement, additive behavior, and policy strategy use DI.

Hidden constants are permitted only for protocol invariants. Retry counts,
timeouts, queue and stream bounds, ordering, fallback, downgrade, model/tool
selection, compaction, output validation, settlement, and observer failure
behavior are options or strategies with documented safe defaults. An option
cannot widen managed security constraints, and a definition cannot name an
implementation that DI did not register.

## Ownership and disposal

In standalone mode AgentEngine owns the provider created by its builder and is
asynchronously disposable. In hosted mode the host owns the provider and
AgentEngine never disposes it. Owned services are disposed exactly once.

`AgentEngine`, immutable definitions, descriptors, and thread-safe catalogs may
be singleton. Run plans, budgets, mutable capability instances, and execution
state are scoped. Operation adapters may be transient. Catalog reload work has
an explicit hosted lifetime and shuts down before its owned provider. Every
public service documents threading, ownership, disposal, and whether it may
retain state across runs.

A singleton may not capture a run-scoped service. Concurrent scopes never share
mutable agent state. Disposal or cancellation of one run cannot dispose a
catalog, provider adapter, or shared immutable definition needed by another run.

Shutdown first seals admission and creation of new local effects, then drains,
cancels, or durably hands off accepted work under the configured bounded policy.
Run-owned scopes are released only after their tasks finish or transfer to an
explicit owner. Root-provider disposal comes last; a timed-out drain cannot
dispose a provider still used by an untracked task. A host that elects to leave
an operation recoverable persists that state and releases local drive ownership
without fabricating `RunSettled`. Repeated disposal is idempotent, and an
already disposed engine rejects new operations deterministically.

## Related concept specifications

- [Agent definition and run context](../concepts/agent-definition-and-run-context.md)
- [Configuration and overrides](../concepts/configuration-and-overrides.md)
- [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)
