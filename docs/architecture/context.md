# Context

**Role:** Build the bounded, trusted, provider-ready view for one model request.

[Working context is derived state](../concepts/context-assembly-and-instructions.md).
It combines the durable conversation with instructions, skills, tools,
retrieval, goals, output requirements, and runtime facts. It is not the session
record. Ordinary assembly does not append history; when enabled, compaction runs
as a separately identified and authorized sub-operation and may append an
activation through its session capability.

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
    ModelGenerated,
    Package
}

public enum ContextCandidateKind
{
    Instruction,
    ReferenceData,
    RuntimeData
}

public enum ContextScope
{
    Engine,
    Agent,
    Session,
    Conversation,
    Run,
    Turn,
    ModelRequest
}

public sealed record ContextCostEstimate(long Utf8Bytes, int? EstimatedTokens);

public sealed record ContextFreshness(DateTimeOffset? ExpiresAt)
{
    public static ContextFreshness Pinned { get; }
}

public enum ContextDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public sealed record ContextDiagnostic(
    ContextDiagnosticSeverity Severity,
    string Code,
    string SafeMessage,
    ContextSourceReference? Source = null);

public enum ContextEvaluationFrequency
{
    OncePerRun,
    OncePerModelRequest
}

public enum ContextOverflowBehavior
{
    Fail,
    CompactWhenConfigured
}

public enum ContextManifestDisposition
{
    Included,
    Transformed,
    Omitted
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
    ExecutionIdentity Identity,
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

public sealed record ContextAssemblyEvidence(
    AgentDefinition Agent,
    ExecutionIdentity Identity,
    HistoryView History,
    SecurityAuthorizationContext Authorization,
    EffectiveConfigurationSnapshot Configuration);

public sealed record ContextAssemblyRequest(
    AgentDefinition Agent,
    SessionId SessionId,
    ConversationId? ConversationId,
    ExecutionIdentity Identity,
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
    ExecutionIdentity Identity,
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

public sealed record ContextBudget(
    long MaxContextTokens,
    int ReservedOutputTokens,
    int ProviderOverheadTokens,
    double EstimationSafetyMargin);

public sealed record ContextBudgetRequest(
    ContextBudget Budget,
    ImmutableArray<ContextCandidate> Candidates,
    ContextOverflowBehavior OverflowBehavior);

public sealed record ContextBudgetPlan(
    ImmutableArray<ContextCandidate> Selected,
    ImmutableArray<ContextCandidate> Omitted,
    ContextCostEstimate EstimatedTotal,
    bool MandatoryOverflow);

public interface IContextBudgetAllocator
{
    ValueTask<ContextBudgetPlan> AllocateAsync(
        ContextBudgetRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ContextCompactionCapability(
    ComponentKey<ICompactor> CompactorKey,
    ICompactor Compactor,
    SessionExecutionCapability Session,
    BudgetExecutionCapability Budget);
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
    ContextCompactionCapability? Compaction,
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

`ContextCandidateKind` controls projection and instruction authority, not
origin. `ContextScope` names the narrowest applicability boundary and carries no
identity or authority. Byte and known-token estimates are nonnegative. A null
freshness expiry pins validity to the captured publication or lifecycle, never
to the process forever. Diagnostic codes and safe messages are nonblank and
content-safe; severity does not override registration failure policy.

`ContextAssemblyEvidence` is the compatibility bridge for the current reduced
implementation. Its agent, history cursor, identity, authorization scope,
definition revision, and configuration version agree atomically. Conversation
identity remains exact in the history cursor because the current authorization
scope exposes no conversation coordinate to cross-check. The reduced
`ContextAssemblyRequest` keeps its original constructor and adds an evidence
constructor whose history is exactly `Evidence.History.Messages`; this does not
replace the full normative request above.

The reduced loop obtains `ConversationId` from the authoritative session
descriptor and pages one prefix pinned by `SessionReadSnapshot`. It constructs
`MessageCursor` from that snapshot's independent version and upper sequence,
then passes the resulting `HistoryView` together with the exact admitted
`AgentDefinition`, authenticated identity, fresh turn authorization, and exact
effective configuration through `ContextAssemblyEvidence`. Legacy reduced run
requests without exact definition/configuration evidence remain supported by the
compatibility path and do not fabricate evidence.

The body is intentionally omitted from this constructor/dependency shape; its
observable members are exactly the `IContextAssembler` contract above. It does
not expose its contributors or an ambient service provider.

The default class consumes neutral contracts and shared diagnostic
infrastructure. The package-internal keyed registration factory compiles the
selected assembler key into one `ContextAssemblerServices` bundle inside the run
scope. When the immutable agent definition selects compaction, that bundle
contains exactly one `ContextCompactionCapability` with the keyed compactor and
the already selected session and budget execution capabilities. When compaction
is not selected, the value is `null`; mandatory overflow fails with the typed
context-limit outcome. The capability is invocation-only, is never serialized or
cached, and prevents the compactor from rediscovering a session coordinator or
capturing a bare run budget.

The class cannot resolve an optional retriever, memory store, tool catalog, or
compactor from `IServiceProvider`, and Microsoft DI is never expected to
propagate an agent key through ordinary constructor injection. Optional features
contribute through explicit contracts; registering one also validates its
collaborators.

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
Candidate caches include agent, the complete `ExecutionIdentity` fingerprint,
authority and policy versions, source version, evaluation frequency,
configuration, model profile, and catalog version. Cached retrieved content
never crosses an authorization boundary.

Trust, precedence, and authority are invariants, not configurable away. Options
may choose among documented loss-aware transformations, budget allocations, and
optional-source failure policies, but retrieved/tool/model-generated content can
never become host instruction or security authority. Hooks may tighten or omit
candidates within their typed boundary and are revalidated after each mutation.

## Workspace project instruction discovery

`AgentKit.Context.Project` discovers bounded workspace instruction files through
the protected `IFileSystem` boundary. Discovery is deterministic and loss-aware:

- Search roots are relative paths configured on
  `ProjectInstructionOptions.SearchRoots`; the default scans the workspace root
  (`"."`).
- Filenames are matched exactly against
  `ProjectInstructionOptions.InstructionFilenames`; the default set includes
  `AGENTS.md` and `CLAUDE.md`.
- Each `(root, filename)` pair is attempted in configuration order. Missing
  files are skipped without failing optional discovery.
- Reads require a fresh `SecurityGrant` issued for the exact path; the
  contributor never bypasses authorization or reads outside the file-system
  boundary.
- Each accepted file contributes one instruction-trusted candidate tagged with
  `ContextTrust.Workspace`, capped by
  `ProjectInstructionOptions.MaxBytesPerFile`.

## DI, options, and replacement

```csharp
namespace AgentKit.Context;

public sealed class AgentContextOptions
{
    public ContextOverflowBehavior OverflowBehavior { get; set; } =
        ContextOverflowBehavior.Fail;
    public int ReservedOutputTokens { get; set; } = 1_024;
    public int ProviderOverheadTokens { get; set; } = 256;
    public double EstimationSafetyMargin { get; set; } = 0.10;
    public double EstimatedCharactersPerToken { get; set; } = 4.0;
    public bool AllowParallelContributors { get; set; }
}

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
history pipeline, optional compaction capability, tool snapshot provider, and
output resolver are singular for the selected context profile and have explicit
replacement paths. Compaction strategies are additive and deterministically
ordered inside the selected compactor. Repeated equivalent package registration
is idempotent.

Typed options and the immutable agent definition configure source sets,
ordering, evaluation frequency, reserved output, estimation margin, compaction
trigger, trimming policy, safe transformation policy, parallel contribution,
cache policy, and required/optional failure behavior. Defaults are
deterministic: required host/agent instructions, pending input, tool
correlation, output contract, and safety margin reserve capacity before optional
content; compaction is opt-in; unknown content is retained or rejected, never
silently flattened. Mutable options are validated and copied into an immutable
profile snapshot when the run plan is compiled, so reloads affect a later run or
explicit next-turn boundary rather than an in-flight assembly.

Composition validates keys, order cycles, scope captures, non-positive budgets,
unsafe cache scopes, missing optional-feature collaborators, and model/output
capability compatibility. Mandatory-content overflow, invalid history,
unauthorized retrieval, contributor failure, non-reducing compaction, and
unsupported translation produce typed `ContextPreparationFailure` outcomes
before the main provider request. Cancellation discards an unpublished candidate
view. It cannot roll back an already activated compaction or erase separately
recorded summary-generation usage. After successful activation, assembly loads
the returned committed cursor and rebuilds its manifest before returning a
provider-ready request; it cannot combine a pre-activation cursor with a newly
activated summary.

## Related concept specifications

- [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)
- [Context compaction](../concepts/context-compaction.md)
- [Context compaction architecture](context-compaction.md)
- [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)
