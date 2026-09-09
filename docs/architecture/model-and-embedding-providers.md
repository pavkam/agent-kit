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
| AgentKit.Providers.ZAI              | Z.ai identity, endpoints, credentials, Chat Completions profile, and provider-native operations         | Conversation and only other operations supported by its verified profile |

OpenAICompatible is deliberately not the package applications normally select as
their provider. OpenAI, OpenRouter, and Z.ai have different authentication,
extensions, model catalogs, capability claims, errors, and usage even when some
requests look alike.

Each concrete package exposes an ASP.NET-style registration entry point:
AddOpenAI, AddOpenRouter, or AddZAI. Registrations may add several named models,
but conversation, embedding, and reranking capabilities remain independent.
Adding OpenAI can register both conversational and embedding models. Adding
OpenRouter can register conversational, embedding, and reranking models. Z.ai
does not advertise an embedding implementation unless its current verified API
actually provides one.

## Identity and capabilities

The [model capability contract](../concepts/model-providers-and-capabilities.md)
makes this identity tuple observable before any request is sent.

Provider, API family, service surface, endpoint, deployment, and model are
distinct identities. Capabilities belong to that configured combination, not to
a brand name. A descriptor states supported roles, modalities, tools, structured
output, streaming, reasoning, continuation, usage reporting, response
multiplicity, schema dialects, and context limits.

A configured provider operation also has three independently selectable
composition bindings:

- an endpoint profile chooses the service surface, origin, region, API version,
  deployment, and transport policy;
- a descriptor retains the exact versioned compatibility profile that supplies
  portable wire-behavior evidence for that configured operation;
- a credential profile chooses one account or workload identity plus its
  authentication scheme, audience, scopes, refresh, and rotation policy; and
- an operation registration binds one chat, embedding, reranking, media, or
  other adapter and model descriptor to exactly one endpoint profile and one
  credential profile.

The operation registration captures those keys and profile versions before an
attempt. The adapter never discovers an unkeyed process-global credential
source, pairs whichever options happened to register first with another
provider's model, or changes endpoint/account binding during an in-flight
attempt. One engine can therefore use several providers, endpoints, service
surfaces, and accounts concurrently without credential or options leakage.

A `CompatibilityProfile` is immutable portable evidence. Its key, version, and
fingerprint identify the published wire-behavior profile retained by a
`ModelDescriptor`; the value itself neither resolves that identity nor verifies
the fingerprint. A publisher rejects two different profile bodies for the same
key and version, including a changed fingerprint, rather than choosing one at
lookup time. The profile's dialect array is ordered, initialized, unique, and
may be empty for an operation with no tool schema support. A nonempty profile
dialect set remains valid when a particular descriptor's effective
`ModelCapabilities` disables tools; when a request does select tools, preflight
requires a dialect supported by both the selected tool schema and the profile.

`ExactlyOne` describes the first-party single-candidate operation contract.
`Multiple` requires a separate candidate-aware operation with stable candidate
identities and interleaving rules. Usage-reporting modes state which report
phases an operation can expose: `NotReported`, `TerminalOnly`, `InterimOnly`, or
`StreamingAndTerminal`. They do not promise complete counters or a report on
every successful response; missing usage remains `NotReported`.

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
safe unknown fields when supported. Provider translation is not assumed to be
one neutral message to one wire message: a compatibility profile may expand or
coalesce messages and parts only through a loss-aware mapping that retains
source order, trust, provenance, and tool correlation. In particular, a
synthetic runtime notice is never elevated to system or developer authority
merely because the wire protocol lacks a runtime role.

Tool-result translation consumes only the bounded `ToolResultPart` message
projection. It preserves the requested alias, resolved identity/version when
available, source terminal status, side-effect uncertainty, and projection-loss
markers; it never reconstructs the authoritative execution record, retries the
tool, or infers success from display text.

Credentials stay inside the concrete integration and are injected only at send
time from the credential profile captured by the operation registration.
Transport limits cover connection, headers, idle streams, total duration,
frames, bytes, redirects, and decompression.

## Shared wire families

AgentKit.Providers.OpenAICompatible exposes reusable base classes and services
only where they remove proven duplication: request translation, JSON contracts,
HTTP behavior, SSE parsing, stream assembly, common errors, compatibility
profile evaluation, and secret-safe credential transport mechanics. A shared
mechanic may obtain an opaque token or construct a profile-approved
authorization header; it never decides that a branded provider supports an
authentication scheme, chooses an audience or scope, owns account selection, or
invents refresh policy. Those decisions and the credential-profile binding
remain in the concrete provider package. Direct interface implementation remains
supported.

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

Provider-backed web search follows that independent-operation rule through
`IWebSearchProvider`. It is not an optional method on `ILlmModel` and does not
inherit a conversational model's endpoint or credential by registration order.
The selected operation exposes a stable provider identity, exact secret-free
destination, and effecting security audience; its leaf adapter owns credentials,
wire behavior, one-attempt execution, and exact egress-grant consumption.

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
public readonly record struct ProviderServiceSurfaceId(string Value);
public readonly record struct ProviderEndpointId(string Value);
public readonly record struct ProviderEndpointProfileKey(string Value);
public readonly record struct ProviderCredentialProfileKey(string Value);
public readonly record struct ProviderEndpointProfileVersion(long Value);
public readonly record struct ProviderCredentialProfileVersion(long Value);
public readonly record struct ProviderCredentialSourceKey(string Value);
public readonly record struct ProviderAccountId(string Value);
public readonly record struct ProviderApiVersion(string Value);
public readonly record struct DeploymentId(string Value);
public readonly record struct ModelDescriptorSourceId(string Value);
public readonly record struct EmbeddingInputId(Guid Value);
public readonly record struct CompatibilityProfileKey(string Value);
public readonly record struct CompatibilityProfileVersion(long Value);
public readonly record struct ModelDescriptorRevision(long Value);
public readonly record struct ModelCatalogVersion(long Value);
public readonly record struct ModelDescriptorSourceVersion(long Value);
public readonly record struct ProviderModelRevision(string Value);

public sealed record ProviderEndpointProfileReference(
    ProviderEndpointProfileKey Key,
    ProviderEndpointProfileVersion Version);

public sealed record ProviderCredentialProfileReference(
    ProviderCredentialProfileKey Key,
    ProviderCredentialProfileVersion Version);

public sealed record ProviderOperationBinding(
    ProviderEndpointProfileReference Endpoint,
    ProviderCredentialProfileReference Credential);

public enum ModelCandidateMultiplicity
{
    ExactlyOne = 0,
    Multiple = 1,
}

public enum ModelUsageReportingMode
{
    NotReported = 0,
    TerminalOnly = 1,
    InterimOnly = 2,
    StreamingAndTerminal = 3,
}

public sealed record CompatibilityProfile(
    CompatibilityProfileKey Key,
    CompatibilityProfileVersion Version,
    ContentHash Fingerprint,
    ModelCandidateMultiplicity RequestMultiplicity,
    ModelCandidateMultiplicity ResponseMultiplicity,
    ModelUsageReportingMode UsageReporting,
    ImmutableArray<JsonSchemaDialectId> SupportedToolSchemaDialects,
    ExtensionData Extensions);

public sealed record ProviderEndpointProfileSnapshot(
    ProviderEndpointProfileReference Reference,
    ProviderId ProviderId,
    ProviderServiceSurfaceId ServiceSurface,
    ProviderEndpointId EndpointId,
    Uri BaseAddress,
    ProviderApiVersion? ApiVersion,
    ContentHash ConfigurationFingerprint,
    ExtensionData Extensions);

public sealed record ProviderCredentialProfileSnapshot(
    ProviderCredentialProfileReference Reference,
    ProviderId ProviderId,
    ProviderServiceSurfaceId ServiceSurface,
    ProviderCredentialSourceKey SourceKey,
    ProviderAccountId? AccountId,
    TimeSpan RefreshSkew,
    ContentHash ConfigurationFingerprint,
    ExtensionData Extensions);

public sealed record ModelDescriptor(
    ModelAlias Alias,
    ProviderId ProviderId,
    ApiFamilyId ApiFamily,
    ProviderServiceSurfaceId ServiceSurface,
    ProviderEndpointId EndpointId,
    ProviderOperationBinding Binding,
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
    ProviderServiceSurfaceId ServiceSurface,
    ProviderEndpointId EndpointId,
    ProviderOperationBinding Binding,
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
    ProviderServiceSurfaceId ServiceSurface,
    ProviderEndpointId EndpointId,
    ProviderOperationBinding Binding,
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
    ProviderServiceSurfaceId ServiceSurface,
    ProviderEndpointId EndpointId,
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

public interface IProviderProfileRuntimeLease : IAsyncDisposable
{
    ProviderEndpointProfileSnapshot Endpoint { get; }
    ProviderCredentialProfileSnapshot Credential { get; }
    IProviderCredentialSource CredentialSource { get; }
}

public sealed record ProviderCredentialResolutionRequest(
    ProviderEndpointProfileSnapshot Endpoint,
    ProviderCredentialProfileSnapshot Credential,
    ProtectedSemanticOperationContext Operation,
    int Attempt,
    DateTimeOffset Deadline,
    SecurityGrant CredentialGrant);

public abstract record ProviderCredentialResolutionResult;

public sealed record ProviderCredentialResolved(
    IProviderCredentialLease Credential)
    : ProviderCredentialResolutionResult;

public sealed record ProviderCredentialUnavailable(ProviderFailure Failure)
    : ProviderCredentialResolutionResult;

public interface IProviderCredentialSource
{
    ProviderCredentialSourceKey Key { get; }

    ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
        ProviderCredentialResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProviderCredentialLease : IAsyncDisposable
{
    ValueTask ApplyAsync(
        IProviderAuthenticationTarget target,
        CancellationToken cancellationToken = default);
}

public abstract record ProviderProfileRuntimeSelectionResult;

public sealed record ProviderProfileRuntimeSelected(
    IProviderProfileRuntimeLease Runtime)
    : ProviderProfileRuntimeSelectionResult;

public sealed record ProviderProfileRuntimeUnavailable(
    ProviderOperationBinding Binding,
    ProviderFailure Failure)
    : ProviderProfileRuntimeSelectionResult;

public interface IProviderProfileRuntimeSelector
{
    ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
        ProviderOperationBinding binding,
        ProtectedSemanticOperationContext operation,
        CancellationToken cancellationToken = default);
}
```

`ProviderFailure.RetryAfter` is the normalized non-negative delay at response
observation time. Safe extension evidence retains whether it came from a delay,
absolute date, or provider-specific reset and the injected-`TimeProvider`
observation instant; the failure value never instructs a component to retry.

Aliases are application selection keys; provider, API family, service surface,
endpoint, deployment, and model are actual identities. A service surface
distinguishes materially different products, routing, billing, retention, or
authentication paths exposed by one provider. An endpoint identifies the
configured network/service target without exposing credentials; a deployment
identifies the provider-side model deployment when that concept exists. None is
an alias for another merely because two profiles currently share an origin.

Endpoint and credential profile keys/versions are composition references, not
response identities or secret containers. Each operation descriptor captures one
exact `ProviderOperationBinding`; its endpoint and secret-free credential
snapshots are version-retained for the lifetime of an in-flight operation. The
single engine-wide `IProviderProfileRuntimeSelector` resolves only that captured
pair and returns an owned lease containing the matching credential source. It
never falls back to a newer profile, an unkeyed source, or another account.

The full operation binding is classified execution/configuration evidence, not
assistant message metadata. `ProviderResponseIdentity` retains the actual
provider, service surface, endpoint, deployment, and model identity but omits
the credential profile reference. Security audit may retain that reference under
its own redaction and access policy; model-visible or ordinary durable
conversation data may not expose it.

The first-party catalog composes additive descriptor sources into one immutable,
versioned, thread-safe snapshot. An invalid discovery refresh leaves the last
good snapshot active. The catalog does not retain provider SDK clients or return
a union descriptor that claims capabilities no single configured operation
supports.

Discovery is itself a declared capability. A descriptor source identifies
whether it is authoritative credential-scoped runtime discovery, a generated
vendor-feed snapshot, a reviewed static baseline, or an application override,
with checked-at time, provenance, and confidence. The catalog never assumes
every provider supports live discovery or promotes name-based capability
heuristics to verified facts.

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

public enum ModelUsageReportState
{
    NotReported,
    Interim,
    Final
}

public sealed record ModelUsage(
    ModelUsageReportState ReportState,
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    long? ReasoningTokens,
    decimal? EstimatedCost,
    string? CostCurrency,
    ExtensionData Extensions)
{
    public static ModelUsage NotReported { get; } = new(
        ModelUsageReportState.NotReported,
        null, null, null, null, null, null, ExtensionData.Empty);
}
```

`ModelUsage.NotReported` means the provider supplied no usage evidence. It is
not reported zero and budget accounting must not release or reconcile a
reservation as though no resources were consumed. `Interim` and `Final` describe
the provider report's lifecycle, not whether every portable counter is known;
individual null counters remain unknown. A successful response may legitimately
carry `NotReported` when the selected profile permits omitted usage, but the
adapter emits no fabricated `ModelUsageUpdated` event and the budget owner
applies its configured estimate/unknown-usage reconciliation policy.
Provider-specific counters that do not fit these portable fields remain in
`Extensions` rather than being dropped or force-fit.

```csharp
namespace AgentKit;

public sealed record LlmModelRequest(
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

public interface ILlmModel
{
    ModelAlias Alias { get; }

    Task<ModelAttemptResult> ExecuteAsync(
        LlmModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}

public sealed record ModelExecutionRequest(
    ProtectedSemanticOperationContext Operation,
    ModelSelectionDecision Selection,
    ModelRequestContext Context,
    BudgetExecutionCapability Budget,
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

This minimal conversational stream represents exactly one response candidate.
The descriptor and compatibility profile declare request and response
multiplicity. The first-party single-candidate operation requests one candidate
and verifies that the response contains one; it never selects the first element
and silently discards siblings. A provider response with extra candidates is a
protocol violation unless the host selected a distinct multi-candidate
operation. Such an operation must add stable candidate identity to every event,
define interleaving and per-candidate terminal rules, and delegate candidate
selection to an explicit policy above the adapter before any assistant message
is committed.

`ContentDelta` is a typed portable family for text, reasoning, tool-argument,
structured-data, and typed provider-extension fragments. A provider adapter may
add a new portable subtype through additive contract evolution; it never hides
fragment kind or tool-call correlation in an untyped string callback. A
cancellation terminal and its returned result use a `ProviderFailure` whose kind
is `Cancellation`; all other failures use `ModelResponseFailed` and
`ModelAttemptFailed`.

Only `ModelResponseCompleted` carries the validated terminal `ModelResponse`
aggregate. This is still a candidate until the session commit succeeds. Its
ordered parts must equal the completed part events and its usage must equal the
last usage event, or a final provider usage report that arrived only with the
terminal response. When the provider reports no usage, the aggregate carries
`ModelUsage.NotReported` and no usage event is synthesized. Failure and
cancellation terminals preserve every bounded partial part and the last known
usage without presenting them as a successful response. The returned
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

A terminal observer failure does not permit a second provider terminal. Once a
validated terminal aggregate is known, the adapter returns that same outcome and
the owning publisher records delivery failure separately. Before terminal, an
observer failure may stop the attempt with truthful partial and usage evidence.
No retry follows merely because terminal delivery acknowledgement was lost; the
runtime reconciles publication and preserves the provider's known outcome.

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
    ProviderServiceSurfaceId ServiceSurface,
    ProviderEndpointId EndpointId,
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
    BudgetExecutionCapability Budget,
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
    BudgetExecutionCapability Budget,
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
    IRandomizerFactory randomizers) : IModelSelector
{
}

internal sealed class DefaultModelRequestExecutor(
    IModelCapabilityValidator capabilityValidator,
    IEnumerable<ILlmModel> llmModels,
    IProviderRetryPolicy retryPolicy,
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
    IEnumerable<IEmbeddingModel> embeddingModels,
    IProviderRetryPolicy retryPolicy,
    IHookDispatcher hookDispatcher,
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
    IEnumerable<IReranker> rerankers,
    IProviderRetryPolicy retryPolicy,
    IHookDispatcher hookDispatcher,
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

When a selection policy requires randomized tie-breaking, the default selector
asks `IRandomizerFactory` for one operation-owned stream keyed by the request's
typed `OperationId` and a model-selection purpose. It records the returned
descriptor with the decision and never stores the randomizer in singleton state.
Deterministic policies do not create or consume a random stream.

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

Executors also never re-read the live model catalog after selection. They map
the complete immutable descriptor in the selection decision to exactly one
injected alias adapter and validate the captured `ModelCatalogVersion`. A
catalog refresh can affect a later selection, never an in-flight execution.
Embedding and reranking executors validate the supplied
`BudgetExecutionCapability`, reserve expected work before every attempt, settle
actual usage exactly once, and dispatch the invocation's hook context through
`IHookDispatcher`. Their constructors do not capture a run budget because these
semantic operations may execute outside an agent run.

`DefaultModelRequestExecutor` maps a selected alias to exactly one injected
`ILlmModel`, validates the request's exact `BudgetExecutionCapability`, reserves
before each attempt, and revalidates descriptor, history affinity, deadline, and
capability before each same-model attempt. It does not capture an `IRunBudget`.
It may retry only under the configured provider policy and never after visible
output unless a new, observable repaired request is started by the loop.

## Justified protocol-family base classes

`AgentKit.Providers.OpenAICompatible` may expose optional base classes after
shared conformance proves common translation, transport, parsing, and error
behavior. The dependency shape remains explicit:

```csharp
namespace AgentKit.Providers.OpenAICompatible;

public abstract class OpenAICompatibleLlmModelBase(
    ModelAlias alias,
    OpenAICompatibilityProfile profile,
    IOpenAIRequestTranslator translator,
    IOpenAIStreamParser streamParser,
    IProviderProfileRuntimeSelector providerProfiles,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grants,
    INetworkTransport networkTransport,
    IHookDispatcher hookDispatcher,
    TimeProvider timeProvider) : ILlmModel
{
    public ModelAlias Alias { get; } = alias;

    public abstract Task<ModelAttemptResult> ExecuteAsync(
        LlmModelRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}
```

The concrete branded package supplies the compatibility profile, descriptor with
exact endpoint/credential profile references, and provider extensions. For each
attempt, the adapter asks `IProviderProfileRuntimeSelector` for that captured
binding, holds the returned lease through credential injection and send, then
disposes it. The selector returns only the version-retained, secret-free
snapshots and exact credential source; the adapter never resolves an unkeyed
`IProviderCredentialSource` from the container. A protocol-family credential
helper may handle opaque secret material and expiry mechanically, but the
branded package declares which schemes it supports and owns audience, scope,
account, refresh, and header policy. The base cannot broaden the profile or
become a provider identity. Native adapters and direct `ILlmModel`
implementations remain first-class; inheritance is never the only extension
path.

Every concrete conversation, embedding, and reranking adapter follows the same
security dependency rule even when it does not use this base: select the
authority captured by `ProtectedSemanticOperationContext.Authorization` through
`ISecurityAuthoritySelector`; obtain a credential-read grant bound to the exact
profile revision, source, account, audience, identity, attempt, and deadline;
and pass it to the selected credential source. The source validates the grant
immediately before releasing one opaque, disposable credential lease. The
adapter then obtains and atomically consumes a distinct provider-egress grant
bound to the destination, classified payload, model revision, and attempt
fingerprint, and obtains separate resolution and send grants for the narrow
network boundaries. `INetworkNameResolver` and `INetworkTransport` consume their
supplied grants; the transport never obtains implicit authority for the adapter.
An adapter never injects an unkeyed authority, reuses a grant for another
boundary or retry, exposes raw credential material, or treats the
semantic-operation context as authority.

## DI registration and replacement semantics

```csharp
namespace AgentKit.Providers;

public sealed class AgentProviderRuntimeOptions
{
    public int MaximumAttempts { get; set; } = 1;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan StreamIdleTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool AllowSemanticFallback { get; set; }
}

public sealed class ProviderEndpointProfileOptions
{
    public ProviderEndpointProfileVersion Version { get; set; } = new(1);
    public ProviderId? ProviderId { get; set; }
    public ProviderServiceSurfaceId? ServiceSurface { get; set; }
    public ProviderEndpointId? EndpointId { get; set; }
    public Uri? BaseAddress { get; set; }
    public ProviderApiVersion? ApiVersion { get; set; }
}

public sealed class ProviderCredentialProfileOptions
{
    public ProviderCredentialProfileVersion Version { get; set; } = new(1);
    public ProviderId? ProviderId { get; set; }
    public ProviderServiceSurfaceId? ServiceSurface { get; set; }
    public ProviderCredentialSourceKey? SourceKey { get; set; }
    public ProviderAccountId? AccountId { get; set; }
    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(5);
}

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

        public IServiceCollection AddProviderEndpointProfile(
            ProviderEndpointProfileKey key,
            Action<ProviderEndpointProfileOptions> configure) =>
            AgentProviderRegistration.AddEndpointProfile(
                services,
                key,
                configure);

        public IServiceCollection ReplaceProviderEndpointProfile(
            ProviderEndpointProfileKey key,
            Action<ProviderEndpointProfileOptions> configure) =>
            AgentProviderRegistration.ReplaceEndpointProfile(
                services,
                key,
                configure);

        public IServiceCollection AddProviderCredentialProfile(
            ProviderCredentialProfileKey key,
            Action<ProviderCredentialProfileOptions> configure) =>
            AgentProviderRegistration.AddCredentialProfile(
                services,
                key,
                configure);

        public IServiceCollection ReplaceProviderCredentialProfile(
            ProviderCredentialProfileKey key,
            Action<ProviderCredentialProfileOptions> configure) =>
            AgentProviderRegistration.ReplaceCredentialProfile(
                services,
                key,
                configure);

        public IServiceCollection AddProviderCredentialSource<TSource>(
            ProviderCredentialSourceKey key)
            where TSource : class, IProviderCredentialSource =>
            AgentProviderRegistration.AddCredentialSource<TSource>(
                services,
                key);

        public IServiceCollection ReplaceProviderCredentialSource<TSource>(
            ProviderCredentialSourceKey key)
            where TSource : class, IProviderCredentialSource =>
            AgentProviderRegistration.ReplaceCredentialSource<TSource>(
                services,
                key);

        public IServiceCollection
            ReplaceProviderProfileRuntimeSelector<TSelector>()
            where TSelector : class, IProviderProfileRuntimeSelector =>
            AgentProviderRegistration.ReplaceProfileRuntimeSelector<TSelector>(
                services);

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

        public IServiceCollection AddLlmModel<TModel>(ModelAlias alias)
            where TModel : class, ILlmModel =>
            AgentProviderRegistration.AddLlmModel<TModel>(services, alias);

        public IServiceCollection ReplaceLlmModel<TModel>(ModelAlias alias)
            where TModel : class, ILlmModel =>
            AgentProviderRegistration.ReplaceLlmModel<TModel>(services, alias);

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
capability validator, endpoint/credential profile catalogs, and profile runtime
selector, plus default keyed conversational, embedding, and reranking
selectors/executors. Every singular service has an explicit replacement API;
every selector and executor is singular per key. Descriptor sources, credential
sources, and operation adapters are additive under typed unique keys or aliases.
Replacement names the exact axis, so replacing an embedding model cannot
displace a chat model, reranker, credential source, or another profile.

Mutable profile options are validated and copied to immutable secret-free
snapshots. Versions are positive and must advance when endpoint, surface,
account/source binding, API version, or refresh policy changes. A replacement is
visible only to later catalog snapshots and operations; version-retained leases
keep in-flight requests on their captured pair. Base provider registration
supplies no endpoint, account, credential source, or secret. Missing external
facts fail startup validation rather than receiving fabricated defaults.

Concrete package entry points add only configured operations. `AddOpenAI` may
add chat and embedding aliases; `AddOpenRouter` may add chat, embedding, and
reranker aliases; `AddZAI` adds only its verified operations. Each alias is
registered with one immutable descriptor and one explicit binding to a keyed,
versioned endpoint profile and a keyed, versioned credential profile. Endpoint
profiles and credential profiles are independently reusable, replaceable, and
collision-checked; operation registration never relies on the first unkeyed
credential or options object in the container.

Provider endpoint, service surface, deployment, credential source,
account/project identity, and model IDs are external facts validated at startup.
A branded package may offer a named helper for a provider's well-known public
service origin, but the host must opt into that endpoint profile and the
resolved origin, API version, surface, and profile version are captured exactly
like custom configuration. No registration silently guesses an origin or
fabricates credentials, authority, deployment, or a model an account may not
support.

Credential material is resolved for every attempt immediately before header
injection. Resolution receives the captured provider, service surface, endpoint,
account/credential profile, operation kind, attempt, deadline, and execution
identity. Expired or near-expiry material is refreshed using the profile's
declared skew and rotation policy; failure returns a typed pre-I/O
authentication result. Secret values never enter descriptor snapshots, options
display, exceptions, manifests, logs, or service keys.

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
combinations, absent network/security collaborators, missing or ambiguous
endpoint/credential bindings, descriptor-to-surface mismatch, and incomplete
credential configuration. Embedding and reranking configuration additionally
requires a complete policy-selector-executor triple and at least one compatible
alias for that operation; neither is inferred from the conversational selection.
Authentication, authorization, throttling, invalid request, unavailable,
timeout, cancellation, protocol violation, unsupported capability, and unknown
failures are normalized typed outcomes retaining safe provider status, code,
request ID, retry hints, and diagnostic cause.

Cancellation flows through selection, hooks, credentials, authorization,
transport, parser, observer, and settlement. One adapter invocation owns only
its attempt. The run-scoped executor owns attempt sequencing; the loop owns
whether a new turn/request is safe; the container owns adapters and shared
transport resources. No canceled or malformed stream can synthesize completion.

Provider retry hints are advisory typed evidence, never authorization to retry.
HTTP-family adapters accept both delay and absolute-date forms, normalize them
at response-observation time with `TimeProvider`, clamp past dates to a
non-negative delay, retain safe provenance, and apply configured maximum-delay,
deadline, attempt, capability, and side-effect rules before the executor waits.
Malformed hints are diagnosed and ignored rather than converted into ambient
wall-clock sleeps.

## Related documentation

- [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)
- [Provider request pipeline](../concepts/provider-request-pipeline.md)
- [Provider research](../providers/index.md)
- [Semantic operations](../providers/semantic-operations.md)
- [Project structure](project-structure.md)
