# Model and embedding providers

**Role:** Select a compatible operation and translate provider-neutral requests
into external model services.

Provider behavior has two layers. AgentKit.Providers owns provider-neutral
runtime mechanics. Concrete AgentKit.Providers.ProviderName packages own
endpoints, credentials, wire translation, stream parsing, errors, and capability
descriptors. Neither layer owns the agent loop, history selection, tool
execution, permissions, compaction, or durable storage.

## Package split

AgentKit.Providers contains the first-party model catalog, selector, capability
validator, and model request executor. AddAgentProviders registers those
replaceable services. The package never references a vendor SDK or assumes an
OpenAI wire shape.

The catalog is shared by the process-level engine; model selectors and request
executors are keyed strategies selected by each `AgentDefinition`. One engine
may therefore host agents with different model policies and providers without
creating an engine per provider or hiding provider choice in a global mutable
setting.

The initial integration family is:

| Package                             | Responsibility                                                                                          | Operations                                                               |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| AgentKit.Providers.OpenAICompatible | Reusable Responses, Chat Completions, embeddings, transport, parsing, and tested compatibility profiles | Building blocks for concrete compatible packages and custom endpoints    |
| AgentKit.Providers.OpenAI           | OpenAI identity, endpoints, credentials, options, descriptors, and registration                         | Conversation and embeddings                                              |
| AgentKit.Providers.OpenRouter       | OpenRouter routing, upstream provenance, metadata, credentials, options, and registration               | Conversation, embeddings, and reranking                                  |
| AgentKit.Providers.ZAi              | Z.ai identity, endpoints, credentials, Chat Completions profile, and provider-native operations         | Conversation and only other operations supported by its verified profile |

OpenAICompatible is deliberately not the package applications normally select as
their provider. OpenAI, OpenRouter, and Z.ai have different authentication,
extensions, model catalogs, capability claims, errors, and usage even when some
requests look alike.

Each concrete package exposes an ASP.NET-style registration entry point:
AddOpenAI, AddOpenRouter, or AddZAi. Registrations may add several named models,
but conversation, embedding, and reranking capabilities remain independent.
Adding OpenAI can register both conversational and embedding models. Adding
OpenRouter can register conversational, embedding, and reranking models. Z.ai
does not advertise an embedding implementation unless its current verified API
actually provides one.

## Identity and capabilities

The [model capability contract](../concepts/model-providers-and-capabilities.md)
makes this identity tuple observable before any request is sent.

Provider, API family, endpoint or deployment, and model are distinct identities.
Capabilities belong to that configured combination, not to a brand name. A
descriptor states supported roles, modalities, tools, structured output,
streaming, reasoning, continuation, usage reporting, schema dialects, and
context limits.

Before I/O, the provider runtime compares the request with the effective
descriptor. Unsupported behavior is rejected, explicitly downgraded, or routed
to another compatible model. Silent downgrade is forbidden.

Registration produces immutable, named descriptors and operation factories.
Names are application aliases; results, telemetry, durable state, and vector
metadata preserve the actual provider, upstream provider where routed, API
family, model, and deployment identity.

## Provider pipeline

Each concrete adapter performs one
[provider request attempt](../concepts/provider-request-pipeline.md); selection,
retry, and fallback remain above that leaf boundary.

One provider attempt validates capabilities, translates the immutable request,
applies bounded provider options, obtains credentials, runs typed trusted hooks,
authorizes the destination and classified data egress, sends through the
AgentKit network abstraction, parses the response as a state machine, and
normalizes the terminal response or error.

The adapter preserves role and content order, call identities, media, usage,
finish reasons, continuation data, safety information, upstream routing, and
safe unknown fields when supported. Credentials stay inside the concrete
integration and are injected only at send time. Transport limits cover
connection, headers, idle streams, total duration, frames, bytes, redirects, and
decompression.

## Shared wire families

AgentKit.Providers.OpenAICompatible exposes reusable base classes and services
only where they remove proven duplication: request translation, JSON contracts,
HTTP behavior, SSE parsing, stream assembly, common errors, and compatibility
profile evaluation. Direct interface implementation remains supported.

Every concrete compatible package supplies an explicit profile for roles,
fields, tool shapes, schema restrictions, streaming events, usage placement,
finish reasons, authentication, and provider extensions. The shared package
cannot broaden that profile. A provider test suite must run both shared-family
conformance and concrete-package tests.

Native providers use independent adapters when shared-wire assumptions would
lose meaning or capability. Cloud brokers keep deployment, identity, region, and
platform policy in their concrete package even when they reuse a payload
translator. Vendor SDK types never enter AgentKit.Abstractions.

## Selection, retry, and fallback

The catalog exposes configured descriptors and the selector chooses one from the
run's requirements and policy. The reason and catalog version are observable.
The model request executor invokes the selected concrete adapter and owns
provider retry and fallback decisions within the loop's reserved budgets.

Retries and fallback occur above the single-attempt adapter, consume shared run
budgets, and revalidate capabilities and history affinity. Provider-bound
reasoning, continuation state, native tool state, or routed-provider identity
cannot be discarded merely to make fallback succeed. There is no generic
resilience package allowed to replay arbitrary provider work behind the loop's
back.

## Embeddings and reranking

Their canonical request, identity, and result semantics are defined as
[independent semantic operations](../providers/semantic-operations.md).

Embedding generation is a separate provider contract with its own provider,
model, revision, dimensions, modality, normalization, limits, and usage. It does
not appear as an optional method on conversational models. The memory component
enforces compatibility between stored vectors and query embeddings.

Reranking is another independent semantic operation. Its request, score
semantics, model identity, limits, and usage differ from embeddings. A package
may implement conversation, embeddings, and reranking together, but applications
select each named operation independently and may use different vendors for
each.

Provider-native tools, token counting, media generation, files, caches, and
hosted retrieval also remain explicit capabilities. AgentKit does not pretend
that installing a vendor package turns every endpoint in that vendor's control
plane into part of the conversational model contract.

## Normative minimal identity and catalog shape

The following C# shapes are normative and minimal rather than exhaustive. Every
named type lives in its own matching file. `ProviderId`, `ModelId`,
`ModelRequestId`, `AgentId`, `RunId`, and `TurnId` are the canonical typed
values from [composition and configuration](composition-and-configuration.md).

```csharp
namespace AgentKit;

public readonly record struct ModelAlias(string Value);
public readonly record struct EmbeddingModelAlias(string Value);
public readonly record struct RerankerAlias(string Value);
public readonly record struct ApiFamilyId(string Value);
public readonly record struct DeploymentId(string Value);
public readonly record struct ModelDescriptorSourceId(string Value);
public readonly record struct EmbeddingInputId(Guid Value);
public readonly record struct ModelDescriptorRevision(long Value);
public readonly record struct ModelCatalogVersion(long Value);
public readonly record struct ModelDescriptorSourceVersion(long Value);
public readonly record struct ProviderModelRevision(string Value);

public sealed record ModelDescriptor(
    ModelAlias Alias,
    ProviderId ProviderId,
    ApiFamilyId ApiFamily,
    ModelId ModelId,
    DeploymentId? DeploymentId,
    ModelDescriptorRevision DescriptorRevision,
    ModelCapabilities Capabilities,
    ModelLimits Limits,
    ModelPricing? Pricing,
    CompatibilityProfile Compatibility,
    ExtensionData Extensions);

public sealed record EmbeddingModelDescriptor(
    EmbeddingModelAlias Alias,
    ProviderId ProviderId,
    ApiFamilyId ApiFamily,
    ModelId ModelId,
    DeploymentId? DeploymentId,
    ProviderModelRevision? ModelRevision,
    EmbeddingCapabilities Capabilities,
    EmbeddingLimits Limits,
    ExtensionData Extensions);

public sealed record RerankerDescriptor(
    RerankerAlias Alias,
    ProviderId ProviderId,
    ApiFamilyId ApiFamily,
    ModelId ModelId,
    DeploymentId? DeploymentId,
    ProviderModelRevision? ModelRevision,
    RerankerCapabilities Capabilities,
    RerankerLimits Limits,
    ExtensionData Extensions);

public sealed record ModelCatalogSnapshot(
    ModelCatalogVersion Version,
    ImmutableArray<ModelDescriptor> ConversationModels,
    ImmutableArray<EmbeddingModelDescriptor> EmbeddingModels,
    ImmutableArray<RerankerDescriptor> Rerankers);

public sealed record ModelDescriptorSourceSnapshot(
    ModelDescriptorSourceId SourceId,
    ModelDescriptorSourceVersion Version,
    ImmutableArray<ModelDescriptor> ConversationModels,
    ImmutableArray<EmbeddingModelDescriptor> EmbeddingModels,
    ImmutableArray<RerankerDescriptor> Rerankers);

public sealed record ProviderResponseIdentity(
    ProviderId ProviderId,
    ProviderId? UpstreamProviderId,
    ApiFamilyId ApiFamily,
    ModelId RequestedModelId,
    ModelId ResolvedModelId,
    ProviderModelRevision? ModelRevision,
    DeploymentId? DeploymentId,
    ProviderRequestId? RequestId,
    ProviderResponseId? ResponseId);

public sealed record ProtectedSemanticOperationContext(
    AgentId AgentId,
    SessionId? SessionId,
    ConversationId? ConversationId,
    ExecutionIdentity Identity,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization);

public enum ProviderFailureKind
{
    Authentication,
    Authorization,
    Throttling,
    InvalidRequest,
    Unavailable,
    Timeout,
    Cancellation,
    ProtocolViolation,
    Unknown
}

public sealed record ProviderFailure(
    ProviderFailureKind Kind,
    ProviderId ProviderId,
    ProviderRequestId? RequestId,
    int? StatusCode,
    string? ProviderCode,
    TimeSpan? RetryAfter,
    string SafeMessage,
    Exception? DiagnosticCause,
    ExtensionData Extensions);

public interface IModelDescriptorSource
{
    ValueTask<ModelDescriptorSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface IModelCatalog
{
    ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
```

Aliases are application selection keys; provider, API family, deployment, and
model are actual identities. The first-party catalog composes additive
descriptor sources into one immutable, versioned, thread-safe snapshot. An
invalid discovery refresh leaves the last good snapshot active. The catalog does
not retain provider SDK clients or return a union descriptor that claims
capabilities no single configured operation supports.

Descriptor sources are additive. Aliases are unique within their operation kind:
the same text may identify a chat and embedding operation without merging their
contracts. Duplicate alias with different canonical descriptor content fails
validation unless an explicit operation-specific replacement is used.

`ProtectedSemanticOperationContext` is the immutable identity, correlation, and
authority binding shared by conversational, embedding, reranking, and other
external semantic operations. The operation validates that
`Identity == Authorization.Identity` and that any run or turn in the request
matches the typed correlation. A changed identity, destination, classified
payload, model revision, or attempt fingerprint requires a fresh security
request; this context is policy input, not itself a grant.

## Selection and capability contracts

```csharp
namespace AgentKit;

public sealed record ModelSelectionPolicy(
    ImmutableArray<ModelAlias> Candidates,
    ModelFallbackPolicy Fallback,
    CapabilityDowngradePolicy Downgrade,
    ExtensionData Extensions);

public sealed record ModelSelectionRequest(
    ProtectedSemanticOperationContext Operation,
    TurnId TurnId,
    ModelRequestId ModelRequestId,
    ModelSelectionPolicy Policy,
    ModelRequirements Requirements,
    HistoryAffinity HistoryAffinity,
    ModelCatalogSnapshot Catalog);

public sealed record ModelSelectionDecision(
    ModelDescriptor Model,
    string Reason,
    ModelCatalogVersion CatalogVersion,
    ImmutableArray<ModelSelectionDiagnostic> Diagnostics);

public abstract record ModelSelectionResult;

public sealed record ModelSelected(ModelSelectionDecision Decision)
    : ModelSelectionResult;

public sealed record NoCompatibleModel(
    ModelRequirements Requirements,
    ImmutableArray<ModelSelectionDiagnostic> Diagnostics)
    : ModelSelectionResult;

public sealed record InvalidModelPolicy(string Reason) : ModelSelectionResult;

public abstract record CapabilityValidationResult;

public sealed record CapabilitiesSupported(ModelRequestContext Context)
    : CapabilityValidationResult;

public sealed record CapabilitiesDowngraded(
    ModelRequestContext Context,
    ImmutableArray<CapabilityAdjustment> Adjustments)
    : CapabilityValidationResult;

public sealed record CapabilitiesUnsupported(
    ImmutableArray<UnsupportedCapability> Capabilities)
    : CapabilityValidationResult;

public interface IModelSelector
{
    ValueTask<ModelSelectionResult> SelectAsync(
        ModelSelectionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IModelCapabilityValidator
{
    ValueTask<CapabilityValidationResult> ValidateAsync(
        ModelRequestContext context,
        CapabilityDowngradePolicy downgradePolicy,
        CancellationToken cancellationToken = default);
}
```

Selection and validation use `ValueTask` because a stable catalog and
deterministic policy normally complete synchronously. A selector that consults
load or health may await injected services, but any non-determinism and
injectable randomness are declared in the selection decision. Selection never
grants authority to egress or causes provider I/O.

Downgrade is a discriminated outcome with exact adjustments;
`CapabilitiesSupported` means no semantic loss. Missing candidates, incompatible
provider-bound history, and unsupported required capabilities produce
`NoCompatibleModel` or `CapabilitiesUnsupported` before credentials, hooks, or
network access.

## Conversational execution contracts

A chat adapter performs exactly one logical attempt. The configured loop or
request pipeline owns selection and cross-model fallback; the provider request
executor owns same-model retry, attempt budgets, and safe publication of
streamed events.

### Usage and stop-reason accounting

[Assistant response metadata](messages-and-history.md#normative-minimal-message-shape)
and every response event below share one usage and stop-reason shape so a
committed message, a mid-stream update, and a failed/cancelled attempt report
usage identically.

```csharp
namespace AgentKit;

public enum NormalizedStopReason
{
    Pending,
    Completed,
    Length,
    ToolUse,
    Error,
    Cancelled,
    Deferred
}

public sealed record ModelUsage(
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    long? ReasoningTokens,
    decimal? EstimatedCost,
    string? CostCurrency,
    ExtensionData Extensions)
{
    public static ModelUsage Empty { get; } = new(
        null, null, null, null, null, null, ExtensionData.Empty);
}
```

`ModelUsage.Empty` is a safe zero-usage default for attempts that fail before a
provider reports any counters; it is never substituted for a real reported value
once one is available. Provider-specific counters that do not fit these portable
fields remain in `Extensions` rather than being dropped or force-fit.

```csharp
namespace AgentKit;

public sealed record ChatModelRequest(
    ProtectedSemanticOperationContext Operation,
    ModelRequestContext Context,
    int Attempt,
    DateTimeOffset Deadline,
    ProviderRequestOptions Options);

public abstract record ModelResponseEvent(
    ModelRequestId RequestId,
    long Sequence);

public sealed record ModelResponseStarted(
    ModelRequestId RequestId,
    long Sequence) : ModelResponseEvent(RequestId, Sequence);

public sealed record ModelPartStarted(
    ModelRequestId RequestId,
    long Sequence,
    int PartIndex) : ModelResponseEvent(RequestId, Sequence);

public abstract record ContentDelta;

public sealed record TextContentDelta(string Text) : ContentDelta;

public sealed record ReasoningContentDelta(
    string Text,
    ExtensionData Extensions) : ContentDelta;

public sealed record ToolArgumentsContentDelta(
    ToolCallId ToolCallId,
    string JsonFragment) : ContentDelta;

public sealed record StructuredDataContentDelta(string JsonFragment)
    : ContentDelta;

public sealed record ProviderContentDelta(
    ProviderId ProviderId,
    ExtensionData Extensions) : ContentDelta;

public sealed record ModelPartDelta(
    ModelRequestId RequestId,
    long Sequence,
    int PartIndex,
    ContentDelta Delta) : ModelResponseEvent(RequestId, Sequence);

public sealed record ModelPartCompleted(
    ModelRequestId RequestId,
    long Sequence,
    int PartIndex,
    ContentPart Part) : ModelResponseEvent(RequestId, Sequence);

public sealed record ModelUsageUpdated(
    ModelRequestId RequestId,
    long Sequence,
    ModelUsage Usage) : ModelResponseEvent(RequestId, Sequence);

public sealed record ModelResponse(
    ModelRequestId RequestId,
    ProviderResponseIdentity Identity,
    ImmutableArray<ContentPart> Parts,
    NormalizedStopReason StopReason,
    ModelUsage Usage,
    ExtensionData Extensions);

public sealed record ModelResponseCompleted(
    ModelRequestId RequestId,
    long Sequence,
    ModelResponse Response) : ModelResponseEvent(RequestId, Sequence);

public sealed record ModelResponseFailed(
    ModelRequestId RequestId,
    long Sequence,
    ProviderFailure Failure,
    ImmutableArray<ContentPart> PartialParts,
    ModelUsage? Usage) : ModelResponseEvent(RequestId, Sequence);

public sealed record ModelResponseCancelled(
    ModelRequestId RequestId,
    long Sequence,
    ProviderFailure Cancellation,
    ImmutableArray<ContentPart> PartialParts,
    ModelUsage? Usage) : ModelResponseEvent(RequestId, Sequence);

public interface IModelResponseObserver
{
    ValueTask OnEventAsync(
        ModelResponseEvent responseEvent,
        CancellationToken cancellationToken = default);
}

public abstract record ModelAttemptResult;

public sealed record ModelAttemptCompleted(ModelResponse Response)
    : ModelAttemptResult;

public sealed record ModelAttemptFailed(
    ProviderFailure Failure,
    ImmutableArray<ContentPart> PartialParts,
    ModelUsage? Usage) : ModelAttemptResult;

public sealed record ModelAttemptCancelled(
    ProviderFailure Cancellation,
    ImmutableArray<ContentPart> PartialParts,
    ModelUsage? Usage) : ModelAttemptResult;

public interface IChatModel
{
    ModelAlias Alias { get; }

    Task<ModelAttemptResult> ExecuteAsync(
        ChatModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}

public sealed record ModelExecutionRequest(
    ProtectedSemanticOperationContext Operation,
    ModelSelectionDecision Selection,
    ModelRequestContext Context,
    HookDispatchContext Hooks,
    ProviderRetryPolicy RetryPolicy);

public abstract record ModelExecutionResult;

public sealed record ModelExecutionCompleted(
    ModelSelectionDecision Selection,
    int Attempts,
    ModelAttemptCompleted Result) : ModelExecutionResult;

public sealed record ModelFallbackRequired(
    ModelSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure) : ModelExecutionResult;

public sealed record ModelExecutionFailed(
    ModelSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure) : ModelExecutionResult;

public sealed record ModelExecutionCancelled(
    ModelSelectionDecision Selection,
    int Attempts,
    ProviderFailure Cancellation) : ModelExecutionResult;

public interface IModelRequestExecutor
{
    Task<ModelExecutionResult> ExecuteAsync(
        ModelExecutionRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}
```

The adapter and executor return `Task` because network execution and ordered
event delivery are inherently asynchronous. The observer uses `ValueTask` to
support synchronous bounded fan-out without allocating per event. Backpressure
is explicit: the adapter awaits each observer call and cannot continue emitting
after a terminal event. Observer cancellation and run cancellation retain their
distinct ownership semantics.

Every attempt emits one `ModelResponseStarted`, then a sequence of
`ModelPartStarted`, `ModelPartDelta`, `ModelPartCompleted`, and
`ModelUsageUpdated` events, followed by exactly one of `ModelResponseCompleted`,
`ModelResponseFailed`, or `ModelResponseCancelled`. Sequences are contiguous and
strictly increasing. A part index starts once; deltas and completion are valid
only while that part is open. Different open parts may interleave, which
preserves providers that stream parallel tool-call arguments. Successful
completion requires every part to be closed. Failure or cancellation may
terminally expose bounded partial parts without committing them. No event
follows a terminal event.

`ContentDelta` is a typed portable family for text, reasoning, tool-argument,
structured-data, and typed provider-extension fragments. A provider adapter may
add a new portable subtype through additive contract evolution; it never hides
fragment kind or tool-call correlation in an untyped string callback. A
cancellation terminal and its returned result use a `ProviderFailure` whose kind
is `Cancellation`; all other failures use `ModelResponseFailed` and
`ModelAttemptFailed`.

Only `ModelResponseCompleted` carries the committed `ModelResponse` aggregate.
Its ordered parts must equal the completed part events and its usage must equal
the last usage event, or the adapter's final usage when no update was emitted.
Failure and cancellation terminals preserve every bounded partial part and the
last known usage without presenting them as a successful response. The returned
`ModelAttemptCompleted`, `ModelAttemptFailed`, or `ModelAttemptCancelled` must
contain the same aggregate or terminal state as the emitted event.

The adapter validates its alias and descriptor, translates the immutable
request, obtains credentials at send time, requests provider-egress authority,
and uses the narrow AgentKit network contracts. It never selects another model,
retries a visible stream, executes an application tool, or mutates history. A
terminal result and terminal stream event must agree; disconnect before terminal
is a protocol failure with truthful partial-output diagnostics.

Same-model retries may remain inside one executor call while no output is
visible. When policy permits a different candidate, the executor returns
`ModelFallbackRequired` without selecting or invoking it. The loop calls its
configured selector, asks the context assembler to rebuild against the newly
selected descriptor, and starts a new observable execution request. This keeps
selection pipeline-owned and prevents provider runtime from reusing stale
model-specific context.

## Embedding and reranking contracts

Conversation, embedding, and reranking remain independently registered even when
one package supplies all three:

```csharp
namespace AgentKit;

public sealed record EmbeddingSelectionPolicy(
    ImmutableArray<EmbeddingModelAlias> Candidates,
    SemanticFallbackPolicy Fallback);

public sealed record RerankerSelectionPolicy(
    ImmutableArray<RerankerAlias> Candidates,
    SemanticFallbackPolicy Fallback);

public sealed record EmbeddingSelectionRequest(
    ProtectedSemanticOperationContext Operation,
    EmbeddingSelectionPolicy Policy,
    EmbeddingRequirements Requirements,
    ModelCatalogSnapshot Catalog);

public sealed record EmbeddingSelectionDecision(
    EmbeddingModelDescriptor Model,
    string Reason,
    ModelCatalogVersion CatalogVersion);

public abstract record EmbeddingSelectionResult;

public sealed record EmbeddingModelSelected(EmbeddingSelectionDecision Decision)
    : EmbeddingSelectionResult;

public sealed record NoCompatibleEmbeddingModel(
    EmbeddingRequirements Requirements,
    ImmutableArray<ModelSelectionDiagnostic> Diagnostics)
    : EmbeddingSelectionResult;

public interface IEmbeddingModelSelector
{
    ValueTask<EmbeddingSelectionResult> SelectAsync(
        EmbeddingSelectionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RerankerSelectionRequest(
    ProtectedSemanticOperationContext Operation,
    RerankerSelectionPolicy Policy,
    RerankerRequirements Requirements,
    ModelCatalogSnapshot Catalog);

public sealed record RerankerSelectionDecision(
    RerankerDescriptor Model,
    string Reason,
    ModelCatalogVersion CatalogVersion);

public abstract record RerankerSelectionResult;

public sealed record RerankerSelected(RerankerSelectionDecision Decision)
    : RerankerSelectionResult;

public sealed record NoCompatibleReranker(
    RerankerRequirements Requirements,
    ImmutableArray<ModelSelectionDiagnostic> Diagnostics)
    : RerankerSelectionResult;

public interface IRerankerSelector
{
    ValueTask<RerankerSelectionResult> SelectAsync(
        RerankerSelectionRequest request,
        CancellationToken cancellationToken = default);
}
```

Selectors are independent because their requirements and compatibility differ.
They consume the shared typed catalog snapshot but cannot return a descriptor of
the wrong operation kind. They are deterministic by default and perform no
provider call, vector write, retrieval, or ranking.

```csharp
namespace AgentKit;

public abstract record EmbeddingInput;

public sealed record TextEmbeddingInput(
    EmbeddingInputId Id,
    string Text) : EmbeddingInput;

public sealed record EmbeddingRequest(
    ImmutableArray<EmbeddingInput> Inputs,
    EmbeddingPurpose Purpose,
    int? Dimensions,
    EmbeddingEncoding? Encoding,
    EmbeddingNormalization? Normalization,
    EmbeddingTruncation Truncation,
    ProviderRequestOptions Options);

public sealed record EmbeddingModelRequest(
    ProtectedSemanticOperationContext Operation,
    EmbeddingSelectionDecision Selection,
    EmbeddingRequest Request,
    int Attempt,
    DateTimeOffset Deadline);

public abstract record EmbeddingVector;

public sealed record DenseFloatVector(
    ImmutableArray<float> Values) : EmbeddingVector;

public sealed record PackedBinaryVector(
    ImmutableArray<byte> Values,
    bool Signed) : EmbeddingVector;

public sealed record EmbeddingSpaceIdentity(
    ProviderId ProviderId,
    ApiFamilyId ApiFamily,
    ModelId RequestedModelId,
    ModelId ResolvedModelId,
    ProviderModelRevision? ModelRevision,
    DeploymentId? DeploymentId,
    int Dimensions,
    EmbeddingElementType ElementType,
    EmbeddingPurpose Purpose,
    EmbeddingNormalization Normalization,
    EmbeddingTruncation Truncation,
    ExtensionData Extensions);

public abstract record EmbeddingItemOutcome(
    int InputIndex,
    EmbeddingInputId InputId);

public sealed record EmbeddingItemSucceeded(
    int InputIndex,
    EmbeddingInputId InputId,
    EmbeddingVector Vector,
    EmbeddingSpaceIdentity Space,
    ExtensionData Extensions)
    : EmbeddingItemOutcome(InputIndex, InputId);

public sealed record EmbeddingItemFailed(
    int InputIndex,
    EmbeddingInputId InputId,
    ProviderFailure Failure,
    ExtensionData Extensions)
    : EmbeddingItemOutcome(InputIndex, InputId);

public sealed record EmbeddingResponse(
    ImmutableArray<EmbeddingItemOutcome> Items,
    SemanticOperationUsage Usage,
    ProviderRequestId? ProviderRequestId,
    ExtensionData Extensions);

public abstract record EmbeddingModelResult;

public sealed record EmbeddingModelCompleted(EmbeddingResponse Response)
    : EmbeddingModelResult;

public sealed record EmbeddingModelFailed(ProviderFailure Failure)
    : EmbeddingModelResult;

public interface IEmbeddingModel
{
    EmbeddingModelAlias Alias { get; }

    Task<EmbeddingModelResult> GenerateAsync(
        EmbeddingModelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EmbeddingExecutionRequest(
    ProtectedSemanticOperationContext Operation,
    EmbeddingSelectionDecision Selection,
    EmbeddingRequest Request,
    HookDispatchContext? Hooks,
    SemanticOperationRetryPolicy RetryPolicy);

public abstract record EmbeddingExecutionResult;

public sealed record EmbeddingExecutionCompleted(
    EmbeddingSelectionDecision Selection,
    int Attempts,
    EmbeddingResponse Response) : EmbeddingExecutionResult;

public sealed record EmbeddingFallbackRequired(
    EmbeddingSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure) : EmbeddingExecutionResult;

public sealed record EmbeddingExecutionFailed(
    EmbeddingSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure) : EmbeddingExecutionResult;

public interface IEmbeddingRequestExecutor
{
    Task<EmbeddingExecutionResult> ExecuteAsync(
        EmbeddingExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RerankRequest(
    string Query,
    ImmutableArray<RerankDocument> Documents,
    int? TopCount,
    ProviderRequestOptions Options);

public sealed record RerankModelRequest(
    ProtectedSemanticOperationContext Operation,
    RerankerSelectionDecision Selection,
    RerankRequest Request,
    int Attempt,
    DateTimeOffset Deadline);

public sealed record RerankDocument(
    DocumentId Id,
    int InputIndex,
    string Text,
    ExtensionData Metadata);

public sealed record RerankResult(
    int InputIndex,
    DocumentId DocumentId,
    double RelevanceScore,
    ExtensionData Extensions);

public sealed record RerankResponse(
    ImmutableArray<RerankResult> Results,
    SemanticOperationUsage Usage,
    ProviderRequestId? ProviderRequestId,
    ExtensionData Extensions);

public abstract record RerankModelResult;

public sealed record RerankModelSucceeded(RerankResponse Response)
    : RerankModelResult;

public sealed record RerankModelFailed(ProviderFailure Failure)
    : RerankModelResult;

public interface IReranker
{
    RerankerAlias Alias { get; }

    Task<RerankModelResult> RerankAsync(
        RerankModelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RerankExecutionRequest(
    ProtectedSemanticOperationContext Operation,
    RerankerSelectionDecision Selection,
    RerankRequest Request,
    HookDispatchContext? Hooks,
    SemanticOperationRetryPolicy RetryPolicy);

public abstract record RerankExecutionResult;

public sealed record RerankExecutionSucceeded(
    RerankerSelectionDecision Selection,
    int Attempts,
    RerankResponse Response) : RerankExecutionResult;

public sealed record RerankFallbackRequired(
    RerankerSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure) : RerankExecutionResult;

public sealed record RerankExecutionFailed(
    RerankerSelectionDecision Selection,
    int Attempts,
    ProviderFailure Failure) : RerankExecutionResult;

public interface IRerankRequestExecutor
{
    Task<RerankExecutionResult> ExecuteAsync(
        RerankExecutionRequest request,
        CancellationToken cancellationToken = default);
}
```

Embedding and reranking calls are inherently external and return `Task`.
Requests preserve input order and caller correlation. A completed embedding
response contains exactly one `EmbeddingItemOutcome` for every input, ordered by
`InputIndex`, with the same `EmbeddingInputId`. Successful items validate
dimensions and finite values; failed items retain a typed `ProviderFailure` and
provider extension data. `EmbeddingModelFailed` and `EmbeddingExecutionFailed`
are reserved for request-wide failures where no complete per-item outcome set
exists. Vector compatibility uses `EmbeddingSpaceIdentity`, never dimension
alone. Reranking scores are order evidence within one response, not portable
probabilities.

An embedding or reranking alias missing from the catalog is a typed selection
failure. Unsupported purpose, modality, dimensions, encoding, truncation, top
count, or document shape fails before send. Providers never silently truncate,
drop purpose, coerce media to text, or emulate an unsupported semantic operation
through chat.

## First-party runtime classes and dependencies

```csharp
namespace AgentKit.Providers;

internal sealed class DefaultModelCatalog(
    IEnumerable<IModelDescriptorSource> sources,
    IModelCatalogValidationPolicy validationPolicy,
    TimeProvider timeProvider,
    ILogger<DefaultModelCatalog> logger) : IModelCatalog
{
}

internal sealed class DefaultModelSelector(
    IModelSelectionPolicyEvaluator policyEvaluator,
    IModelHealthSource healthSource,
    IRandomizer randomizer) : IModelSelector
{
}

internal sealed class DefaultModelRequestExecutor(
    IModelCatalog catalog,
    IModelCapabilityValidator capabilityValidator,
    IEnumerable<IChatModel> chatModels,
    IProviderRetryPolicy retryPolicy,
    IRunBudget runBudget,
    IHookDispatcher hookDispatcher,
    TimeProvider timeProvider,
    ILogger<DefaultModelRequestExecutor> logger) : IModelRequestExecutor
{
}

internal sealed class DefaultEmbeddingModelSelector(
    IModelSelectionPolicyEvaluator policyEvaluator)
    : IEmbeddingModelSelector
{
}

internal sealed class DefaultEmbeddingRequestExecutor(
    IModelCatalog catalog,
    IEnumerable<IEmbeddingModel> embeddingModels,
    IProviderRetryPolicy retryPolicy,
    TimeProvider timeProvider,
    ILogger<DefaultEmbeddingRequestExecutor> logger)
    : IEmbeddingRequestExecutor
{
}

internal sealed class DefaultRerankerSelector(
    IModelSelectionPolicyEvaluator policyEvaluator)
    : IRerankerSelector
{
}

internal sealed class DefaultRerankRequestExecutor(
    IModelCatalog catalog,
    IEnumerable<IReranker> rerankers,
    IProviderRetryPolicy retryPolicy,
    TimeProvider timeProvider,
    ILogger<DefaultRerankRequestExecutor> logger)
    : IRerankRequestExecutor
{
}
```

The class bodies are intentionally omitted from these constructor/dependency
shapes. Their observable APIs are exactly the corresponding abstraction
contracts above; the implementations add no service-resolution surface.

The catalog is an engine-wide thread-safe singleton over immutable descriptor
snapshots. A stateless selector and capability validator may be singleton. The
request executor is run-scoped because it owns attempt state and budget
reservations. Chat, embedding, and reranking adapters may be singleton only when
thread-safe and free of run state; otherwise they are scoped or transient and
the executor receives the current scope's explicit enumerable. No runtime class
uses a keyed-service locator.

Embedding and reranking selectors are stateless singletons by default. Their
request executors are scoped to the calling operation (and therefore to the run
when invoked during a run). Each executor maps only its own alias kind to an
explicitly injected adapter enumerable, applies that operation's limits and
retry policy, and returns its own typed result. They share catalog mechanics,
not adapter interfaces or retry side effects.

Selectors and executors are independent keyed run-plan choices. The loop or
semantic-operation pipeline invokes the configured selector first, then passes
that immutable decision to the configured executor. An executor never captures
an unkeyed selector, selects a replacement, or invokes a fallback candidate; it
returns the operation-specific `*FallbackRequired` outcome and lets its owner
select, rebuild any model-specific context, and issue a new request.

`DefaultModelRequestExecutor` maps a selected alias to exactly one injected
`IChatModel`, reserves budget before each attempt, and revalidates descriptor,
history affinity, deadline, and capability before each same-model attempt. It
may retry only under the configured provider policy and never after visible
output unless a new, observable repaired request is started by the loop.

## Justified protocol-family base classes

`AgentKit.Providers.OpenAICompatible` may expose optional base classes after
shared conformance proves common translation, transport, parsing, and error
behavior. The dependency shape remains explicit:

```csharp
namespace AgentKit.Providers.OpenAICompatible;

public abstract class OpenAICompatibleChatModelBase(
    ModelAlias alias,
    OpenAICompatibilityProfile profile,
    IOpenAIRequestTranslator translator,
    IOpenAIStreamParser streamParser,
    IProviderCredentialSource credentials,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grants,
    INetworkTransport networkTransport,
    IHookDispatcher hookDispatcher,
    TimeProvider timeProvider) : IChatModel
{
    public ModelAlias Alias { get; } = alias;

    public abstract Task<ModelAttemptResult> ExecuteAsync(
        ChatModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}
```

The concrete branded package supplies the profile, endpoint, credential source,
descriptor, and provider extensions. The base cannot broaden the profile or
become a provider identity. Native adapters and direct `IChatModel`
implementations remain first-class; inheritance is never the only extension
path.

Every concrete conversation, embedding, and reranking adapter follows the same
security dependency rule even when it does not use this base: select the
authority captured by `ProtectedSemanticOperationContext.Authorization` through
`ISecurityAuthoritySelector`, obtain and atomically consume a provider-egress
grant bound to the exact execution identity, destination, classified payload,
model revision, and attempt fingerprint, then ask `INetworkTransport` to obtain
and enforce its own lower-boundary network grant. An adapter never injects an
unkeyed `ISecurityAuthority`, reuses an egress grant for a retry, or treats the
semantic-operation context as authority.

## DI registration and replacement semantics

```csharp
namespace AgentKit.Providers;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentProviders(
            Action<AgentProviderRuntimeOptions>? configure = null) =>
            AgentProviderRegistration.AddRuntime(services, configure);

        public IServiceCollection AddModelDescriptorSource<TSource>()
            where TSource : class, IModelDescriptorSource =>
            AgentProviderRegistration.AddDescriptorSource<TSource>(services);

        public IServiceCollection ReplaceModelCatalog<TCatalog>()
            where TCatalog : class, IModelCatalog =>
            AgentProviderRegistration.ReplaceCatalog<TCatalog>(services);

        public IServiceCollection ReplaceModelCapabilityValidator<TValidator>()
            where TValidator : class, IModelCapabilityValidator =>
            AgentProviderRegistration.ReplaceCapabilityValidator<TValidator>(
                services);

        public IServiceCollection AddModelSelector<TSelector>(
            ComponentKey<IModelSelector> key)
            where TSelector : class, IModelSelector =>
            AgentProviderRegistration.AddModelSelector<TSelector>(services, key);

        public IServiceCollection ReplaceModelSelector<TSelector>(
            ComponentKey<IModelSelector> key)
            where TSelector : class, IModelSelector =>
            AgentProviderRegistration.ReplaceModelSelector<TSelector>(
                services,
                key);

        public IServiceCollection AddModelRequestExecutor<TExecutor>(
            ComponentKey<IModelRequestExecutor> key)
            where TExecutor : class, IModelRequestExecutor =>
            AgentProviderRegistration.AddModelExecutor<TExecutor>(services, key);

        public IServiceCollection ReplaceModelRequestExecutor<TExecutor>(
            ComponentKey<IModelRequestExecutor> key)
            where TExecutor : class, IModelRequestExecutor =>
            AgentProviderRegistration.ReplaceModelExecutor<TExecutor>(
                services,
                key);

        public IServiceCollection AddChatModel<TModel>(ModelAlias alias)
            where TModel : class, IChatModel =>
            AgentProviderRegistration.AddChatModel<TModel>(services, alias);

        public IServiceCollection ReplaceChatModel<TModel>(ModelAlias alias)
            where TModel : class, IChatModel =>
            AgentProviderRegistration.ReplaceChatModel<TModel>(services, alias);

        public IServiceCollection AddEmbeddingModel<TModel>(
            EmbeddingModelAlias alias)
            where TModel : class, IEmbeddingModel =>
            AgentProviderRegistration.AddEmbeddingModel<TModel>(services, alias);

        public IServiceCollection ReplaceEmbeddingModel<TModel>(
            EmbeddingModelAlias alias)
            where TModel : class, IEmbeddingModel =>
            AgentProviderRegistration.ReplaceEmbeddingModel<TModel>(
                services,
                alias);

        public IServiceCollection AddEmbeddingModelSelector<TSelector>(
            ComponentKey<IEmbeddingModelSelector> key)
            where TSelector : class, IEmbeddingModelSelector =>
            AgentProviderRegistration.AddEmbeddingSelector<TSelector>(
                services,
                key);

        public IServiceCollection ReplaceEmbeddingModelSelector<TSelector>(
            ComponentKey<IEmbeddingModelSelector> key)
            where TSelector : class, IEmbeddingModelSelector =>
            AgentProviderRegistration.ReplaceEmbeddingSelector<TSelector>(
                services,
                key);

        public IServiceCollection AddEmbeddingRequestExecutor<TExecutor>(
            ComponentKey<IEmbeddingRequestExecutor> key)
            where TExecutor : class, IEmbeddingRequestExecutor =>
            AgentProviderRegistration.AddEmbeddingExecutor<TExecutor>(
                services,
                key);

        public IServiceCollection ReplaceEmbeddingRequestExecutor<TExecutor>(
            ComponentKey<IEmbeddingRequestExecutor> key)
            where TExecutor : class, IEmbeddingRequestExecutor =>
            AgentProviderRegistration.ReplaceEmbeddingExecutor<TExecutor>(
                services,
                key);

        public IServiceCollection AddReranker<TReranker>(RerankerAlias alias)
            where TReranker : class, IReranker =>
            AgentProviderRegistration.AddReranker<TReranker>(services, alias);

        public IServiceCollection ReplaceReranker<TReranker>(
            RerankerAlias alias)
            where TReranker : class, IReranker =>
            AgentProviderRegistration.ReplaceReranker<TReranker>(services, alias);

        public IServiceCollection AddRerankerSelector<TSelector>(
            ComponentKey<IRerankerSelector> key)
            where TSelector : class, IRerankerSelector =>
            AgentProviderRegistration.AddRerankerSelector<TSelector>(
                services,
                key);

        public IServiceCollection ReplaceRerankerSelector<TSelector>(
            ComponentKey<IRerankerSelector> key)
            where TSelector : class, IRerankerSelector =>
            AgentProviderRegistration.ReplaceRerankerSelector<TSelector>(
                services,
                key);

        public IServiceCollection AddRerankRequestExecutor<TExecutor>(
            ComponentKey<IRerankRequestExecutor> key)
            where TExecutor : class, IRerankRequestExecutor =>
            AgentProviderRegistration.AddRerankExecutor<TExecutor>(services, key);

        public IServiceCollection ReplaceRerankRequestExecutor<TExecutor>(
            ComponentKey<IRerankRequestExecutor> key)
            where TExecutor : class, IRerankRequestExecutor =>
            AgentProviderRegistration.ReplaceRerankExecutor<TExecutor>(
                services,
                key);
    }
}
```

`AddAgentProviders` uses `TryAdd` for the engine-wide singular catalog and
capability validator and for default keyed conversational, embedding, and
reranking selectors/executors. The catalog and validator have explicit
replacement APIs; every selector and executor is singular per key. Descriptor
sources and operation adapters are additive, with unique aliases per operation
kind. Replacement names the exact operation and alias, so replacing an embedding
model cannot displace a chat model or reranker.

Concrete package entry points add only configured operations. `AddOpenAI` may
add chat and embedding aliases; `AddOpenRouter` may add chat, embedding, and
reranker aliases; `AddZAi` adds only its verified operations. Provider endpoint,
deployment, credential source, account/project identity, and model IDs are
required external facts validated at startup. No default fabricates credentials,
authority, deployment, or a model an account may not support.

Runtime options and the agent definition configure selection order, health/load
inputs, capability downgrade, retry count/backoff/jitter, fallback, request and
stream deadlines, output/byte bounds, and unknown-extension policy. Defaults
reject semantic downgrade, do not retry after visible output, honor
cancellation, bound all transport phases, and preserve unknown safe response
data. Provider specific options remain typed in the leaf package and cannot
override protected identity, authentication, security, or budget fields.

Build and catalog-refresh validation reject missing selected keys, duplicate
aliases, descriptor/adapter mismatches, impossible limits, unsupported
configured capabilities, singleton capture of run state, unsafe retry
combinations, absent network/security collaborators, and incomplete credential
configuration. Embedding and reranking configuration additionally requires a
complete policy-selector-executor triple and at least one compatible alias for
that operation; neither is inferred from the conversational selection.
Authentication, authorization, throttling, invalid request, unavailable,
timeout, cancellation, protocol violation, unsupported capability, and unknown
failures are normalized typed outcomes retaining safe provider status, code,
request ID, retry hints, and diagnostic cause.

Cancellation flows through selection, hooks, credentials, authorization,
transport, parser, observer, and settlement. One adapter invocation owns only
its attempt. The run-scoped executor owns attempt sequencing; the loop owns
whether a new turn/request is safe; the container owns adapters and shared
transport resources. No canceled or malformed stream can synthesize completion.

## Related documentation

- [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)
- [Provider request pipeline](../concepts/provider-request-pipeline.md)
- [Provider research](../providers/index.md)
- [Semantic operations](../providers/semantic-operations.md)
- [Project structure](project-structure.md)
