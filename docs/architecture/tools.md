# Tools

**Role:** Turn model-proposed operations into bounded, authorized, observable
application work.

The model may request a tool call. It never invokes application code directly.
The tool component separates every stage so discovery, policy, execution, and
recording remain independently replaceable.

## Package model

AgentKit.Tools contains the optional first-party tool runtime: catalog,
resolution, validation, scheduling, invocation, normalization, and result
coordination. The contracts live in AgentKit.Abstractions, so another runtime
can replace it without referencing this package.

`AgentEngine` is the process-level host and may run many agent definitions at
once. Tool availability and execution policy are therefore captured for the
specific `AgentId`, `SessionId`, and `RunId`; neither the catalog nor the
scheduler has a mutable "current agent". Runs for different agents may execute
concurrently without sharing mutable call or budget state.

Individual tools are feature packages named AgentKit.Tools.ToolName. The first
set includes AgentKit.Tools.Read, AgentKit.Tools.Write, AgentKit.Tools.Skill,
and AgentKit.Tools.Web. Process-backed tools follow the same naming rule when
introduced. Each package owns its descriptor, invoker, options, registration,
and tests. AddReadTool, AddWriteTool, AddSkillTool, and AddWebTool register
their features through the service collection.

AgentKit.Tools.Skill deliberately contributes two capabilities: the skill tool
and the context contributor that describes available skills to the model. Both
use the same source identity and are registered together. File-aware tools
depend on file-system abstractions and never on AgentKit.FileSystem itself. Web
tools depend on network abstractions and never create an unrestricted HTTP
client. Process-backed tools depend on process abstractions and never start an
operating-system process directly.

## Normative minimal contract shape

The following C# 14 shapes are normative and minimal rather than an exhaustive
API listing. Every named type is defined in its own same-named file. `AgentId`,
`SessionId`, `RunId`, `OperationId`, `ToolCallId`, and
`IIdentifierGenerator<TIdentifier>` are shared AgentKit.Abstractions contracts.
Tool-specific identity remains strongly typed as well:

```csharp
namespace AgentKit;

public readonly record struct ToolId(string Value);

public readonly record struct ToolSourceId(string Value);

public readonly record struct ToolCatalogVersion(string Value);

public readonly record struct ToolSourceVersion(string Value);

public readonly record struct ToolVersion(string Value);

public readonly record struct ToolAlias(string Value);

public readonly record struct ToolIdentity(
    ToolId Id,
    ToolVersion Version);

public readonly record struct ToolsetKey(string Value);

public readonly record struct ToolExecutionPolicyKey(string Value);
```

Runtime-generated call and operation identities come from injected
`IIdentifierGenerator<ToolCallId>` and `IIdentifierGenerator<OperationId>`
services. Provider call IDs are correlated values, never substituted for
AgentKit identity.

Description is immutable and independent from discovery, resolution, policy,
execution, state, and observation:

```csharp
namespace AgentKit;

public sealed record ToolDescriptor(
    ToolId Id,
    ToolVersion Version,
    string Name,
    string Description,
    JsonSchema InputSchema,
    JsonSchema? OutputSchema,
    ToolEffects Effects,
    ToolExecutionHints ExecutionHints,
    ToolSourceId SourceId,
    ExtensionData Extensions);

public sealed record ToolsetReference(
    ToolsetKey Key,
    ToolExecutionPolicyKey ExecutionPolicyKey);

public sealed record ToolDiscoveryRequest(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    AgentDefinitionRevision AgentDefinitionRevision,
    ImmutableArray<ToolsetReference> Toolsets,
    ModelCapabilities ModelCapabilities);

public sealed record ToolProviderSnapshot(
    ToolSourceId SourceId,
    ToolSourceVersion SourceVersion,
    ImmutableArray<ToolDescriptor> Tools);

public sealed record ToolCatalogSnapshot(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    ToolCatalogVersion Version,
    ImmutableArray<ToolDescriptor> Tools,
    ImmutableDictionary<ToolAlias, ToolIdentity> ProviderAliases);
```

The snapshot is bound to one run. A provider alias maps to one exact
`ToolId`/`ToolVersion` in that snapshot and is not resolved against live DI
registrations later.

```csharp
namespace AgentKit;

public sealed record ToolCallRequest(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    SecurityAuthorizationContext Authorization,
    ToolCatalogVersion CatalogVersion,
    int SourceOrdinal,
    ToolAlias ProviderAlias,
    ImmutableArray<byte> RawArguments,
    DateTimeOffset RequestedAt);

public sealed record ResolvedToolCall(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    SecurityAuthorizationContext Authorization,
    ToolDescriptor Tool,
    ToolVersion ToolVersion,
    int SourceOrdinal,
    ImmutableArray<byte> RawArguments,
    DateTimeOffset RequestedAt,
    DateTimeOffset ResolvedAt);

public sealed record ValidatedToolCall(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    SecurityAuthorizationContext Authorization,
    ToolDescriptor Tool,
    ToolVersion ToolVersion,
    int SourceOrdinal,
    JsonElement Arguments,
    InputFingerprint InputFingerprint,
    ToolExecutionPlan ExecutionPlan,
    DateTimeOffset RequestedAt,
    DateTimeOffset ValidatedAt);

public sealed record ToolInvocationContext(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    ToolDescriptor Tool,
    ToolVersion ToolVersion,
    JsonElement Arguments,
    SecurityGrant InvocationGrant,
    int Attempt,
    DateTimeOffset RequestedAt,
    DateTimeOffset InvocationStartedAt,
    DateTimeOffset Deadline,
    IToolProgressReporter Progress);

public sealed record ToolCallResult(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    SecurityAuthorizationContext Authorization,
    GrantId? GrantId,
    ToolId? ToolId,
    ToolVersion? ToolVersion,
    ToolTerminalStatus Status,
    ImmutableArray<ToolResultContent> Content,
    ToolError? Error,
    SideEffectCertainty SideEffectCertainty,
    ToolUsage Usage,
    bool Retryable,
    DateTimeOffset RequestedAt,
    DateTimeOffset? InvocationStartedAt,
    DateTimeOffset CompletedAt,
    ExtensionData Extensions);
```

`ToolCallResult.Status` is authoritative. Construction validation rejects
impossible combinations such as `Succeeded` without a resolved tool/version,
`Succeeded` with an error, `Denied` with a claim that invocation ran, a
descriptor/version mismatch, timestamps outside
`RequestedAt <= InvocationStartedAt <= CompletedAt`, or a retryable unknown
mutating effect. Calls that terminate before invocation leave
`InvocationStartedAt` null but still carry required request and completion
timestamps.

Provider adapters copy bounded argument bytes into an owned
`ImmutableArray<byte>` before constructing `ToolCallRequest`; a view over
caller-owned mutable storage is not a durable or concurrency-safe call payload.
Validated `JsonElement` values are cloned before publication or persistence, so
they never depend on a caller-owned `JsonDocument` lifetime. `TurnId`, exact
`ToolVersion`, authorization context, and timestamps flow unchanged through
resolution, authorization, recording, invocation, and the terminal result. The
optional `GrantId` is present only when a grant was issued. All framework
timestamps come from injected `TimeProvider`.

### Discovery, resolution, validation, and selection

```csharp
namespace AgentKit;

public interface IToolProvider
{
    ValueTask<ToolProviderSnapshot> DiscoverAsync(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IToolCatalog
{
    ValueTask<ToolCatalogSnapshot> CaptureAsync(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IToolResolver
{
    ValueTask<ToolResolutionResult> ResolveAsync(
        ToolCatalogSnapshot snapshot,
        ToolCallRequest request,
        CancellationToken cancellationToken);
}

public interface IToolArgumentValidator
{
    ValueTask<ToolArgumentValidationResult> ValidateAsync(
        ResolvedToolCall call,
        ToolArgumentLimits limits,
        CancellationToken cancellationToken);
}

public interface IToolExecutionPolicy
{
    ValueTask<ToolExecutionPlanResult> PlanAsync(
        ImmutableArray<ValidatedToolCall> calls,
        ToolExecutionPolicyContext context,
        CancellationToken cancellationToken);
}

public interface IToolExecutionPolicySelector
{
    ValueTask<ToolExecutionPolicySelectionResult> SelectAsync(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ToolExecutionPolicyKey key,
        CancellationToken cancellationToken);
}
```

Providers are additive discovery sources. The catalog combines their immutable
snapshots; the resolver binds identity; the validator handles bounded canonical
schema validation; and the execution policy chooses scheduling, result, and
retry behavior. `ToolResolutionResult`, `ToolArgumentValidationResult`, and
`ToolExecutionPlanResult` are discriminated results, not nullable successes.

### Execution, durable state, and observation

```csharp
namespace AgentKit;

public interface IToolInvoker
{
    ValueTask<ToolCallResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken);
}

public interface IToolScheduler
{
    Task<ToolBatchResult> ExecuteAsync(
        ToolBatch batch,
        CancellationToken cancellationToken);
}

public interface IToolExecutor
{
    Task<ToolBatchResult> ExecuteAsync(
        ToolCatalogSnapshot snapshot,
        ImmutableArray<ToolCallRequest> calls,
        HookDispatchContext hooks,
        CancellationToken cancellationToken);
}

public interface IToolCallRecorder
{
    ValueTask<ToolCallRecordResult> RecordAcceptedAsync(
        AcceptedToolCall call,
        CancellationToken cancellationToken);

    ValueTask<ToolCallRecordResult> RecordTerminalAsync(
        ToolCallResult result,
        CancellationToken cancellationToken);
}

public interface IToolEventSink
{
    ValueTask PublishAsync(
        ToolEvent toolEvent,
        CancellationToken cancellationToken);
}
```

`IToolInvoker` performs one already validated and authorized attempt. It never
discovers tools or decides policy. `IToolCallRecorder` owns accepted/terminal
state and optimistic or idempotent recording; it is not an event sink.
`IToolEventSink` observes immutable activity and cannot influence the result.
The loop depends only on `IToolExecutor`.

`AgentKit.Tools` supplies sealed first-party catalog, resolver, validator,
scheduler, normalizer, and executor classes. The executor's dependency shape is
explicit:

```csharp
namespace AgentKit.Tools;

internal sealed class ToolExecutor(
    IToolResolver resolver,
    IToolArgumentValidator argumentValidator,
    IToolExecutionPolicySelector policySelector,
    ISecurityAuthoritySelector securityAuthorities,
    IToolCallRecorder recorder,
    IToolScheduler scheduler,
    IToolResultNormalizer resultNormalizer,
    IHookDispatcher hooks,
    IEnumerable<IToolEventSink> eventSinks,
    TimeProvider timeProvider) : IToolExecutor
{
}
```

The body is intentionally omitted from this constructor/dependency shape; its
observable API is exactly `IToolExecutor`. It exposes neither the container nor
the independently replaceable pipeline stages.

The scheduler receives already prepared invoker handles from the resolver; it
does not resolve an `IServiceProvider`. Feature tools implement `IToolInvoker`
directly. AgentKit defines no mandatory tool base class. A leaf package may add
a base class only for demonstrated reusable mechanics such as remote stream
assembly or typed argument binding while retaining direct interface support.

## Configuration and dependency injection

All behavior that changes availability or execution is configurable through
typed options, named toolsets and policies, agent definitions, or replaceable
services. Agent definitions select one or more `ToolsetReference` values;
per-run overrides may only tighten that captured selection. Precedence is
explicit call override, agent definition, named toolset/policy, then library
defaults.

```csharp
namespace AgentKit.Tools;

public sealed class ToolRuntimeOptions
{
    public int MaximumArgumentBytes { get; set; } = 1_048_576;
    public int MaximumResultBytes { get; set; } = 4_194_304;
    public int MaximumParallelInvocations { get; set; } = 4;
    public TimeSpan InvocationTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public ToolBatchFailureMode BatchFailureMode { get; set; } =
        ToolBatchFailureMode.SettleIndependently;
    public UnknownSchedulingMode UnknownSchedulingMode { get; set; } =
        UnknownSchedulingMode.Sequential;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentTools(
            ComponentKey<IToolExecutor> executorKey,
            Action<ToolRuntimeOptions>? configure = null) =>
            ToolServiceRegistration.AddAgentTools(
                services,
                executorKey,
                configure);

        public IServiceCollection AddToolset(
            ToolsetKey key,
            Action<ToolsetOptions> configure) =>
            ToolServiceRegistration.AddToolset(services, key, configure);

        public IServiceCollection AddToolProvider<TProvider>()
            where TProvider : class, IToolProvider =>
            ToolServiceRegistration.AddToolProvider<TProvider>(services);

        public IServiceCollection AddTool<TInvoker>(
            ToolDescriptor descriptor,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TInvoker : class, IToolInvoker =>
            ToolServiceRegistration.AddTool<TInvoker>(
                services,
                descriptor,
                lifetime);

        public IServiceCollection ReplaceToolExecutor<TExecutor>(
            ComponentKey<IToolExecutor> key)
            where TExecutor : class, IToolExecutor =>
            ToolServiceRegistration.ReplaceToolExecutor<TExecutor>(services, key);
    }
}
```

The package-internal `ToolServiceRegistration` helper carries the generic
constraints and performs registration without building or resolving a provider.

`AddAgentTools` is idempotent and uses `TryAdd` for the engine-wide registration
catalog, resolver, argument validator, policy selector, scheduler, and
normalizer. It uses `TryAddKeyed` for each executor selected by
`ComponentKey<IToolExecutor>`. `ReplaceToolExecutor` and corresponding
replacement methods for the other singular-per-key axes are explicit. Tool
providers, event sinks, policy contributors, and tool registrations are
additive. A duplicate `ToolId`/`ToolVersion`, provider source identity, or
provider-visible alias is a build error unless an explicit replacement API names
the exact registration being replaced.

Named toolsets and execution-policy strategies are keyed registrations. Agent
definitions refer to typed toolset and policy keys; runtime components receive
catalogs and selectors, not keyed-container access. `AddReadTool`,
`AddWriteTool`, `AddSkillTool`, and `AddWebTool` are idempotent for the same
feature identity and must not replace host registrations implicitly.

The catalog coordinator and scheduler may be thread-safe singletons, but the
catalog coordinator retains only immutable registration/source metadata.
`CaptureAsync` creates a run-bound `ToolCatalogSnapshot` and neither stores that
snapshot nor captures run services. Mutable budgets, invocation state, progress
reporters, resolved invoker bindings, and scoped tool instances are run- or
operation-scoped. The owning DI scope disposes invokers once. Cancellation stops
admission and awaiting, then follows the bounded settlement rules; it never
claims synchronous or remote effects were undone. Retry delay uses injected
`TimeProvider` and injectable randomness.

## Composition validation and unsupported behavior

Registering AgentKit.Tools makes the optional tool capability subject to build
validation. Validation requires one effective engine-wide registration catalog,
resolver, validator, and scheduler plus one selected executor and execution
policy per tool-enabled definition, a security authority, a call recorder backed
by session state, the shared hook dispatcher, `TimeProvider`, and required ID
generators. It also validates profile references, schema dialects, aliases,
bounds, retry safety, tool lifetimes, ordering constraints, and feature
dependencies such as file, network, or process boundaries.

An agent that does not select a toolset exposes no application tools. An
unknown, ambiguous, removed, invalid, denied, approval-required, or unsupported
call returns the corresponding typed result before invocation. Unknown effects
fail closed. Unsupported parallelism follows the captured policy by serializing
or rejecting before effects; it is never silently guessed. Missing security,
recording, enforcement, or required audit prevents execution. No runtime path
discovers missing support by throwing `NotSupportedException` after an effect
has started.

## Tool sources and identity

Tool providers expose immutable descriptor and invoker snapshots from sources
such as application registrations, reflected functions, remote services,
capability packages, or MCP servers. A catalog combines those snapshots,
preserves source-qualified identity, and rejects ambiguous provider-visible
names before a request is sent.

Descriptions, schemas, annotations, and effect hints are untrusted metadata.
Host policy may tighten them. A model call resolves against the exact catalog
snapshot included in its originating request, so a later catalog change cannot
redirect execution.

## Call pipeline

The [tool-call lifecycle](../concepts/tool-call-lifecycle.md) makes every stage,
authority decision, and terminal result explicit.

Each call passes through resolution, size bounds, parsing, canonical schema
validation, security evaluation, approval or deferral, durable call recording,
invocation, result normalization, and terminal recording. No invocation begins
until the accepted call has been recorded. Invalid, denied, unknown, or
ambiguous calls produce no side effect.

The invoker receives only the validated arguments, approved resource scope,
identity, deadline, cancellation, attempt data, safe dependencies, and a bounded
progress channel. It does not receive the loop, arbitrary history, credentials,
or the dependency container.

## Scheduling

The [scheduling contract](../concepts/tool-scheduling-and-concurrency.md)
separates execution order from deterministic publication order.

Provider source order defines result publication order. Calls may execute
concurrently when their declared and host-verified scheduling policy allows it.
Sequential calls form barriers; concurrency keys prevent conflicting overlap;
global exclusivity is scoped explicitly.

The scheduler preflights the whole batch for identities, validation, hard
limits, and side-effect-free permission checks before starting work. Completion
order may differ from publication order, but every accepted call receives
exactly one terminal result.

## Results and retries

The [tool result contract](../concepts/tool-errors-retries-and-results.md) binds
retryability to idempotency and side-effect certainty.

Results contain typed bounded content, status, safe errors, usage, retryability,
and side-effect certainty. Oversized results are rejected, summarized,
truncated, or stored behind authorized [artifact references](artifacts.md)
according to explicit policy. The tool normalizer coordinates artifact creation
and terminal-result commitment; the artifact component never calls back into the
tool recorder.

Retries require both a retryable failure and safe execution semantics. Mutating
calls need idempotency or proof that the prior attempt did not start. Raw
exceptions and secret-bearing arguments never enter model-visible results.

Provider-native tools remain distinct because their execution, permission,
billing, and result lifecycles differ from application tools.

Installing or registering a tool makes it discoverable; it does not authorize
invocation. The tool runtime translates the call into a `SecurityRequest` and
receives a bounded `SecurityGrant`. The effecting file-system, network, or
process implementation validates its derived grant again immediately before
acting. Changed resources or inputs return through authorization.

## Related concept specifications

- [Tools and toolsets](../concepts/tools-and-toolsets.md)
- [Tool-call lifecycle](../concepts/tool-call-lifecycle.md)
- [Tool scheduling and concurrency](../concepts/tool-scheduling-and-concurrency.md)
- [Tool errors, retries, and results](../concepts/tool-errors-retries-and-results.md)
- [Security and human control](permissions-and-human-control.md)
- [Network access](network.md)
- [Process execution](process-execution.md)
