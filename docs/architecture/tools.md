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

public sealed record ToolsetSourceSelection(
    ToolSourceId SourceId);

public sealed record ToolAliasAssignment(
    ToolAlias Alias,
    ToolIdentity Tool);

public sealed record ToolsetPublication(
    ToolsetKey Key,
    ToolsetVersion Version,
    ToolExecutionPolicyReference ExecutionPolicy,
    ImmutableArray<ToolsetSourceSelection> Sources,
    ImmutableArray<ToolAliasAssignment> Aliases);

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
    ImmutableDictionary<ToolSourceId, ToolSourceVersion> SourceVersions,
    ImmutableArray<ToolDescriptor> Tools,
    ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>
        ExecutionPolicies,
    ImmutableDictionary<ToolAlias, ToolIdentity> ProviderAliases);
```

An agent definition authors only `ToolsetReference`: the stable toolset key and
execution-policy family it selects. The referenced immutable
`ToolsetPublication` owns the positive toolset version, exact retained policy
version, ordered source membership, and explicit provider-visible alias
assignments. Source membership names a stable `ToolSourceId`; it does not ask an
agent author to predict a dynamic provider's next `ToolSourceVersion`.

Capture resolves every selected source once. Each provider returns an owned
`IToolProviderCapture` containing its exact `ToolProviderSnapshot` and the
ability to acquire invokers only from that source publication. The catalog
returns an owned `IToolCatalogCapture` that retains those provider captures
alongside its immutable snapshot. A later provider refresh cannot alter the
captured graph. Borrowed host-DI instances remain owned by their host scope.

`SourceVersions` retains every acquired source's exact publication version,
including selected sources that exposed no tools. Every descriptor's source must
appear in this map. The map uses exact domain equality regardless of a supplied
dictionary comparer. A provider snapshot contains only descriptors from its own
source and rejects repeated exact tool identities; display names need not be
unique within that publication. Cross-source and exposure collisions belong to
catalog merging. These snapshots carry immutable evidence, not live invokers or
proof that an acquisition remains available after process loss.

Alias assignments target an exact `ToolIdentity`; capture verifies that the
identity appears in exactly one selected source snapshot and that the captured
descriptor declares the same source. An alias is never inferred from a
descriptor name or canonical ID. Unknown provider text remains an unresolved
alias and reaches typed rejection. Multiple aliases may explicitly target one
identity, while duplicate aliases and ambiguous source identities are catalog
collisions.

One replaceable `IToolCatalogMergePolicy` receives the complete deterministic
collision set after all selected source snapshots validate and before any alias
is advertised. Its closed decision either selects an explicitly configured
source and identity already present in the set or rejects capture; it cannot
create an identity, version, alias, policy reference, or invoker. The
first-party policy rejects every unconfigured descriptor, identity, source, or
alias collision. Registration order, dictionary comparer behavior, descriptor
names, and textual alias-to-ID equality never break ties.

`ToolCatalogCandidate` retains its complete `ToolsetPublication`, exact
`ToolProviderSnapshot`, and the descriptor found in that source. Candidates are
ordered by authored toolset, source membership, then descriptor order. Repeated
exact identities produce `ToolCatalogIdentityCollision`, preserving competing
source, descriptor, and execution-policy evidence together. Even equivalent
overlapping toolsets require explicit selection. Different versions of one
canonical ID remain distinct identities. Repeated display names alone do not
collide when explicit aliases are unambiguous.

`ToolCatalogAliasCollision` retains every candidate and unresolved assignment
for an explicitly assigned alias. A repeated alias still collides when one or
every assignment has no descriptor. `ToolCatalogMissingAliasTarget` retains the
originating toolset and exact missing assignment; another toolset's membership
cannot repair it. The complete `ToolCatalogMergeContext` lists identity
collisions first, alias collisions second, and missing assignments last,
preserving first authored occurrence in each category. All selected source keys
and publications must validate before policy runs. A malformed source map or a
publication that differs from the authored selection is invalid input, not a
precedence choice.

The closed `ToolCatalogMergeDecision` is either `ToolCatalogRejection` or
`ToolCatalogSelection`. A selection supplies exactly one existing candidate per
distinct identity and one existing assignment per distinct authored alias. It
cannot omit an unambiguous contribution, hide a missing target, or reorder the
catalog. Alias contributions must agree with the selected descriptor, complete
source publication, and exact execution policy. Equivalent toolset origins may
contribute different aliases to that same selected binding. The catalog
revalidates all evidence and preserves the first occurrence of each identity
when ordering selected descriptors.

`RejectingToolCatalogMergePolicy` accepts collision-free graphs and rejects
every collision. `AddToolCatalogMerging` registers that replaceable default and
the internal merge coordinator without source discovery or activation.
Composition requires exactly one unkeyed policy; multiple registrations never
select a winner by order. `ReplaceToolCatalogMergePolicy<TPolicy>` explicitly
replaces unkeyed policies while preserving keyed registrations, host clocks, and
old hosts. The merge coordinator borrows source snapshots and owns no capture;
catalog capture still owns discovery, cleanup, schema/capability preflight, and
model exposure. The legacy `AddAgentTools` coordinator is not yet migrated.

Merge and policy operations use `tool.catalog.merge` and
`tool.catalog.merge.policy`, with safe run correlation and bounded
operation/outcome metrics. Cancellation is checked before policy and after its
completion; a late decision transfers no snapshot. Rejection returns the
complete collision context without a partial snapshot. Policy failure and
invalid decisions propagate as failures, with observer and clock failures
isolated from the semantic outcome.

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

`ToolDiscoveryRequest` also requires matching agent/session IDs and an
`InRunOperationCorrelation` for the exact active run. Before-run or after-run
correlation cannot substitute a causal run ID. Toolset selections have unique
exact keys; an empty selection and definition revision zero are valid. Local
construction establishes this correlation only. Publication resolution, model
capability/schema preflight, and authorization of protected discovery remain
runtime responsibilities.

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
    ToolCatalogVersion CatalogVersion,
    ToolAlias ProviderAlias,
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
    ToolCatalogVersion CatalogVersion,
    ToolAlias ProviderAlias,
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

Every `ToolCallResult` describes an attempted or requested tool-effect boundary,
so it rejects `SideEffectCertainty.NotApplicable`. A pre-invocation terminal
result uses `DefinitelyNotPerformed` only when evidence establishes that the
effect did not occur. `PartiallyPerformed` is possibly started and receives the
same idempotency or reconciliation protections as an unknown effect. Completion
certainty and durable terminal recording remain distinct facts.

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
    ToolSourceId SourceId { get; }

    ValueTask<IToolProviderCapture> DiscoverAsync(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IToolProviderCapture : IAsyncDisposable
{
    ToolProviderSnapshot Snapshot { get; }

    ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(
        ToolIdentity identity,
        CancellationToken cancellationToken);
}

public interface IToolRegistrationCatalog
{
    ToolDiscoverySelection ResolveSelection(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IToolCatalog
{
    ValueTask<IToolCatalogCapture> CaptureAsync(
        ToolDiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IToolCatalogCapture : IAsyncDisposable
{
    ToolCatalogSnapshot Snapshot { get; }

    ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(
        ToolIdentity identity,
        CancellationToken cancellationToken);
}

public interface IToolInvokerLease : IAsyncDisposable
{
    ToolDescriptor Tool { get; }
    ToolSourceVersion SourceVersion { get; }
    IToolInvoker Invoker { get; }
}

public interface IToolResolver
{
    ValueTask<ToolResolutionResult> ResolveAsync(
        IToolCatalogCapture capture,
        ToolCallRequest request,
        CancellationToken cancellationToken);
}

public interface IToolSchemaEngine
{
    ToolSchemaProfile Profile { get; }

    ToolSchemaCompilationResult Compile(
        JsonSchema schema,
        ToolSchemaLimits limits,
        CancellationToken cancellationToken);
}

public interface ICompiledToolSchema
{
    JsonSchema Schema { get; }
    ToolSchemaProfile Profile { get; }
    ToolSchemaLimits CompilationLimits { get; }

    ToolSchemaValidationResult Validate(
        JsonElement instance,
        ToolSchemaLimits limits,
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

`IToolProvider.SourceId` is stable composition metadata that requires no I/O.
Its capture must publish that same source. Capture snapshot access is stable and
side-effect-free. `ToolInvokerLeaseResult` is closed: `ToolInvokerAcquired`
carries an owned `IToolInvokerLease`; `ToolInvokerUnavailable` carries the exact
requested identity and bounded safe reason. Acquiring an invoker never invokes
the tool or grants authority. An acquired lease must match the full captured
descriptor and its source version. It cannot substitute a newer implementation.

The resolver validates the request's catalog, agent, session, run, identity,
definition, and security/configuration evidence before acquiring an invoker. It
resolves the requested alias only through the captured alias map. Unknown or
ambiguous aliases fail before acquisition. Resolution success transfers both a
`ResolvedToolCall` and its owned invoker lease to the executor; failure or
cancellation releases any acquisition before returning or propagating. Resolved
and validated values retain the original alias and catalog version. The executor
retains the lease through validation, planning, and settlement and supplies that
exact binding to the scheduler alongside the prepared call. Immutable call and
terminal evidence never contains a live lease.

Closing a capture rejects new acquisitions; outstanding invoker leases retain
their exact bindings until released. `DisposeAsync` waits for those leases and
the single owned cleanup; callers must release their leases before awaiting
closure on the same control path. A released lease rejects further invoker
access while retaining readable descriptor/version evidence. Repeated or
concurrent disposal shares completion, including cleanup failure, and never
retries the resource effect. Acquisition from a closed source returns
`ToolInvokerUnavailable` for the exact requested identity; cancellation before
ownership transfer still propagates as cancellation.

A failed or cancelled catalog capture releases every acquisition it owns and
publishes no partial catalog. Ownership transfer and release must be race-safe
and dispose each owned acquisition once. Disposal never proves that an external
effect stopped or was undone.

The internal first-party `ToolCatalogDiscovery` implements complete source
acquisition through the materialized registration view. It rejects null or
substituted request selections, calls each distinct provider once in authored
first-use order, and validates provider identity immediately before discovery.
Returned captures are owned before post-await cancellation or snapshot access.
Null captures, null or foreign publications, throwing metadata getters, and
reused capture instances reject the operation without a partial result. Earlier
owners and late captures are released before failure escapes.

`ToolDiscoveryCapture` owns the resulting selection, exact immutable source
publications, and distinct source acquisitions until merge/preflight finishes.
Its one-time handoff constructs `ToolCatalogCapture` from retained publications,
without rereading third-party metadata. Catalog construction revalidates exact
request correlation, source membership, versions, and selected descriptors.
Failed handoff retains the discovery owner's sources; a successful handoff and
closure have one synchronized winner. Closing the old discovery owner cannot
close the transferred catalog. Metadata remains readable after closure.

Discovery cleanup starts every source release before awaiting any, including
empty sources. Failures are ordered by ordinal source ID, independently of
completion order. A discovery failure or cancellation precedes all cleanup
failures in the resulting aggregate; successful cleanup preserves the original
exception and cancellation token. Repeated closure shares cleanup completion or
failure and never retries an effect. Reused owner instances reject before
metadata reads at direct catalog construction as well as during discovery.

`AddToolRegistrationCatalog` installs this internal coordinator without
activation and requires exactly one nonnull unkeyed registration view when
resolved. Providers retain host or external disposal ownership; source captures
own their acquired resources. The coordinator retains no mutable run state or
container. Discovery, each source callback, transfer, closure, batch cleanup,
and each source cleanup emit shared `tool.catalog.discover*` /
`tool.catalog.discovery.*` activities and events 4080/4081 with safe run/source
correlation. Metrics use only bounded operation/outcome dimensions. Observer
failures are isolated and missing or reversed duration is omitted.

This source-acquisition boundary is implemented. The combined canonical catalog
still needs integration of canonical schema compilation,
provider/model-capability preflight, and migration from the legacy
`IToolCatalog`/loop path. Neither source discovery nor handoff skips those
requirements or claims model-ready schema support.

The local schema boundary is separately implemented by `IToolSchemaEngine` and
`ICompiledToolSchema` in `AgentKit.Abstractions`, with the first-party bounded
engine in `AgentKit.Tools`. Another engine can implement those contracts without
referencing this runtime. No source, provider, or output-runtime dependency is
introduced. Complete compilation returns `ToolSchemaCompiled`; a classified
`ToolSchemaCompilationRejected` contains no partial executable handle.

A `ToolSchemaProfile` pins a typed identity, positive version, exact dialect,
and disjoint assertion/annotation keyword sets. Unknown keywords reject. A
compiled handle retains the exact owned canonical `JsonSchema`, profile, and
compilation limits; consumers revalidate this evidence before exposure.
Validation never rereads current registrations, changes input, applies defaults,
coerces values, or performs I/O. Handles are immutable, resource-free, and safe
for concurrent callers with independent operation budgets.

`AddToolSchemaEngine` idempotently registers the unkeyed default and observation
collaborators; `ReplaceToolSchemaEngine<TEngine>` replaces all unkeyed engine
registrations without activating them. Explicit keyed registrations and old
hosts/compiled handles are preserved. The default profile
`agentkit-bounded-tool-schema`, revision 1, accepts draft 2020-12 with explicit
support for type, properties, required, additionalProperties, items, enum,
const, minimum/maximum and their exclusive forms, string/array/property count
limits, and uniqueItems. Supported annotations are `$comment`, title,
description, default, examples, deprecated, readOnly, writeOnly, and format.
Format remains annotation-only. References, regex assertions, applicator unions,
nested dialect declarations, vocabulary changes, and all undeclared keywords
reject; this is a bounded subset, not complete draft conformance.

`ToolSchemaLimits` bounds raw UTF-8 bytes, root-inclusive depth, JSON-value
count, and deterministic total work. The default engine additionally caps actual
JSON depth at 128. Raw UTF-8 length is read before decoded allocations;
annotation data and duplicate members are included in inspection. Work charges
cover traversal, property lookup, comparisons, and numeric parsing, including
quadratic uniqueness checks. Exact decimal comparison never expands exponents or
rounds through binary floating point. String length counts Unicode scalar
values. Validation distinguishes invalid data from exhausted resources;
cancellation propagates unchanged. Raw tool arguments still require their own
bound before parsing at the argument-validator boundary.

Compilation and validation emit `tool.schema.compile` / `tool.schema.validate`,
events 4090/4091, and bounded operation/outcome metrics. No schema, instance,
profile, property name, or exception content enters diagnostics. Parent activity
context supplies causality without inventing run identity. Observer failures and
missing/reversed diagnostic time cannot alter validation.

Canonical compilation does not select a provider translation profile or expose
tools to a model. Catalog integration must compile every retained input/output
schema, validate returned exact evidence, preflight model-visible translation,
and retain canonical handles through execution. Those integration and legacy
catalog/loop migration steps remain open.

The first-party `StaticToolProvider` implements `IToolProvider` over a
host-supplied `ToolProviderSnapshot` and complete exact invoker map. It
validates the binding graph once, preserves the explicit source version, and
returns a fresh `ToolProviderCapture` for each discovery. Captures share
immutable bindings but never share closure or lease state. Discovery does not
read invoker metadata, perform I/O, or invoke a tool.

This provider is deliberately principal-independent: it publishes the configured
metadata for every locally coherent request. Toolset selection, model capability
and schema preflight, alias assignment, and authorization remain at their owning
boundaries. The host keeps borrowed invokers alive until all captures and leases
have closed. Per-request instances, principal-specific filtering, and protected
remote discovery require a provider that owns those acquisition mechanics.

The first-party `ToolProviderCapture` captures a complete exact
identity-to-invoker map beside one `ToolProviderSnapshot`. It validates missing,
extra, default, null, and duplicate normalized bindings before taking ownership
of an optional `IAsyncDisposable` source lifetime. That lifetime may own a
source DI scope, but must not own or await the capture or its leases. Without
it, invokers remain externally owned. The capture never disposes individual
invokers, discovers a new binding, or invokes a tool. Providers construct it per
acquisition rather than registering it as an engine-wide singleton. Its
synchronized transitions run cleanup and diagnostic callbacks outside the state
gate.

The first-party `ToolCatalogCapture` owns the source graph for an already merged
`ToolCatalogSnapshot`. Construction normalizes source keys, validates the exact
source-version keyset and every selected descriptor against its source, and
reads each source snapshot once. Rejection leaves every source with the caller.
Unselected source descriptors remain unavailable through the catalog; a merge
coordinator must decide selection and collisions before construction.

Each pending source acquisition retains the catalog until it either transfers a
validated lease or releases its temporary acquisition. A successful source lease
must match the full descriptor and source version and supply a nonnull invoker.
Cancellation or catalog closure before transfer releases a late lease before
returning. Malformed source results fail without redirecting to another source.
The catalog wraps each successful source lease so repeated release has one
owner.

Catalog closure waits for pending acquisition and transferred leases before
starting every owned source cleanup once. It starts all source cleanups before
awaiting any, including empty or fully filtered sources. One cleanup failure is
preserved; multiple failures are aggregated in ordinal source-ID order rather
than completion order. Acquisition or lease-release failure does not hide a
second cleanup failure. Source callbacks never run under the catalog state gate.
This class is created per capture, not registered as a singleton; discovery,
merge policy, and the catalog coordinator retain their separate ownership.

### Execution, durable state, and observation

```csharp
namespace AgentKit;

public interface IToolInvoker
{
    ValueTask<ToolInvocationResult> InvokeAsync(
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
        IToolCatalogCapture capture,
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
discovers tools or decides policy. It returns owned raw `ToolInvocationResult`
evidence, which cannot establish successful terminal normalization or recording.
The executor combines that evidence with the admitted call, acceptance record,
and captured policies, applies the selected normalizer, and constructs the
authoritative `ToolCallResult`. `IToolCallRecorder` owns accepted/terminal state
and optimistic or idempotent recording; it is not an event sink.
`IToolEventSink` observes immutable activity and cannot influence the result.
`IToolResultProjectionPolicyCatalog` resolves the exact retained snapshot named
by the terminal record; `IToolResultProjector` deterministically creates the
bounded message value and never performs or retries the tool effect. The loop
depends only on `IToolExecutor`.

Policy resolution has two closed outcomes: `ToolResultProjectionPolicyResolved`
contains the immutable snapshot, and `ToolResultProjectionPolicyUnavailable`
retains the requested reference. Cancellation propagates; an operational failure
must not masquerade as an unavailable revision. No outcome permits selecting a
newer revision or repeating the tool effect.

The first-party `ToolResultProjectionPolicyCatalog` captures explicitly
registered immutable snapshots once at composition. It accepts structurally
equivalent duplicates and rejects different content under one reference.
Reference matching is ordinal and version-exact; an empty catalog resolves all
references as unavailable. This configuration catalog provides no mutable
publication or persistence surface. Hosts retain every revision needed for
recovery, or replace the catalog with an implementation that resolves retained
policy content through its own storage boundary.

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

### Application presentation

`IToolPresenter` owns bounded, provider-neutral application presentation
separately from execution, authorization, and model-history projection. A
request carries an exact captured `ToolDescriptor` when available, plus either
the original `ToolCallPart` or an explicitly labeled `ToolResultPart`
projection. The presenter selects an additive `IToolPresentationFormatter` only
when its complete source-owned descriptor is value-equal to the captured
descriptor. It performs no alias guessing or live catalog lookup; absent or
mismatched evidence uses bounded fallback.

Presentations contain literal text, code, and diff parts with optional language
or path hints. Core values contain no terminal markup or ANSI control sequences.
Input bytes, output characters, and part count are bounded, truncation is
reported, and cancellation propagates.

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

`AddToolResultProjectionPolicyCatalog` can register the catalog independently,
preserving an existing catalog or `TimeProvider` and adding logging without an
exporter. `AddToolResultProjectionPolicy` registers a supplied snapshot; no
default policy content is fabricated. `ReplaceToolResultProjectionPolicy`
changes only the exact reference before catalog capture and leaves existing
catalog instances unchanged. It rejects opaque unkeyed snapshot registrations
before mutation because their reference cannot be inspected without activating
services. Replacement must not rewrite retained content required by recorded
results. `ReplaceToolResultProjectionPolicyCatalog<TCatalog>` replaces the
singular catalog registration while preserving configured snapshots and clocks.

`AddToolset(ToolsetPublication)` publishes complete immutable membership,
versions, execution-policy evidence, and explicit aliases under an exact typed
`ToolsetKey`. `ReplaceToolset` replaces every descriptor for that exact key
without activation. The explicit publication overload is implemented; the
options-based convenience shape above remains planned.

`AddToolProvider<TProvider>(sourceId)` registers a concurrently callable,
host-owned singleton under its exact `ToolSourceId`. Standard `[ServiceKey]`
constructor injection supplies the typed key when needed. The instance overload
validates source identity before mutation and keeps the provider's original
external disposal owner. `ReplaceToolProvider` changes only the exact source;
old hosts and retained selections keep their original bindings. Add rejects
duplicates, including identical instances, before mutation. No overload
activates provider services while registering them.

These registrations install the replaceable `IToolRegistrationCatalog` through
`AddToolRegistrationCatalog`. Its composition factory resolves explicit source
and toolset keys once, requires exactly one registration for each key, validates
provider source identity and complete publication membership, then releases the
container reference. String-keyed, foreign-keyed, and unkeyed services never
supply fallback registrations. Sources and toolsets can be registered in either
order. `ReplaceToolRegistrationCatalog<TCatalog>` preserves publications,
providers, host clocks, keyed catalogs, and already constructed hosts.

The first-party `ToolRegistrationCatalog.ResolveSelection` validates every
authored toolset key and execution-policy family before source discovery. It
returns a `ToolDiscoverySelection` retaining the original request, exact
publications in request order, and borrowed `ToolProviderBinding` instances in
first-use source order. A shared source appears once. Unknown keys or policy
families reject the whole selection with `InvalidOperationException`; empty
selection exposes no source or toolset. It queries neither live provider
metadata nor the container and owns no provider, capture, or invoker lifetime.

Selection uses `tool.registration.select`, start/completion events 4070/4071,
and bounded outcome-only count/duration metrics. Traces and logs retain safe run
identity without alias, policy, descriptor, or exception content. Cancellation
prevents transfer; observer failures cannot change selection, and unknown or
reversed timing omits duration. This materialized view is implemented separately
from source acquisition ownership and the remaining canonical schema/capability
preflight and catalog integration.

`AddStaticToolProvider(snapshot, invokers)` registers one keyed singleton
`IToolProvider`, using the exact `ToolSourceId` value as its DI key. It
validates the full binding graph before changing registrations, rejects
duplicate keys even for identical publications, and preserves host loggers and
clocks. It builds no host, activates no service, and registers no unkeyed
fallback. The provider owns no borrowed invoker lifetime; its returned captures
belong to discovery callers.

`ReplaceStaticToolProvider` changes only that exact typed source key, including
opaque factory registrations, without activation. Other source keys and existing
hosts, providers, captures, and leases are unchanged. Replacement does not
reclaim an old invoker. Only actual `ToolSourceId` keys participate in matching;
equality on a foreign host key cannot claim or replace a source. These
registrations supply discovery sources; the catalog coordinator still selects
sources from explicit toolset publications and never chooses a source through
registration order.

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
`CaptureAsync` returns a run-owned `IToolCatalogCapture` with an immutable
`ToolCatalogSnapshot`; the singleton coordinator retains neither the capture nor
run services. Mutable budgets, invocation state, progress reporters, resolved
invoker leases, and scoped tool instances are run- or operation-scoped. Capture
and lease owners release their acquisitions; the owning DI scope disposes any
borrowed invoker instances once. Cancellation stops admission and awaiting, then
follows the bounded settlement rules; it never claims synchronous or remote
effects were undone. Retry delay uses injected `TimeProvider` and injectable
randomness.

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

The toolset and capture contract has these acceptance scenarios:

- A request selecting several toolsets resolves all keys and policy families
  before discovery. An unknown later key rejects the whole selection, and an
  empty request never falls back to registered sources.
- Toolset order follows the request, independently of registration order. Shared
  sources appear once in first-use order, preserving exact publication and
  policy versions without live provider metadata reads.
- Duplicate or missing explicit keys, mismatched provider identity, and a
  publication referencing a missing source reject materialization. Typed-key
  replacements preserve foreign registrations without activation.
- A retained selection discovers the original source and acquires its original
  invoker after a later host replaces both source and toolset publication.
  Generic providers keep host disposal ownership; supplied instances retain
  external ownership.
- An authored toolset selects a source ID without naming a future dynamic source
  version; capture records the exact returned source version and retains it
  after the provider publishes a newer snapshot.
- Two explicit aliases may target one exact identity. Duplicate aliases,
  ambiguous identities from selected sources, and an alias targeting a missing
  descriptor reach the configured merge policy and reject under the first-party
  policy before conversational model I/O. Dynamic discovery may perform its own
  bounded provider I/O before producing a source snapshot.
- A provider alias whose text equals a canonical tool ID remains unresolved
  unless the captured toolset explicitly assigned that alias.
- A merge policy may choose only an identity and source already present in the
  validated collision set. A decision containing different evidence is rejected
  rather than treated as a new registration.
- Collisions are collected across every selected publication before the single
  policy call. Missing alias targets cannot be borrowed from an unrelated
  toolset, dropped from a decision, or repaired by fabricated metadata.
- A configured selection preserves descriptor order independently of the
  selection array and retains every selected source version, including empty
  sources. An alias cannot select a different source or execution policy from
  its chosen descriptor.
- Multiple unkeyed merge policies reject composition. Explicit policy
  replacement preserves previously constructed hosts and keyed host policies.
- A provider returning a capture after cancellation has that owner released
  before cancellation propagates. A throwing, null, or foreign snapshot still
  leaves the returned owner eligible for cleanup.
- A later discovery failure starts every earlier cleanup before awaiting one.
  Multiple cleanup failures retain source-ID order after the original failure;
  cleanup is not cancelled by the discovery token.
- A source graph survives merge rejection until its discovery owner closes it.
  Successful handoff uses retained publication evidence without live metadata
  reads and retains its invoker until the last lease is released.
- Closure racing handoff has one winner. A capture reused for two source keys
  rejects before direct catalog ownership or duplicate cleanup can occur.
- A dynamic provider lease binds only descriptors from its own exact returned
  source snapshot. A changed or missing binding rejects resolution, and partial
  capture failure releases every owned acquisition exactly once.
- Capturing or releasing borrowed host-DI tool instances never transfers or
  duplicates their owning scope's disposal responsibility.
- A selected empty source remains in `SourceVersions`. Missing descriptor-source
  versions and repeated identities within one provider publication reject
  construction; provider display names remain subject to catalog alias policy.
- Discovery rejects a substituted identity, session, active run, definition, or
  configuration before contacting a source. After-run correlation with the same
  causal run ID is still invalid.
- Closing a capture blocks new acquisitions while an outstanding invoker lease
  retains its binding. Cancellation during capture releases partial acquisitions
  without advertising a partial graph.
- Capture closure waits for every outstanding lease before disposing an owned
  source scope. A borrowed host scope retains its invokers until the host closes
  it. Concurrent release and closure perform owned cleanup once.
- A failing owned cleanup reaches every waiting close/final-release caller;
  repeated disposal observes the same failure and never repeats cleanup.
- Exact acquisition, release, closure, and owned cleanup expose correlated safe
  diagnostics with bounded operation/outcome metrics. Throwing listeners,
  loggers, and diagnostic clocks cannot alter ownership or the semantic result.

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
[artifact references](artifacts.md) according to that snapshot. The executor
coordinates normalization, authorized artifact creation, and the
`IToolCallRecorder` terminal commit; the artifact component never calls back
into the tool recorder. A separate deterministic projection then creates the
tighter model/history `ToolResultPart`.

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
