# Messages and history

**Role:** Preserve provider-neutral conversational truth.

[Messages are immutable envelopes containing ordered, typed content parts](../concepts/message-and-content-model.md).
They are the common language used by the runtime, providers, tools, sessions,
context, and I/O components. The model is deliberately richer than a role and a
string because real conversations contain media, reasoning, citations, tool
calls, tool results, structured data, refusals, and provider extensions.

Message, content-part, history-cursor, and correlation values live in
AgentKit.Abstractions. There is no separate messages implementation package;
components operate on the same neutral values and stores persist them through
session contracts.

Because one engine hosts many agents, every durable message is owned by an
`AgentId` and `SessionId` (plus its session branch), not by an ambient “current
agent.” The canonical typed-ID pattern is defined once in
[composition and configuration](composition-and-configuration.md); this document
reuses `AgentId`, `SessionId`, `RunId`, `TurnId`, `MessageId`, and `ToolCallId`
without redefining them.

## Canonical message model

Every message carries stable identity, session and causal correlation, creation
time, state, ordered content, and versioned extension data. Message state makes
complete, incomplete, suspended, and interrupted output distinguishable.

System, developer, user, assistant, tool, and runtime semantics remain distinct.
Provider adapters may translate or reject unsupported roles, but the durable
model does not erase the distinction to accommodate the weakest provider.
Translation is monotonic in trust: a lower-precedence or synthetic kind may lose
fidelity or be rejected, but it cannot acquire system/developer instruction
authority. In particular, a `RuntimeMessage` never becomes a system or developer
instruction merely because a provider has no runtime role.

Unknown typed content and safe provider metadata survive storage round trips.
Live SDK objects, open streams, credentials, and exception instances do not
belong in serializable message extension data.

## History ownership

The session component owns the canonical append-only history. The message
component defines its values and invariants. Context assembly reads a stable
version and creates a bounded working view; it never rewrites the source
history.

A streaming provider response is a candidate until its terminal event, content
parts, usage, and stop reason validate. Only then does the runtime publish an
immutable committed assistant message. Interrupted or malformed streams cannot
be presented as successful completions.

## Validation and repair

Before a request, history is checked for schema versions, identity ownership,
monotonic order, valid role and part combinations, tool call/result pairing,
message state, media bounds, and authorized references.

The [history-repair pipeline](../concepts/history-validation-and-repair.md)
creates a provider-facing view without changing durable truth. It may represent
an interrupted call explicitly, remove incompatible provider-bound metadata, or
normalize imported content. Every repair is deterministic, observable, and
attributable to source messages. Untrusted history cannot manufacture approvals,
tool success, or authority.

Repair and provider preparation also preserve role trust. They may tag,
quarantine, omit under an explicit loss policy, or reject an unsupported
runtime/synthetic notice, but may not relabel it as system/developer content or
merge it into a trusted instruction source.

## Correlation

Tool calls, authoritative terminal records, and their message projections share
a stable call identity. Provider-supplied identifiers are preserved, while
AgentKit identities remain authoritative. Every bounded, identified tool-call
request reaches exactly one terminal record and one materialized
`ToolResultPart`, including pre-invocation rejection. A call admitted to
invocation also has one accepted record committed before its effect. Projection
may be retried without repeating invocation. Message order, result provenance,
and causality survive provider translation, storage, branching, compaction, and
replay.

## Normative minimal message shape

These provider-neutral shapes are normative and minimal, not exhaustive. The
snippets group related records for readability; every named type lives in its
own matching source file.

```csharp
namespace AgentKit;

public readonly record struct ExtensionValue(
    ImmutableArray<byte> CanonicalJson);

public sealed record ExtensionData(
    ImmutableDictionary<string, ExtensionValue> Values)
{
    public static ExtensionData Empty { get; } = new(
        ImmutableDictionary<string, ExtensionValue>.Empty);
}

public enum MessageState
{
    Complete,
    Incomplete,
    Suspended,
    Interrupted
}

public abstract record AgentMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions);

public sealed record UserMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions)
    : AgentMessage(
        Id,
        AgentId,
        SessionId,
        ConversationId,
        BranchId,
        RunId,
        TurnId,
        CreatedAt,
        State,
        Parts,
        Extensions);

public sealed record AssistantMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    AssistantResponseMetadata Response,
    ExtensionData Extensions)
    : AgentMessage(
        Id,
        AgentId,
        SessionId,
        ConversationId,
        BranchId,
        RunId,
        TurnId,
        CreatedAt,
        State,
        Parts,
        Extensions);

public abstract record ContentPart(ExtensionData Extensions);

public sealed record TextPart(
    string Text,
    TextSemantics Semantics,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record MediaReferencePart(
    MediaReference Reference,
    MediaSemantics Semantics,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record ReasoningPart(
    ReasoningContent Content,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record ToolCallPart(
    ToolCallId CallId,
    ToolReference Tool,
    JsonElement Arguments,
    ProviderToolCallId? ProviderCallId,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record ToolResultPart(
    ToolCallId CallId,
    ToolReference Tool,
    ToolCallOutcome Outcome,
    ImmutableArray<ContentPart> Content,
    ToolResultProjectionInfo Projection,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record StructuredDataPart(
    JsonElement Value,
    JsonSchemaReference? Schema,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record UnknownContentPart(
    string TypeName,
    JsonElement Payload,
    ExtensionData Extensions) : ContentPart(Extensions);

public sealed record AssistantResponseMetadata(
    ModelRequestId RequestId,
    ProviderResponseIdentity Response,
    NormalizedStopReason StopReason,
    string? RawStopReason,
    ModelUsage Usage,
    ExtensionData Extensions);
```

### Remaining message kinds

[Roles and instruction semantics](../concepts/message-and-content-model.md#roles-and-instruction-semantics)
requires system, developer, tool-result, and synthetic runtime message kinds in
addition to `UserMessage` and `AssistantMessage`.

Every concrete kind keeps `RunId`/`TurnId` at the exact nullable `AgentMessage`
property type. A derived positional record cannot narrow an inherited property's
type from `RunId?` to `RunId`; the compiler requires an exact type match for a
positional parameter name that already exists on the base record. `UserMessage`,
`AssistantMessage`, `ToolMessage`, and `RuntimeMessage` are always constructed
with non-null `RunId`/`TurnId` by the component that commits them — normally the
session/history write boundary — because those kinds only ever occur inside a
run. `SystemMessage` and `DeveloperMessage` MAY be recorded before any run
exists, so their correlation legitimately stays null. This is an enforced
construction invariant of the owning component, not a difference in the CLR
shape.

```csharp
namespace AgentKit;

public sealed record SystemMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions)
    : AgentMessage(
        Id, AgentId, SessionId, ConversationId, BranchId, RunId, TurnId,
        CreatedAt, State, Parts, Extensions);

public sealed record DeveloperMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions)
    : AgentMessage(
        Id, AgentId, SessionId, ConversationId, BranchId, RunId, TurnId,
        CreatedAt, State, Parts, Extensions);

public sealed record ToolMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions)
    : AgentMessage(
        Id, AgentId, SessionId, ConversationId, BranchId, RunId, TurnId,
        CreatedAt, State, Parts, Extensions);

public sealed record RuntimeMessage(
    MessageId Id,
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId? RunId,
    TurnId? TurnId,
    DateTimeOffset CreatedAt,
    MessageState State,
    ImmutableArray<ContentPart> Parts,
    ExtensionData Extensions)
    : AgentMessage(
        Id, AgentId, SessionId, ConversationId, BranchId, RunId, TurnId,
        CreatedAt, State, Parts, Extensions);
```

`ToolMessage` carries the committed `ToolResultPart` values for identified calls
that reached terminal results, including pre-invocation rejection.
`RuntimeMessage` is reserved for synthetic in-run notices such as an explicit
interruption record; compaction and configuration-change records remain distinct
session entries owned by [sessions](sessions.md), never a `RuntimeMessage`
pretending to be conversational content.

`RuntimeMessage` is operational evidence, not an instruction source. Its
framework or adapter provenance does not give its text system/developer
precedence. A provider adapter that lacks a runtime role may project it into an
explicitly tagged non-instruction-bearing representation or reject the request;
it MUST NOT map it to a system or developer role when that mapping elevates the
notice's trust or instruction priority. History processors and repair policies
are bound by the same non-elevation rule.

### Content-part support values

`ContentPart` variants above reference several narrow, independently owned value
types. Each stays minimal until its owning component (tools, providers) promotes
richer behavior; message storage only needs stable identity and loss-aware
round-tripping.

```csharp
namespace AgentKit;

public enum TextSemantics
{
    Plain,
    Markdown,
    Code
}

public enum MediaSemantics
{
    Input,
    Output,
    Thumbnail
}

public readonly record struct MediaId(Guid Value);

public enum MediaSourceKind
{
    InlineBytes,
    Uri,
    FileReference
}

public sealed record MediaReference(
    MediaId Id,
    MediaSourceKind SourceKind,
    string MediaType,
    Uri? Uri,
    ImmutableArray<byte> InlineBytes,
    long? SizeInBytes,
    ContentHash? Hash,
    ExtensionData Extensions);

public enum ReasoningVisibility
{
    Visible,
    Redacted,
    EncryptedSignature
}

public sealed record ReasoningContent(
    string? Text,
    ReasoningVisibility Visibility,
    string? SignatureToken,
    ExtensionData Extensions);

public sealed record ToolReference(
    ToolAlias ProviderAlias,
    ToolId? Id,
    ToolVersion? Version);

public enum ToolCallOutcomeKind
{
    Success,
    Failed,
    Rejected,
    Cancelled
}

public sealed record ToolCallOutcome(
    ToolCallOutcomeKind Kind,
    ToolTerminalStatus SourceStatus,
    SideEffectCertainty SideEffectCertainty,
    bool Retryable,
    string? FailureReason,
    ExtensionData Extensions);

public enum ToolResultProjectionLoss
{
    Redacted,
    Normalized,
    Summarized,
    Truncated,
    Externalized,
    StatusCoarsened
}

public sealed record ToolResultProjectionInfo(
    ToolResultProjectionPolicyReference Policy,
    ImmutableArray<ToolResultProjectionLoss> Losses,
    long OmittedBytes,
    int OmittedParts);

public sealed record JsonSchemaReference(
    string Name,
    SchemaVersion Version);
```

`ToolId` and `ToolVersion` are the canonical typed values from
[tools](tools.md#normative-minimal-contract-shape); the message model reuses
them rather than redefining tool identity. `ToolReference.ProviderAlias` is
always present; `Id` and `Version` are both present only after successful
resolution. Construction rejects a version without an ID, an ID without a
version, or any unresolved reference that claims canonical identity.
`MediaReference.InlineBytes` is empty unless `SourceKind` is `InlineBytes`; a
`Uri`-sourced or file-referenced medium never inlines its bytes into a durable
message merely because it was observed once.

`ToolResultPart` is the bounded durable/model-facing projection of the
authoritative `ToolCallResult` recorded by the tool runtime. It preserves the
source terminal status, side-effect certainty, retryability, requested alias,
exact tool/version when resolution succeeded, and correlation to the one
terminal record for that call even when `ToolCallOutcomeKind` or the selected
provider protocol is coarser. Projection loss is explicit and ordered; omitted
byte/part counts are zero when nothing was omitted. Authorized artifact or
continuation references appear as typed content and remain covered by the same
projection provenance.

`Policy` is the same immutable key/version reference captured in the accepted
call and authoritative terminal result. Its referenced snapshot owns the exact
bounds and transformation rules, so a publication retry either uses that version
or returns a typed unavailable result; it never silently uses the current
policy.

`Success` maps only from an authoritative successful terminal status.
Pre-invocation unknown, invalid, unsupported, denied, or approval-rejected calls
map to `Rejected`; attempted failures and result-processing failures map to
`Failed`; cancellation and interruption map to `Cancelled`. Unknown future
statuses map fail-closed to `Failed` with `StatusCoarsened`. The exact
`SourceStatus` and `SideEffectCertainty` remain available, so a coarse portable
kind never claims that an uncertain effect did not occur. Text is never parsed
to derive outcome.

Construction validates the closed status mapping, success/error consistency,
non-negative omitted counts, initialized loss collections, and agreement between
loss markers and omitted content. A projection cannot claim lossless status or
content while carrying a coarsened status or non-zero omitted count.

`ToolResultProjectionInfo` preserves the order and repetition of defined loss
markers. A positive omitted byte or part count requires at least one
content-loss marker; `StatusCoarsened` alone accounts for no omitted content. A
content transformation may retain byte and part counts, so a loss marker with
zero omitted counts is valid. These are measured projection counts, separate
from earlier terminal-normalization omissions. The value validates local
evidence; the projector remains responsible for measuring content, enforcing
bounds, and checking the captured policy's allowed transformations.

The projection cannot be used to recreate authorization, usage, diagnostics, or
other fields omitted from the terminal record. If history materialization must
be retried, the session/tool coordinator reprojects from the already recorded
`ToolCallResult`; it never invokes the tool again.

The base records preserve closed invariants while derived records preserve
semantics. They are not an invitation to serialize arbitrary CLR objects.
Unknown provider content has a bounded JSON-compatible representation and stable
type name, so a component that cannot interpret it can retain it without
claiming support. Provider call/response IDs remain external correlation; local
`MessageId`, `ToolCallId`, and `ModelRequestId` remain authoritative.

Message timestamps come from the injected `TimeProvider` at the committing
boundary. Message, content-part, metadata, and extension collections are deeply
immutable and safe for concurrent readers. Media buffers and authorized object
references document ownership explicitly; disposing a provider stream cannot
invalidate already committed message content.

Extension values own bounded canonical JSON bytes. They never retain a
caller-owned buffer, mutable JSON node, or disposable document, and duplicate
keys are rejected before constructing the immutable dictionary. Typed portable
fields remain first-class members; extension data preserves only genuinely
provider-specific or forward-compatible content.

## Normative minimal history contracts

The session component owns reads and appends. The history pipeline owns
validation and construction of a provider-facing view without mutating the
source snapshot.

```csharp
namespace AgentKit;

public sealed record MessageCursor(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    SessionVersion Version,
    SessionSequence Sequence);

public sealed record HistorySnapshot(
    MessageCursor Cursor,
    ImmutableArray<AgentMessage> Messages);

public sealed record HistoryPreparationRequest(
    AgentDefinition Agent,
    SessionId SessionId,
    ConversationId? ConversationId,
    ExecutionIdentity Identity,
    RunId RunId,
    TurnId TurnId,
    MessageCursor Cursor,
    ModelDescriptor TargetModel,
    SecurityAuthorizationContext Authorization,
    EffectiveConfigurationSnapshot Configuration);

public enum HistoryRepairKind
{
    NormalizedContent,
    SettledInterruptedToolCall,
    ExcludedIncompleteAssistantContent,
    DegradedReasoning,
    OmittedReasoning,
    RelocatedMedia,
    RemovedProviderMetadata,
    MergedAdjacentUserContent,
    NormalizedToolCallIdentity,
    ProjectedImportedOrphanToolCall
}

public sealed record HistoryRepair(
    ImmutableArray<MessageId> SourceMessageIds,
    HistoryRepairKind Kind,
    string Reason,
    ExtensionData Diagnostics);

public sealed record HistoryView(
    MessageCursor SourceCursor,
    ImmutableArray<AgentMessage> Messages,
    ImmutableArray<HistoryRepair> Repairs);

public abstract record HistoryPreparationResult;

public sealed record PreparedHistory(HistoryView View)
    : HistoryPreparationResult;

public sealed record RejectedHistory(HistoryFailure Failure)
    : HistoryPreparationResult;

public interface IHistoryPipeline
{
    Task<HistoryPreparationResult> PrepareAsync(
        HistoryPreparationRequest request,
        SessionExecutionCapability session,
        HookDispatchContext hooks,
        CancellationToken cancellationToken = default);
}

public sealed record HistoryProcessorContext(
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization,
    ModelDescriptor TargetModel,
    EffectiveConfigurationSnapshot Configuration);

public sealed record HistoryValidationContext(
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization,
    ModelDescriptor TargetModel,
    EffectiveConfigurationSnapshot Configuration);

public interface IHistoryProcessor
{
    ValueTask<HistoryView> ProcessAsync(
        HistoryView input,
        HistoryProcessorContext context,
        CancellationToken cancellationToken = default);
}

public interface IHistoryValidator
{
    ValueTask<HistoryValidationResult> ValidateAsync(
        HistoryView view,
        HistoryValidationContext context,
        CancellationToken cancellationToken = default);
}
```

`HistoryRepairKind` names the repair operation; numeric order is not policy.
Repair source IDs are initialized, nonempty, nondefault, and unique. Every
message in a `HistoryView` has the source cursor's exact agent, session, and
conversation coordinates. A child-branch view may retain messages carrying their
parent-branch provenance; the session snapshot establishes membership in the
selected branch. Repairs and views compare immutable arrays deeply and in order,
and reject default arrays.

`IHistoryPipeline` returns `Task` because loading authorization data and
validating media references may be inherently asynchronous. Individual
processors and validators return `ValueTask` because deterministic in-memory
paths commonly complete synchronously. Cancellation never publishes a partial
repaired view as valid.

`AgentKit.Context` supplies the first-party `DefaultHistoryPipeline`; there is
still no messages implementation package. Its constructor dependencies are
explicit:

```csharp
namespace AgentKit.Context;

internal sealed class DefaultHistoryPipeline(
    IHistoryValidator validator,
    IEnumerable<IHistoryProcessor> processors,
    IHistoryRepairPolicy repairPolicy,
    IHookDispatcher hookDispatcher,
    ILogger<DefaultHistoryPipeline> logger) : IHistoryPipeline
{
}
```

The body is intentionally omitted from this constructor/dependency shape; its
observable members are exactly the `IHistoryPipeline` contract above. It does
not expose extra resolution or mutation APIs.

The pipeline is run-scoped. The facade compiles `SessionExecutionCapability`
from the run's selected immutable `SessionProfileSnapshot`. It passes that same
capability to context history preparation and compaction; the record is an
invocation-only service bundle, is never serialized, and does not own or dispose
either coordinator. `DefaultHistoryPipeline` never injects an unkeyed
`ISessionCoordinator`, resolves keyed services, or receives `IServiceProvider`.
The active `HookDispatchContext` is a separate invocation-only argument; the
pipeline dispatches through its injected dispatcher but never reselects a hook
profile or embeds the lease in history values.

Construction and invocation validate that the request session and agent match
the cursor and selected capability; that the profile key, version, coordinator
keys, and configuration fingerprint match the compiled run plan; and that
`request.Identity == request.Authorization.Identity`. Validators and processors
receive that same immutable identity and authorization snapshot; they cannot
substitute a tenant/principal projection or an ambient principal.

Validators and processors may be singleton only when documented thread-safe and
stateless; run-dependent implementations are scoped. Ordered processors execute
sequentially against immutable values so later processors observe earlier
output. Cache keys include the complete execution identity and authorization
snapshot, session version, processor catalog version, configuration snapshot,
and target compatibility profile. No cache may cross either boundary.

## DI registration and validation

History validation is singular per selected history-pipeline key. Processors are
additive, stably identified, and deterministically ordered. The agent's context
profile selects the pipeline and processor set; run options may disable only
processors declared optional.

```csharp
namespace AgentKit.Context;

// Excerpt from this package's ServiceExtensions.cs.
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHistoryProcessor<TProcessor>(
            ComponentKey<IHistoryPipeline> pipeline,
            HistoryProcessorRegistration registration)
            where TProcessor : class, IHistoryProcessor =>
            HistoryRegistration.AddProcessor<TProcessor>(
                services,
                pipeline,
                registration);

        public IServiceCollection ReplaceHistoryValidator<TValidator>(
            ComponentKey<IHistoryPipeline> pipeline)
            where TValidator : class, IHistoryValidator =>
            HistoryRegistration.ReplaceValidator<TValidator>(services, pipeline);

        public IServiceCollection ReplaceHistoryRepairPolicy<TPolicy>(
            ComponentKey<IHistoryPipeline> pipeline)
            where TPolicy : class, IHistoryRepairPolicy =>
            HistoryRegistration.ReplaceRepairPolicy<TPolicy>(services, pipeline);
    }
}
```

Repeated equivalent processor registrations are idempotent; duplicate stable
identity with different type, order, or options fails build. Composition also
rejects order cycles, singleton-to-scoped captures, missing validators, and an
agent history profile incompatible with its selected model policy.

Unsupported schema major versions, structurally corrupt local history,
unauthorized references, invalid tool pairing, and unsafe provider affinity are
typed `HistoryFailure` values. The configured policy may produce an observable
loss-aware repair only where the source truth supports it. It can never forge
authority, approval, completed output, or tool success.

## Related concept specifications

- [Message and content model](../concepts/message-and-content-model.md)
- [History validation and repair](../concepts/history-validation-and-repair.md)
- [Context compaction](../concepts/context-compaction.md)
