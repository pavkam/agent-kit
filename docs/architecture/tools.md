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

Individual tools are feature packages named AgentKit.Tools.ToolName. Workspace
observation is split into AgentKit.Tools.Read, AgentKit.Tools.List,
AgentKit.Tools.Glob, and AgentKit.Tools.Search; mutation uses
AgentKit.Tools.Write, AgentKit.Tools.Edit, and AgentKit.Tools.Patch. Skill, web,
process, language-service, session-state, and delegation tools follow the same
naming rule. Each package owns its descriptor, invoker, options, registration,
and tests. Registration methods such as AddReadTool and AddListTool add only
their feature and never grant its effects.

`AgentKit.Tools.Search` validates the complete search profile before requesting
authority, including the pinned pattern engine, path glob, hidden-file policy,
and every resource/output bound. It authorizes `FileSearch` against the narrow
host audience and passes the resulting single-use grant to
`IFileContentSearcher`; the low-level host consumes the same fingerprint before
observing path names or content. Invalid patterns and denied requests therefore
cannot leak base existence, sibling names, file types, or match data.

`AgentKit.Tools.Edit` performs exact ordinal text replacement over a strict
UTF-8 byte snapshot. A normal edit requires exactly one occurrence; explicit
`replace_all` replaces every non-overlapping occurrence, while zero or ambiguous
matches fail before mutation authorization. Untouched bytes, a UTF-8 BOM,
newline spelling, and final-newline state are preserved because the complete
final bytes are computed before the second security decision. The mutation
decision binds those bytes and the observed source hash, and the host rejects a
concurrent version change instead of overwriting it under stale authority.

`AgentKit.Tools.Patch` parses its pinned patch grammar without effects, resolves
the complete source-ordered plan against exact snapshots, and obtains separate
authority for every create, replace, delete, and move. It depends on
`IWorkspacePatchApplier`, never on `AgentKit.FileSystem`, and passes exact final
bytes plus source-version fingerprints to the host boundary. Duplicate or
overlapping paths fail before authorization. Patch results preserve the host's
operation-level atomicity classification and every per-entry committed,
unchanged, or uncertain state; a sequential multi-file commit is never presented
as atomic.

`AgentKit.Tools.Language` is a thin read-only adapter over the selected
`ILanguageIntelligenceService`. It validates document and workspace-symbol query
shapes before authority, converts the model-facing one-based positions to
zero-based UTF-16 coordinates, and requests exact `FileRead`/`Observe` authority
for the document or workspace resource. The host implementation consumes the
same single-use grant before observation. Results preserve successful-empty,
unsupported, unavailable, stale, timeout, cancellation, and failure states;
model projection re-applies item and text bounds even when an adapter returns an
oversized snapshot.

`AgentKit.Tools.Web` supplies the local `web_fetch` tool. It accepts only
absolute HTTP(S) URLs without user information or fragments and never accepts
model-provided credential headers. For every hop it first requests authority for
the resolver audience, consumes that grant before DNS, then requests a second
grant bound to the exact method, routed destination, resolved addresses,
headers, classification, and bounds. Redirects are returned unfollowed by the
transport and repeat this full sequence with a fresh operation identity and two
fresh single-use grants. Response bytes, headers, total duration, redirects,
character decoding, and final projection are independently bounded. HTML uses a
deterministic executable-region-removing transform, and every projection marks
remote content as untrusted data.

`AgentKit.Tools.WebSearch` supplies `web_search` over one explicitly selected
`IWebSearchProvider`; registration deliberately supplies no provider, endpoint,
credential, or synthetic result source. Query text, ordered canonical domain
filters, freshness, result bound, provider identity, secret-free destination,
attempt ID, and deadline form one exact `Network`/`Egress` fingerprint. A
provider implementation consumes that single-use grant before sending the
classified query and performs one attempt without hidden retry. The tool
revalidates request correlation, HTTP(S) result URLs, domain filters, item
count, title/snippet bounds, and provider result kinds. Truncation clears
completeness, and every projection marks titles, URLs, and snippets as
non-authoritative untrusted data.

`AgentKit.Tools.Resource` supplies a stable `resource` list/read tool over an
immutable host-configured file-resource catalog. Listing returns only approved
identity, kind, trust, description, media, and integrity metadata; backing paths
remain private and listing performs no discovery. Reading resolves the identity
before authority, obtains exact `FileRead`/`Observe` authority for the
configured path and complete-file byte bound, then calls `IFileSnapshotReader`,
which consumes the same single-use grant. The tool requires strict UTF-8,
verifies an optional pinned content hash, independently bounds decoded
characters, and marks all loaded text `instruction_authority: false`. Loading
never migrates, installs, executes, or mutates its source.

`AgentKit.Tools.Question` supplies the explicit `question` boundary for a
material decision that cannot be inferred safely. It validates two through ten
unique, mutually exclusive options, prompt and option text ceilings, optional
supplementary free text, and a bounded response timeout before allocating an ID
or asking for authority. Publication requests exact `StateMutation`/`Create`
authority bound to the question ID, full ordered presentation, free-text policy,
and deadline. `AgentKit.IO.DefaultHumanQuestionBroker` consumes that single-use
grant immediately before passing a grant-free prompt to the selected application
channel. The tool returns only after an answer, timeout, unavailable channel, or
cancellation; it never disguises pending work as a successful terminal result.
Answers are checked against the authorized option set and output bounds and are
projected with `instruction_authority: false`.

`AgentKit.Tools.Plan` supplies the versioned `plan` tool and the default
session-backed `IPlanStateStore`. A plan has a stable `PlanId`, positive
optimistic revision, authenticated author, bounded title, and one through fifty
ordered items with stable IDs and explicit pending, in-progress, completed, or
blocked status. At most one item may be in progress. Reads, complete
replacements, and individual status transitions have distinct exact
fingerprints; mutations require the revision previously observed by the model.
The tool obtains `StateRead`/`Observe` or `StateMutation`/`Mutate` authority,
and `SessionPlanStateStore` consumes that single-use grant before loading or
appending session state. Every accepted mutation appends a complete typed
`PlanSessionEntry`; it never rewrites an earlier revision. Session optimistic
conflicts are re-read and returned as plan revision conflicts rather than
silently overwriting concurrent work.

The same package also exposes `todo` as an explicit compatibility tool over the
identical `IPlanStateStore`, schema, revisions, grants, and session entries.
Hosts may publish either or both names, but AgentKit never creates a second todo
list beside the work plan merely to imitate another harness's spelling.

`AgentKit.Tools.Task` supplies the model-facing `task` adapter over an
explicitly selected `ITaskDelegationBroker`. It requires a durable parent
session and an active in-run correlation, validates the target agent, objective,
acceptance criteria, exact child tool allow-list, turn/tool-call budgets, and
deadline before allocating identities or requesting authority, and binds every
field to one `Delegation/Create` grant. The result projection preserves child
goal, attempt, session, run, terminal status, and side-effect certainty while
marking the child summary `instruction_authority: false`.

The adapter-specific `TaskDelegation*` contracts deliberately do not replace the
fuller goal coordinator contracts. `AgentKit.Goals` supplies the protected
default broker, which atomically consumes the exact single-use grant immediately
before forwarding a grant-free envelope to an application-selected
`ITaskDelegationChannel`. That channel delegates to the goal coordinator's
durable admission and join contracts: resolve the active parent goal, attenuate
authority and catalogs, reserve budget, and create child state idempotently.
Local execution crosses the
[host-owned worker boundary](goals-and-delegation.md#delegation-discovery-selection-policy-and-execution)
without capturing the engine inside the tool or dispatcher graph. Admitted work
must settle or be durably handed off. Registration supplies no fake channel and
rejection before dispatch contains no invented child identities.

`AgentKit.Tools.Skill` deliberately contributes two capabilities: the `skill`
tool and `ISkillCatalogContextSource`, which renders bounded discovery metadata
without reading skill bodies. Both resolve the same singleton `ISkillCatalog`
snapshot and therefore share one deterministic catalog version and source
identity. Listing performs no file observation. Activation selects by stable ID,
authorizes an exact bounded file snapshot, validates optional integrity and
strict UTF-8, reports truncation explicitly, and marks both discovery and loaded
content `instruction_authority: false`; it never installs, migrates, executes,
or silently elevates repository text. File-aware tools depend on file-system
abstractions and never on AgentKit.FileSystem itself. Web tools depend on
network abstractions and never create an unrestricted HTTP client.
Process-backed tools depend on process abstractions and never start an
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

public readonly record struct ToolsetVersion(long Value);

public readonly record struct ToolExecutionPolicyKey(string Value);

public readonly record struct ToolExecutionPolicyVersion(long Value);

public readonly record struct ToolResultProjectionPolicyKey(string Value);

public readonly record struct ToolResultProjectionPolicyVersion(long Value);

public readonly record struct ToolResultRejectionPolicyKey(string Value);

public readonly record struct ToolResultRejectionPolicyVersion(long Value);

public sealed record ToolResultRejectionPolicyReference(
    ToolResultRejectionPolicyKey Key,
    ToolResultRejectionPolicyVersion Version);

public sealed record ToolResultProjectionPolicyReference(
    ToolResultProjectionPolicyKey Key,
    ToolResultProjectionPolicyVersion Version);

[Flags]
public enum ToolResultProjectionTransformations
{
    None = 0,
    Redaction = 1,
    Normalization = 2,
    Summarization = 4,
    Truncation = 8,
    Externalization = 16
}

public enum ToolResultNormalizationTransformation
{
    Redacted,
    Normalized,
    Summarized,
    Truncated,
    Externalized
}

public enum ToolErrorKind
{
    Tool,
    Transport,
    Host,
    Policy,
    Serialization,
    Protocol
}

public enum ToolUsageMeasurementQuality
{
    Measured,
    Estimated,
    Unknown,
    NotApplicable
}

public sealed record ToolError(
    ToolErrorKind Kind,
    string SafeMessage,
    string? ExternalCode,
    TimeSpan? RetryAfter,
    ExtensionData Extensions);

public sealed record ToolUsageMeasurement(
    BudgetDimension Dimension,
    BudgetUnit Unit,
    BudgetQuantity? Amount,
    ToolUsageMeasurementQuality Quality);

public sealed record ToolUsage(
    ImmutableArray<ToolUsageMeasurement> Measurements,
    ExtensionData Extensions);

public sealed record ToolResultProjectionBounds(
    long MaximumBytes,
    int MaximumParts);

public sealed record ToolResultProjectionPolicySnapshot(
    ToolResultProjectionPolicyReference Reference,
    ToolResultProjectionBounds Bounds,
    ToolResultProjectionTransformations AllowedTransformations,
    ExtensionData Extensions);

public readonly record struct ToolResultNormalizationAlgorithmVersion(long Value);

public sealed record ToolResultBounds(
    long MaximumCanonicalBytes,
    int MaximumParts);

public sealed record ToolResultNormalizationSnapshot(
    ToolResultRejectionPolicyReference RejectionPolicy,
    ToolResultProjectionPolicyReference ProjectionPolicy,
    ToolExecutionPolicyReference? ExecutionPolicy,
    ToolResultNormalizationAlgorithmVersion AlgorithmVersion,
    ToolResultBounds Bounds,
    ToolResultProjectionTransformations AllowedTransformations,
    ExtensionData Extensions);
```

Runtime-generated call and operation identities come from injected
`IIdentifierGenerator<ToolCallId>` and `IIdentifierGenerator<OperationId>`
services. Provider call IDs are correlated values, never substituted for
AgentKit identity.

Projection policy keys are validated non-empty composition keys and versions are
positive, monotonically published values. Policy construction rejects
non-positive bounds, unknown transformation flags, and a snapshot whose
reference collides with different content. Normalization algorithm versions are
positive, its bounds are positive, and its allowed transformations use the same
closed flags. A run captures a nondefault `ToolResultRejectionPolicyReference`
before resolution. That immutable run-level policy supplies normalization bounds
and the exact projection-policy reference/bounds for unknown, ambiguous,
invalid, or otherwise pre-policy rejections. A selected
`ToolExecutionPolicyReference`, when one exists, identifies the immutable
per-tool normalization rules; it is not a second normalizer registry.

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

public sealed record ToolExecutionPolicyReference(
    ToolExecutionPolicyKey Key,
    ToolExecutionPolicyVersion Version);

public sealed record ToolDiscoveryRequest(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    ExecutionIdentity Identity,
    SecurityAuthorizationContext Authorization,
    AgentDefinitionRevision AgentDefinitionRevision,
    EffectiveConfigurationSnapshot Configuration,
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
    ExecutionIdentity Identity,
    SecurityPolicySnapshotReference SecurityPolicy,
    AgentDefinitionRevision AgentDefinitionRevision,
    ConfigurationVersion ConfigurationVersion,
    ToolCatalogVersion Version,
    ImmutableArray<ToolDescriptor> Tools,
    ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>
        ExecutionPolicies,
    ImmutableDictionary<ToolAlias, ToolIdentity> ProviderAliases);
```

Every descriptor carries an exact nondefault tool version and explicit stable
source identity. Its input and optional output use the shared owned `JsonSchema`
value with an explicit `JsonSchemaDialectId`; accepting that value proves only
that the document is representable and owned, not that a selected validator
supports its keywords. Catalog construction and request preflight perform those
capability checks.

`ToolEffects` always carries the defined coarse `ToolEffect`. Its optional
`IdempotencyClassification` and optional resource-kind collection are declared
evidence: null means the publisher made no claim, while an initialized empty
resource collection explicitly declares no protected resource kind. Neither form
grants authority, and missing evidence never implies retry safety.

`ToolExecutionHints` uses the closed `ToolSchedulingMode` vocabulary from the
scheduling contract. `Unspecified` supplies no parallel-safety claim. A
concurrency key is required exactly for `ConcurrencyKey`; expected duration and
approval-cacheability remain nullable when unasserted. Host policy may tighten
every hint and treats absent or unknown evidence conservatively.

This descriptor shape intentionally replaces the earlier reduced public
constructor. `Version` is now required, the borrowed `JsonElement` input schema
is an owned `JsonSchema`, the singular `Effect` property is represented by
`Effects`, and output schema, execution hints, and source identity are explicit.
Callers migrate by supplying their real published version, package or
registration source, and dialect-bound schema; no compatibility overload fills
those facts with defaults.

The snapshot is bound to one run. A provider alias maps to one exact
`ToolId`/`ToolVersion` in that snapshot and is not resolved against live DI
registrations later. Its per-tool execution-policy map preserves the originating
toolset's exact policy key/version after several toolsets are merged; resolution
copies that reference through validation and planning.

Discovery validates that `Identity`, `Authorization.Identity`, agent-definition
revision, and effective configuration agree before contacting any static,
dynamic, remote, or MCP provider. Principal-specific exposure and discovery
effects use the configured security authority. Catalog and provider caches
include the complete identity fingerprint plus authority, policy, definition,
configuration, tool-source, and model-capability versions; they never cross one
of those boundaries.

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
    ToolExecutionPolicyReference ExecutionPolicy,
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
    ToolExecutionPolicyReference ExecutionPolicy,
    int SourceOrdinal,
    JsonElement Arguments,
    InputFingerprint InputFingerprint,
    DateTimeOffset RequestedAt,
    DateTimeOffset ValidatedAt);

public sealed record PreparedToolCall(
    ValidatedToolCall Call,
    ToolExecutionPlan ExecutionPlan);

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

public sealed record ToolCallAdmissionEvidence(
    ToolCatalogVersion CatalogVersion,
    int SourceOrdinal,
    InputFingerprint RawArgumentsFingerprint);

public sealed record ToolCallAcceptanceEvidence(
    GrantId InvocationGrantId,
    InputFingerprint ValidatedArgumentsFingerprint,
    DateTimeOffset AcceptedAt);

public sealed record ToolResultNormalizationInfo(
    ImmutableArray<ToolResultNormalizationTransformation> Transformations,
    long? InputCanonicalBytes,
    int? InputParts,
    long? OmittedCanonicalBytes,
    int? OmittedParts,
    ExtensionData Extensions);

public sealed record AcceptedToolCall(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    SecurityAuthorizationContext Authorization,
    ToolCallAcceptanceEvidence Acceptance,
    ToolAlias ProviderAlias,
    ToolId ToolId,
    ToolVersion ToolVersion,
    ToolEffects Effects,
    IdempotencyKey? ExternalIdempotencyKey,
    ToolCallAdmissionEvidence Admission,
    ToolResultNormalizationSnapshot Normalization,
    ToolResultProjectionPolicyReference ProjectionPolicy,
    DateTimeOffset RequestedAt);

public sealed record ToolCallResult(
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    TurnId TurnId,
    OperationId OperationId,
    ToolCallId CallId,
    SecurityAuthorizationContext Authorization,
    GrantId? GrantId,
    ToolCallAcceptanceEvidence? Acceptance,
    ToolAlias ProviderAlias,
    ToolId? ToolId,
    ToolVersion? ToolVersion,
    ToolEffects? Effects,
    IdempotencyKey? ExternalIdempotencyKey,
    ToolCallAdmissionEvidence Admission,
    ToolTerminalStatus Status,
    ImmutableArray<ToolResultContent> Content,
    ToolError? Error,
    SideEffectCertainty SideEffectCertainty,
    ToolUsage? Usage,
    bool Retryable,
    ToolResultNormalizationSnapshot Normalization,
    ToolResultNormalizationInfo NormalizationInfo,
    ToolResultProjectionPolicyReference ProjectionPolicy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? InvocationStartedAt,
    DateTimeOffset CompletedAt,
    ExtensionData Extensions);
```

`AcceptedToolCall` is the durable invocation-acceptance fact. It exists only
after resolution, semantic argument validation, planning, and authorization
establish the resolved identity, declared effects, retry mechanism reference,
and normalization policy. The executor records it before it invokes an effect.
`IToolCallRecorder.RecordAcceptedAsync` must succeed before `IToolInvoker`
begins. Deferral is not acceptance until this record exists.

`ToolCallResult.Status` is authoritative, numeric, and forward-compatible. Known
status values cover successful invocation, rejection before invocation, denial
or approval expiry, invocation failure, timeout, cancellation, interruption,
result normalization failure, result serialization failure, and protocol
failure. An unrecognized numeric value is retained exactly in the terminal
record; no CLR enum validation rewrites it. A projector that cannot represent it
maps it to portable `Failed`, retains the original numeric source value, and
records `StatusCoarsened` in `ToolResultProjectionInfo.Losses`.

Construction validates local structural facts only: a requested alias and
admission evidence are present; a resolved `ToolId` and `ToolVersion` occur
together; effects are present only for a resolved descriptor; a terminal result
without acceptance evidence has no invocation start; and a successful result has
resolved identity, acceptance evidence, an invocation start, and no error. When
acceptance evidence is present, `GrantId` equals its `InvocationGrantId`. The
supplied authorization is historical evidence for this exact
agent/session/run/turn/operation correlation. It does not reauthorize, inspect a
current grant, infer execution from content, or establish that an idempotency
mechanism was actually enforced. When acceptance evidence is present, the
recorder loads that accepted record and checks exact identity, effect,
idempotency-key, admission, acceptance/grant, and captured-policy coherence. UTC
timestamp comparison is not a stage-order check because clocks can move
backward; durable transitions establish chronology.

`Effects` and `ExternalIdempotencyKey` preserve declared facts whenever a
descriptor resolved; both remain absent for an unresolved alias. The captured
run-level rejection policy, rather than a current/default per-tool policy,
governs the normalization and projection bounds in that case. Retrying a
possibly-started mutating call is structurally consistent only with `Idempotent`
or `IdempotentWithKey` declared effects; the latter requires its exact nonempty
external key. The executor separately verifies that the chosen invoker/host will
enforce that mechanism before the retry starts. Descriptor declarations alone
never prove replay safety. A call known not to have started may be retried under
ordinary retry policy.

`ToolCallResult` is the complete authoritative terminal record, not message
content. It retains terminal status, historical authorization/acceptance-grant
correlation, side-effect certainty, normalized typed content, safe error,
optional usage, retry decision, normalization provenance, and policy evidence.
The runtime records it before projecting a `ToolResultPart`; a recording retry
is never an invocation retry. The duplicate `ProjectionPolicy` field on both
accepted and terminal records is required to match
`Normalization.ProjectionPolicy`, so stores and message projection can index the
retained reference directly.

`Normalization` is the immutable normalization snapshot captured at admission.
It contains the always-present captured run-level rejection and projection
policies, an optional selected per-tool execution-policy reference,
normalization algorithm revision, result bounds, and allowed transformations.
The rejection policy makes pre-resolution rejections representable without a
current/default lookup. The execution-policy reference is the existing selection
identity when resolution succeeds: its retained immutable snapshot includes
normalization rules and does not create a second normalizer catalog. The
normalizer owns canonical retained-content encoding and checks aggregate byte
bounds before it constructs a terminal result. Constructors validate content
shape and captured limits but do not claim to measure aggregate content bytes
independently of that canonical encoder.

The durable `ToolResultPart` is a distinct, tighter, loss-aware history/model
projection. It retains call identity, requested alias, resolved identity when
present, source status, coarse outcome, side-effect certainty, retryability,
safe correction detail, and projection provenance. It records every redaction,
normalization, summary, truncation, omitted part/byte count, and authorized
artifact or continuation replacement. Projection occurs only after terminal
recording; retrying a failed append reprojects the record and never repeats the
effect.

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
    ToolExecutionPolicyReference Reference { get; }

    ValueTask<ToolExecutionPlanResult> PlanAsync(
        ImmutableArray<ValidatedToolCall> calls,
        ToolExecutionPolicyContext context,
        CancellationToken cancellationToken);
}

public interface IToolExecutionPolicySelector
{
    ValueTask<ToolExecutionPolicySelectionResult> SelectAsync(
        ToolExecutionPolicyReference reference,
        ToolExecutionCapability capability,
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
        ToolExecutionCapability capability,
        HookDispatchContext hooks,
        CancellationToken cancellationToken);
}

public sealed record ToolExecutionCapability(
    SessionExecutionCapability Session,
    BudgetExecutionCapability Budget,
    ImmutableArray<ToolExecutionPolicyBinding> ExecutionPolicies);

public sealed record ToolExecutionPolicyBinding(
    ToolExecutionPolicyReference Reference,
    IToolExecutionPolicy Policy);

public interface IToolCallRecorder
{
    ValueTask<ToolCallRecordResult> RecordAcceptedAsync(
        AcceptedToolCall call,
        SessionExecutionCapability session,
        CancellationToken cancellationToken);

    ValueTask<ToolCallRecordResult> RecordTerminalAsync(
        ToolCallResult result,
        SessionExecutionCapability session,
        CancellationToken cancellationToken);
}

public interface IToolResultProjectionPolicyCatalog
{
    ValueTask<ToolResultProjectionPolicyResolution> ResolveAsync(
        ToolResultProjectionPolicyReference reference,
        CancellationToken cancellationToken);
}

public interface IToolResultProjector
{
    ValueTask<ToolResultProjectionResult> ProjectAsync(
        ToolCallResult result,
        ToolResultProjectionPolicySnapshot policy,
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
`IToolResultProjectionPolicyCatalog` resolves the exact retained snapshot named
by the terminal record; `IToolResultProjector` deterministically creates the
bounded message value and never performs or retries the tool effect. The loop
depends only on `IToolExecutor`.

`ToolExecutionCapability` is invocation-only and binds execution to the run's
exact session profile/coordinators and budget profile/scope. The executor
validates both bindings and every catalog policy reference against its exact
policy bindings before preflight. `ValidatedToolCall` contains validated data
and the policy reference, never a plan that has not been produced yet; a
successful policy decision creates `PreparedToolCall`. The recorder receives the
selected session capability on every write and never injects an unkeyed session
coordinator. The executor reserves attempted, concurrent, retry, result-byte,
and successful-call dimensions before their corresponding work and settles each
reservation exactly once.

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
    IToolResultProjectionPolicyCatalog projectionPolicies,
    IToolResultProjector resultProjector,
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

public sealed class ToolsetOptions
{
    public ToolsetVersion Version { get; set; } = new(1);
    public ToolExecutionPolicyVersion ExecutionPolicyVersion { get; set; } =
        new(1);
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

        public IServiceCollection ReplaceToolset(
            ToolsetKey key,
            Action<ToolsetOptions> configure) =>
            ToolServiceRegistration.ReplaceToolset(services, key, configure);

        public IServiceCollection AddToolProvider<TProvider>(
            ToolSourceId sourceId)
            where TProvider : class, IToolProvider =>
            ToolServiceRegistration.AddToolProvider<TProvider>(
                services,
                sourceId);

        public IServiceCollection ReplaceToolProvider<TProvider>(
            ToolSourceId sourceId)
            where TProvider : class, IToolProvider =>
            ToolServiceRegistration.ReplaceToolProvider<TProvider>(
                services,
                sourceId);

        public IServiceCollection AddToolExecutionPolicy<TPolicy>(
            ToolExecutionPolicyReference reference)
            where TPolicy : class, IToolExecutionPolicy =>
            ToolServiceRegistration.AddExecutionPolicy<TPolicy>(
                services,
                reference);

        public IServiceCollection ReplaceToolExecutionPolicy<TPolicy>(
            ToolExecutionPolicyReference reference)
            where TPolicy : class, IToolExecutionPolicy =>
            ToolServiceRegistration.ReplaceExecutionPolicy<TPolicy>(
                services,
                reference);

        public IServiceCollection AddTool<TInvoker>(
            ToolDescriptor descriptor,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TInvoker : class, IToolInvoker =>
            ToolServiceRegistration.AddTool<TInvoker>(
                services,
                descriptor,
                lifetime);

        public IServiceCollection ReplaceTool<TInvoker>(
            ToolDescriptor descriptor,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TInvoker : class, IToolInvoker =>
            ToolServiceRegistration.ReplaceTool<TInvoker>(
                services,
                descriptor,
                lifetime);

        public IServiceCollection AddToolEventSink<TSink>(
            ToolEventSinkRegistration registration)
            where TSink : class, IToolEventSink =>
            ToolServiceRegistration.AddToolEventSink<TSink>(
                services,
                registration);

        public IServiceCollection ReplaceToolCatalog<TCatalog>()
            where TCatalog : class, IToolCatalog =>
            ToolServiceRegistration.ReplaceToolCatalog<TCatalog>(services);

        public IServiceCollection ReplaceToolResolver<TResolver>()
            where TResolver : class, IToolResolver =>
            ToolServiceRegistration.ReplaceToolResolver<TResolver>(services);

        public IServiceCollection
            ReplaceToolArgumentValidator<TValidator>()
            where TValidator : class, IToolArgumentValidator =>
            ToolServiceRegistration.ReplaceToolArgumentValidator<TValidator>(
                services);

        public IServiceCollection
            ReplaceToolExecutionPolicySelector<TSelector>()
            where TSelector : class, IToolExecutionPolicySelector =>
            ToolServiceRegistration
                .ReplaceToolExecutionPolicySelector<TSelector>(services);

        public IServiceCollection ReplaceToolScheduler<TScheduler>()
            where TScheduler : class, IToolScheduler =>
            ToolServiceRegistration.ReplaceToolScheduler<TScheduler>(services);

        public IServiceCollection
            ReplaceToolResultNormalizer<TNormalizer>()
            where TNormalizer : class, IToolResultNormalizer =>
            ToolServiceRegistration.ReplaceToolResultNormalizer<TNormalizer>(
                services);

        public IServiceCollection ReplaceToolExecutor<TExecutor>(
            ComponentKey<IToolExecutor> key)
            where TExecutor : class, IToolExecutor =>
            ToolServiceRegistration.ReplaceToolExecutor<TExecutor>(services, key);

        public IServiceCollection AddToolCallRecorder<TRecorder>(
            ComponentKey<IToolExecutor> executor)
            where TRecorder : class, IToolCallRecorder =>
            ToolServiceRegistration.AddRecorder<TRecorder>(services, executor);

        public IServiceCollection ReplaceToolCallRecorder<TRecorder>(
            ComponentKey<IToolExecutor> executor)
            where TRecorder : class, IToolCallRecorder =>
            ToolServiceRegistration.ReplaceRecorder<TRecorder>(
                services,
                executor);

        public IServiceCollection AddToolResultProjectionPolicy(
            ToolResultProjectionPolicySnapshot policy) =>
            ToolServiceRegistration.AddProjectionPolicy(services, policy);

        public IServiceCollection ReplaceToolResultProjectionPolicy(
            ToolResultProjectionPolicySnapshot policy) =>
            ToolServiceRegistration.ReplaceProjectionPolicy(services, policy);

        public IServiceCollection
            ReplaceToolResultProjectionPolicyCatalog<TCatalog>()
            where TCatalog : class, IToolResultProjectionPolicyCatalog =>
            ToolServiceRegistration
                .ReplaceProjectionPolicyCatalog<TCatalog>(services);

        public IServiceCollection ReplaceToolResultProjector<TProjector>()
            where TProjector : class, IToolResultProjector =>
            ToolServiceRegistration.ReplaceResultProjector<TProjector>(services);
    }
}
```

The package-internal `ToolServiceRegistration` helper carries the generic
constraints and performs registration without building or resolving a provider.

`AddAgentTools` is idempotent and uses `TryAdd` for the engine-wide registration
catalog, resolver, argument validator, policy selector, scheduler, and
normalizer plus the singular projection-policy catalog and projector. It uses
`TryAddKeyed` for each executor selected by `ComponentKey<IToolExecutor>`.
Executor and recorder replacement methods target one executor key. The default
recorder is scoped, depends only on session abstractions, and uses the
invocation's selected session capability; neither it nor the executor captures a
session or budget scope. Replacement methods for the other singular axes are
explicit. Tool providers, event sinks, policy contributors, projection-policy
snapshots, and tool registrations are additive. A duplicate
`ToolId`/`ToolVersion`, provider source identity, or provider-visible alias is a
build error unless an explicit replacement API names the exact registration
being replaced.

Projection is deterministic and effect-free. Summarization uses only the bounded
recorded result and captured rules; it does not call a model or tool. An
externalization projection may select an authorized artifact reference that
terminal normalization already recorded, but cannot create a new artifact after
the terminal record is committed.

Named toolsets and execution-policy strategies are keyed registrations. Agent
definitions refer to typed toolset and policy keys; runtime components receive
catalogs and selectors, not keyed-container access. The compiled
`ToolExecutionCapability` holds the exact policy instances referenced by the
versioned catalog; a selector only validates and returns one of those bindings.
It never resolves a newer policy or uses registration order. `AddReadTool`,
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
by session state, a deterministic result projector, and a resolvable captured
terminal-result projection policy with bounded history/model output, the shared
hook dispatcher, `TimeProvider`, and required ID generators. It also validates
profile references, schema dialects, aliases, positive projection bounds,
retained policy versions, loss-aware status mappings, retry safety, tool
lifetimes, ordering constraints, and feature dependencies such as file, network,
or process boundaries.

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
invocation, result normalization, authoritative terminal recording, bounded
`ToolResultPart` projection, and source-order history materialization. No
invocation begins until the accepted call has been recorded. No message
projection is authoritative over its terminal record. Invalid, denied, unknown,
or ambiguous calls produce no side effect, but every bounded request with a
stable call identity still receives one rejected terminal record and correlated
projection.

The invoker receives only the validated arguments, approved resource scope,
identity, deadline, cancellation, attempt data, safe dependencies, and a bounded
progress channel. It does not receive the loop, arbitrary history, credentials,
or the dependency container.

## First-party file observation and mutation tools

`AgentKit.Tools.Read`, `.List`, `.Glob`, `.Search`, `.Write`, `.Edit`, and
`.Patch` are independent feature packages. A convenience registration may add
several, but it does not own their tool types, merge their descriptors, or
replace their distinct dependency and capability validation. A combined
`AgentKit.Tools.FileSystem` package is not the architectural package boundary.

The read tool exposes a model-facing line window while depending only on
`IFileReader`. Its offset is a one-based logical line number; omission means the
first line. Its limit is a positive maximum returned-line count; omission uses a
configured finite window rather than meaning unbounded. Zero or negative offset
or limit values are invalid. An empty file has zero lines, and a terminal
newline terminates the preceding line without creating a phantom extra line. An
offset beyond the last logical line returns a successful empty window marked at
end-of-file, rather than not-found or an invented blank line.

The tool decodes and scans the authorized host stream incrementally. It never
reads the whole file and then applies the requested window. Returned text uses
the selected declared text profile; the first-party portable projection
recognizes CRLF, LF, and a lone CR as one logical delimiter, normalizes them to
`\n`, preserves whether the selected source range ended in a delimiter, and
never synthesizes a final newline. Its structured result reports requested and
actual start, returned lines and bytes, whether verified EOF was reached, and
every truncation cause (line limit, host-byte limit, result budget, or decoding
boundary). Whether the complete source ended with a newline is reported only
when EOF was verified; otherwise that value is unknown rather than false.

A truncated read carries a continuation bound to the normalized target,
snapshot/version evidence, decoding profile, prior window, next byte position,
and next one-based line offset. A changed target produces conflict rather than
continuing across versions. Reaching a byte or result bound cannot be described
as EOF. The file-system layer owns byte enforcement and target evidence; line
windowing remains here at the model-facing tool layer.

The write tool requires an explicit `CreateOnly`, `ReplaceExisting`,
`CreateOrReplace`, or `Append` disposition and has no overwrite default.
`CreateOrReplace` must be deliberately selected and authorized for both target
states. Empty and whitespace-only content remain valid string payloads. The tool
declares its text encoding, BOM, and newline profile, passes exact bytes and
fingerprints to `IFileWriter`, and projects the host's `Created`, `Replaced`, or
`Appended` outcome with payload, previous, and final sizes plus the committed
final fingerprint. Parent-directory creation is requested as a separate
protected operation; the tool never hides it inside a file write.

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

The authoritative terminal result contains a numeric exact status, typed
normalized content, safe errors, optional usage, retryability, and side-effect
certainty. A captured immutable normalization snapshot names the selected
execution-policy reference, algorithm revision, bounds, and allowed
transformations. Its normalizer, not an arbitrary result constructor, owns
canonical retained-content encoding and aggregate byte enforcement. Oversized
content is rejected, transformed, or stored behind authorized
[artifact references](artifacts.md) according to that snapshot. The normalizer
coordinates artifact creation and the `IToolCallRecorder` terminal commit; the
artifact component never calls back into the tool recorder. A separate
deterministic projection then creates the tighter model/history
`ToolResultPart`.

Retries require both a retryable failure and safe execution semantics. A
possibly-started mutating call needs a declared idempotency mechanism and its
captured external key when applicable; the executor must verify the selected
invoker actually enforces it before retrying. Raw exceptions and secret-bearing
arguments never enter model-visible results.

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
