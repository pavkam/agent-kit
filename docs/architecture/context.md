# Context

**Role:** Build the bounded, trusted, provider-ready view for one model request.

[Working context is derived state](../concepts/context-assembly-and-instructions.md).
It combines the durable conversation with instructions, skills, tools,
retrieval, goals, output requirements, and runtime facts. It is not the session
record, and assembling it does not mutate durable history.

AgentKit.Context contains the first-party context assembler and ordered
contributor pipeline. The context contracts and candidate values live in
AgentKit.Abstractions. AddAgentContext registers the assembler as singular and
replaceable for a component key and contributors as an ordered additive
collection. Different agents in one engine may select different assembler keys,
contributor sets, budgets, and compaction policies.

The [context compaction component](context-compaction.md), implemented by
AgentKit.Context.Compaction, is the focused first-party implementation package
for semantic compaction. It owns cut selection, summary production, structural
validation, measurable-reduction checks, and activation of durable compaction
records through session abstractions. The neutral compactor, strategy, request,
result, and record contracts remain in AgentKit.Abstractions, so applications
can replace the package without replacing context assembly.

## Context contributors

Every contributor produces typed candidates with source, provenance, trust,
priority, scope, cost, freshness, and evaluation frequency. Typical contributors
include:

- framework safety and host policy instructions;
- agent and run instructions;
- project or workspace guidance;
- skills selected for the current task;
- tool descriptions and schemas;
- session history and active compaction summaries;
- durable memory, retrieved documents, and citations;
- current goals, constraints, and runtime state; and
- structured-output requirements.

Skills are reusable context capabilities, not a privileged prompt paste. A skill
may contribute instructions, references, and declared component requirements.
Its content retains provenance and trust. Any tools it makes available still go
through the normal tool and permission components.

AgentKit.Tools.Skill owns the first-party skill contributor because the tool and
its context representation must use one catalog and one source identity. Its
registration adds both pieces atomically and remains idempotent.

## Assembly

The assembler loads a stable authorized session branch, validates and repairs
history, resolves effective instructions, gathers
[authorized retrieval candidates](../concepts/memory-retrieval-and-storage.md),
applies [compaction](../concepts/context-compaction.md), resolves the current
tool snapshot and output contract, allocates budgets, and translates the
selected content against the model capability profile.

Compaction is selected independently from assembly. `AgentKit.Context` consumes
the selected abstraction; `AgentKit.Context.Compaction` supplies the built-in
implementation through `AddAgentContextCompaction`. When compaction is enabled
for an agent, composition requires exactly one compatible compactor key. When it
is disabled, overflow terminates with a typed context-limit result rather than
silently dropping history.

Selection and trimming are deterministic for the same versioned inputs.
Mandatory content reserves space for provider framing, pending user input,
tools, output schema, minimum model output, and estimation error. If mandatory
content cannot fit, context preparation fails before provider I/O.

## Trust and precedence

Instruction sources preserve their identity and precedence until provider
translation. Retrieved text, tool results, imported history, and model-produced
summaries are data. They cannot replace host instructions or grant authority.

Replacement instructions that contradict the historical prefix start a new
context epoch or wait for a compaction boundary. AgentKit never silently
pretends old messages occurred under a new system contract.

## Output

The component returns an immutable request view and a manifest of included,
transformed, and omitted sources. The manifest records effective configuration,
tool catalog, provider capabilities, history version, estimates, and reasons for
loss-aware transformations.

The provider adapter owns wire formatting. It does not get to decide which
history, skills, retrieval results, or instructions survive.

Output definitions are supplied by [AgentKit.Output](structured-output.md)
through `IOutputDefinitionResolver`. Context includes the immutable definition
in the request manifest but does not validate terminal model output.

## Normative minimal contract shape

The following C# shapes are normative and minimal, not an exhaustive frozen API.
Every named type belongs in its own matching file. Shared agent, session, run,
turn, model, and request IDs are the canonical values from
[composition and configuration](composition-and-configuration.md).

```csharp
namespace AgentKit;

public enum ContextTrust
{
    Framework,
    HostPolicy,
    AgentDefinition,
    Workspace,
    User,
    RetrievedData,
    ToolData,
    ModelGenerated
}

public enum ContextEvaluationFrequency
{
    OncePerRun,
    OncePerModelRequest
}

public readonly record struct ContextSourceNamespace(string Value);

public readonly record struct ContextSourceKey(string Value);

public readonly record struct ContextSourceVersion(string Value);

public readonly record struct ContextContributorCatalogVersion(long Value);

public sealed record ContextSourceReference(
    ContextSourceNamespace Namespace,
    ContextSourceKey Key,
    ContextSourceVersion Version);

public sealed record ContextCandidate(
    ContextSourceReference Source,
    ContextCandidateKind Kind,
    ContextTrust Trust,
    int Priority,
    ContextScope Scope,
    ContextCostEstimate Cost,
    ContextFreshness Freshness,
    ContextEvaluationFrequency Frequency,
    bool Mandatory,
    ImmutableArray<ContentPart> Content,
    ExtensionData Extensions);

public sealed record ContextContributionRequest(
    AgentDefinition Agent,
    SessionId SessionId,
    ConversationId? ConversationId,
    TenantId TenantId,
    PrincipalId PrincipalId,
    RunId RunId,
    TurnId TurnId,
    ModelRequestId ModelRequestId,
    ModelDescriptor Model,
    HistoryView History,
    SecurityAuthorizationContext Authorization,
    EffectiveConfigurationSnapshot Configuration);

public sealed record ContextContribution(
    ImmutableArray<ContextCandidate> Candidates,
    ImmutableArray<ContextDiagnostic> Diagnostics);

public interface IContextContributor
{
    ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ContextAssemblyRequest(
    AgentDefinition Agent,
    SessionId SessionId,
    ConversationId? ConversationId,
    TenantId TenantId,
    PrincipalId PrincipalId,
    RunId RunId,
    TurnId TurnId,
    ModelRequestId ModelRequestId,
    ModelDescriptor Model,
    MessageCursor HistoryCursor,
    SecurityAuthorizationContext Authorization,
    HookDispatchContext Hooks,
    EffectiveConfigurationSnapshot Configuration,
    ContextBudget Budget);

public sealed record ContextManifestEntry(
    ContextSourceReference Source,
    ContextManifestDisposition Disposition,
    ContextCostEstimate EstimatedCost,
    string Reason,
    ImmutableArray<MessageId> SourceMessageIds);

public sealed record ContextManifest(
    ModelRequestId ModelRequestId,
    AgentDefinitionRevision AgentDefinitionRevision,
    SessionVersion SessionVersion,
    ConfigurationVersion ConfigurationVersion,
    ContextContributorCatalogVersion ContributorCatalogVersion,
    ModelDescriptor Model,
    ImmutableArray<ContextManifestEntry> Entries,
    ContextCostEstimate TotalEstimate);

public sealed record ModelRequestContext(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    TenantId TenantId,
    PrincipalId PrincipalId,
    RunId RunId,
    TurnId TurnId,
    ModelRequestId ModelRequestId,
    ModelDescriptor Model,
    ImmutableArray<InstructionSource> Instructions,
    ImmutableArray<AgentMessage> Messages,
    ToolCatalogSnapshot? Tools,
    OutputDefinition Output,
    SecurityAuthorizationContext Authorization,
    ModelRequestSettings Settings,
    ContextManifest Manifest);

public abstract record ContextAssemblyResult;

public sealed record ContextReady(ModelRequestContext Context)
    : ContextAssemblyResult;

public sealed record ContextPreparationFailed(ContextPreparationFailure Failure)
    : ContextAssemblyResult;

public interface IContextAssembler
{
    Task<ContextAssemblyResult> AssembleAsync(
        ContextAssemblyRequest request,
        CancellationToken cancellationToken = default);
}

public interface IContextBudgetAllocator
{
    ValueTask<ContextBudgetPlan> AllocateAsync(
        ContextBudgetRequest request,
        CancellationToken cancellationToken = default);
}
```

`IContextAssembler` returns `Task` because assembly normally includes durable
history reads, authorization, and retrieval. Contributors and budget allocators
use `ValueTask` because static instructions, cached snapshots, and deterministic
allocation often complete synchronously. All outputs are immutable snapshots;
the request captures one definition revision, configuration version, model
descriptor, history cursor, and tool catalog.

The assembler does not generate IDs itself. The loop allocates one
`ModelRequestId` through `IIdentifierGenerator<ModelRequestId>` before context
preparation, and the same identity flows through the manifest, provider attempt,
stream events, committed assistant metadata, diagnostics, and usage.

## First-party class and dependency direction

```csharp
namespace AgentKit.Context;

internal sealed record ContextAssemblerServices(
    IHistoryPipeline History,
    IInstructionResolver Instructions,
    IEnumerable<IContextContributor> Contributors,
    ICompactor Compactor,
    IContextBudgetAllocator Budgets,
    IToolSnapshotProvider Tools,
    IOutputDefinitionResolver Outputs,
    IHookDispatcher Hooks);

internal sealed class DefaultContextAssembler(
    ContextAssemblerServices services,
    TimeProvider timeProvider,
    ILogger<DefaultContextAssembler> logger) : IContextAssembler
{
}
```

The body is intentionally omitted from this constructor/dependency shape; its
observable members are exactly the `IContextAssembler` contract above. It does
not expose its contributors or an ambient service provider.

The default class depends only on `AgentKit.Abstractions`. The package-internal
keyed registration factory compiles the selected assembler key and compactor key
into one `ContextAssemblerServices` bundle inside the run scope. The class
cannot resolve an optional retriever, memory store, tool catalog, or compactor
from `IServiceProvider`, and Microsoft DI is never expected to propagate an
agent key through ordinary constructor injection. Optional features contribute
through explicit contracts; registering one also validates its collaborators.

No assembler or contributor base class is initially required. Direct interface
implementation keeps host policy, static instruction, retrieval, goal, skill,
and tool-schema contributors independent. A future base class is justified only
for proven shared provenance, caching, or bounds validation and must not make
inheritance mandatory.

## Contributor ordering, concurrency, and trust

Each contributor registration has stable identity, declared order constraints,
evaluation frequency, required/optional status, and a failure policy. Mutating
instruction resolution and history processing remain sequential. Independent
candidate producers may run concurrently only when explicitly declared; their
results are restored to deterministic catalog order before selection.

The assembler is run-scoped. Contributors may be singleton only when immutable,
thread-safe, and independent of run services; dynamic contributors are scoped.
Candidate caches include agent, principal/authority, source version, evaluation
frequency, configuration, model profile, and catalog version. Cached retrieved
content never crosses an authorization boundary.

Trust, precedence, and authority are invariants, not configurable away. Options
may choose among documented loss-aware transformations, budget allocations, and
optional-source failure policies, but retrieved/tool/model-generated content can
never become host instruction or security authority. Hooks may tighten or omit
candidates within their typed boundary and are revalidated after each mutation.

## DI, options, and replacement

```csharp
namespace AgentKit.Context;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentContext(
            ComponentKey<IContextAssembler> key,
            Action<AgentContextOptions>? configure = null) =>
            AgentContextRegistration.Add(services, key, configure);

        public IServiceCollection AddContextAssembler<TAssembler>(
            ComponentKey<IContextAssembler> key)
            where TAssembler : class, IContextAssembler =>
            AgentContextRegistration.AddAssembler<TAssembler>(services, key);

        public IServiceCollection ReplaceContextAssembler<TAssembler>(
            ComponentKey<IContextAssembler> key)
            where TAssembler : class, IContextAssembler =>
            AgentContextRegistration.ReplaceAssembler<TAssembler>(services, key);

        public IServiceCollection AddContextContributor<TContributor>(
            ComponentKey<IContextAssembler> assembler,
            ContextContributorRegistration registration)
            where TContributor : class, IContextContributor =>
            AgentContextRegistration.AddContributor<TContributor>(
                services,
                assembler,
                registration);

        public IServiceCollection ReplaceContextBudgetAllocator<TAllocator>(
            ComponentKey<IContextAssembler> assembler)
            where TAllocator : class, IContextBudgetAllocator =>
            AgentContextRegistration.ReplaceBudgetAllocator<TAllocator>(
                services,
                assembler);

    }
}
```

The implementation package registers compaction independently:

```csharp
namespace AgentKit.Context.Compaction;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentContextCompaction(
            ComponentKey<ICompactor> key,
            Action<ContextCompactionOptions>? configure = null) =>
            ContextCompactionRegistration.Add(services, key, configure);

        public IServiceCollection AddCompactionStrategy<TStrategy>(
            ComponentKey<ICompactor> compactor,
            CompactionStrategyRegistration registration)
            where TStrategy : class, ICompactionStrategy =>
            ContextCompactionRegistration.AddStrategy<TStrategy>(
                services,
                compactor,
                registration);
    }
}
```

The default assembler uses `TryAddKeyedScoped` and is singular per key.
Contributors are additive and ordered; duplicate identity with different type,
options, or constraints fails build. Budget allocator, instruction resolver,
history pipeline, compactor selection, tool snapshot provider, and output
resolver are singular for the selected context profile and have explicit
replacement paths. Compaction strategies are additive and deterministically
ordered inside the selected compactor. Repeated equivalent package registration
is idempotent.

Typed options and the immutable agent definition configure source sets,
ordering, evaluation frequency, reserved output, estimation margin, compaction
trigger, trimming policy, safe transformation policy, parallel contribution,
cache policy, and required/optional failure behavior. Defaults are
deterministic: required host/agent instructions, pending input, tool
correlation, output contract, and safety margin reserve capacity before optional
content; unknown content is retained or rejected, never silently flattened.

Composition validates keys, order cycles, scope captures, non-positive budgets,
unsafe cache scopes, missing optional-feature collaborators, and model/output
capability compatibility. Mandatory-content overflow, invalid history,
unauthorized retrieval, contributor failure, non-reducing compaction, and
unsupported translation produce typed `ContextPreparationFailure` outcomes
before provider I/O. Cancellation discards the candidate view and leaves durable
history unchanged.

## Related concept specifications

- [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)
- [Context compaction](../concepts/context-compaction.md)
- [Context compaction architecture](context-compaction.md)
- [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)
