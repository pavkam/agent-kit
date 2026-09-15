# Context compaction

**Role:** Produce and activate a bounded semantic checkpoint of older context
without deleting or rewriting durable conversation history.

`AgentKit.Context.Compaction` is the first-party implementation package for the
provider-neutral compaction contracts in `AgentKit.Abstractions`. It is separate
from [AgentKit.Context](context.md): the context assembler decides when its
captured policy requires compaction and consumes the result, while the
compaction component selects a safe cut, produces and validates a candidate, and
conditionally activates a durable record.

The dependency direction is inward. `AgentKit.Context.Compaction` may depend on
`AgentKit.Abstractions` and the public session, provider, hook, and security
contracts. It never references a concrete provider SDK or session store.
`AgentKit.Context` consumes `ICompactor`; neither package resolves keyed
services at runtime.

## Responsibilities and exclusions

The component:

- reads one authorized, versioned session branch snapshot;
- selects a complete semantic cut without splitting tool, approval, deferred,
  goal, or admitted-input causality;
- preserves an exact retained suffix and a hash manifest of the covered source;
- invokes only strategies selected by the immutable compaction policy snapshot;
- keeps generated summaries untrusted even after structural, causal, security,
  and measurable-reduction validation succeeds;
- records strategy, model, settings, source, context epoch, instruction,
  configuration, estimate, and supersession provenance; and
- activates through optimistic session concurrency so concurrent appends are
  never overwritten.

It does not assemble a provider request, own durable session storage, mutate an
agent definition, grant security authority, translate provider wire formats, or
delete covered history. A checkpoint affects only later context views on the
same applicable branch.

## Normative identities and correlation

The following C# 14 shapes are normative and minimal rather than exhaustive.
Every named type lives in its own same-named file in `AgentKit.Abstractions`.
Shared identifiers, session values, security values, content parts, provider
identities, and extension data reuse their canonical definitions.

```csharp
namespace AgentKit;

public readonly record struct CompactionId(Guid Value);

public readonly record struct CompactionManifestId(Guid Value);

public readonly record struct CompactionProfileKey(string Value);

public readonly record struct CompactionProfileVersion(long Value);

public readonly record struct CompactionManifestVersion(long Value);

public readonly record struct CompactionRecordVersion(long Value);

public readonly record struct CompactionStrategyKey(string Value);

public readonly record struct CompactionStrategyVersion(string Value);

public readonly record struct CompactionSummaryGeneratorKey(string Value);

public readonly record struct CompactionSummaryGeneratorVersion(string Value);

public readonly record struct CompactionCutSelectorVersion(string Value);

public readonly record struct CompactionValidatorVersion(string Value);

public readonly record struct CompactionValidationRuleId(string Value);

public readonly record struct CompactionEventSinkId(string Value);

public readonly record struct ContextEpoch(long Value);

public sealed record CompactionOperationContext(
    CompactionId CompactionId,
    AgentId AgentId,
    SessionId SessionId,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization);
```

Default, empty, non-positive, or whitespace identities and versions are invalid
at public boundaries. `CompactionId`, `CompactionManifestId`, and the
`OperationId` inside `OperationCorrelation` come from their closed injected
`IIdentifierGenerator<TIdentifier>` registrations. In-run work uses
`InRunOperationCorrelation`; work causally following a settled run uses
`AfterRunOperationCorrelation`; out-of-run maintenance uses
`BeforeRunOperationCorrelation` without fabricating a `RunId` or `TurnId`. There
is no ambient current run, and a session sequence is never an identity.

`CompactionId` is the idempotent identity of one logical checkpoint across
retries. `OperationCorrelation.OperationId` identifies one causal invocation and
carries a real run and optional turn only when one exists. The authorization
context captures the exact authority key, policy snapshot, principal, tenant,
agent-definition revision, and configuration version. It contains no grant and
cannot itself authorize an effect.

## Immutable request and policy snapshot

Options are converted into one immutable policy snapshot when the run plan is
compiled. Reloads may affect a later run or named next-turn boundary; they never
reinterpret an in-flight compaction.

```csharp
namespace AgentKit;

public enum CompactionTriggerKind
{
    ContextPressure,
    ProviderOverflow,
    ExplicitMaintenance,
    InstructionEpochChanged
}

public sealed record CompactionTrigger(
    CompactionTriggerKind Kind,
    string Reason,
    OperationId? CausalOperationId);

public sealed record CompactionSizeEstimate(
    int Tokens,
    long Bytes,
    int EntryCount);

public sealed record CompactionPolicySnapshot(
    CompactionProfileKey ProfileKey,
    CompactionProfileVersion ProfileVersion,
    ComponentKey<ICompactor> CompactorKey,
    ImmutableArray<CompactionStrategyKey> StrategyOrder,
    int MaximumAttempts,
    int MaximumSourceEntries,
    long MaximumSourceBytes,
    int MaximumSummaryTokens,
    int MinimumRetainedEntries,
    int MaximumValidationIssues,
    double MinimumReductionRatio,
    bool AllowOversizedTurnRepair,
    bool PersistRejectedCandidates,
    ContentHash ConfigurationFingerprint);

public sealed record CompactionRequest(
    CompactionOperationContext Context,
    SessionProfileReference SessionProfile,
    BranchId BranchId,
    SessionVersion SourceVersion,
    SessionSequence SourceThrough,
    ContextEpoch ContextEpoch,
    ContentHash EffectiveInstructionsFingerprint,
    CompactionTrigger Trigger,
    CompactionPolicySnapshot Policy,
    int TargetInputTokens,
    DateTimeOffset RequestedAt,
    DateTimeOffset Deadline,
    ExtensionData Extensions);
```

Construction validates that correlation values agree with the authorization
scope, the deadline follows the request time, bounds are positive, the source
sequence exists in the named version and branch, and the strategy order has no
duplicates. The invocation's `SessionExecutionCapability.Profile.Reference` must
equal `SessionProfile`; a mismatch is rejected before authorization or session
I/O. Provider overflow includes the failed provider request's `OperationId` as
the causal operation. Text such as `Reason` is descriptive and never used as
identity, policy, or authority.

## Source snapshot and semantic cut

Source loading is a protected session read. The first-party compactor obtains a
bounded immutable snapshot through session contracts after authorization; a cut
selector receives data, not a session store or service provider.

```csharp
namespace AgentKit;

public sealed record CompactionSourceSnapshot(
    CompactionOperationContext Context,
    BranchId BranchId,
    SessionVersion Version,
    SessionSequence ThroughSequence,
    ImmutableArray<SessionEntry> Entries,
    ContentHash ContentHash);

public sealed record CompactionSourceRange(
    SessionSequence StartInclusive,
    SessionSequence EndInclusive);

public sealed record CompactionCut(
    CompactionSourceRange CoveredRange,
    SessionSequence RetainedSuffixStart,
    ImmutableArray<SessionEntryId> CoveredEntryIds,
    ContentHash CoveredContentHash,
    CompactionCutSelectorVersion SelectorVersion);

public sealed record CompactionCutSelectionRequest(
    CompactionRequest Request,
    CompactionSourceSnapshot Source);

public abstract record CompactionCutSelectionResult;

public sealed record CompactionCutSelected(CompactionCut Cut)
    : CompactionCutSelectionResult;

public sealed record NoSafeCompactionCut(CompactionRejection Rejection)
    : CompactionCutSelectionResult;

public sealed record CompactionCutSelectionFailed(CompactionFailure Failure)
    : CompactionCutSelectionResult;

public interface ICompactionCutSelector
{
    ValueTask<CompactionCutSelectionResult> SelectAsync(
        CompactionCutSelectionRequest request,
        CancellationToken cancellationToken = default);
}
```

`ICompactionCutSelector` is a pure policy boundary over a stable snapshot and
normally completes synchronously, hence `ValueTask`. The default selector
prefers a boundary before a user turn: it walks down from the largest boundary
that respects the retention minimum and returns the first causally safe one
whose retained suffix begins with a `UserMessage`, falling back to the largest
causally safe boundary only when no user-turn boundary qualifies. It cannot
split an assistant part, tool call and terminal result, approval and
resolution, deferred operation and settlement, admitted input and promotion, or
goal/delegation transition.

The covered IDs must exactly match the contiguous range and hash. The retained
suffix begins after the covered range and remains byte-for-byte represented by
the original entries. Oversized-turn repair is disabled by default; when
enabled, explicit repair markers and complete tool causality are mandatory.

The first-party compactor pins every continuation page to the first page's
`SessionReadSnapshot` and compares that snapshot's observed version with the
request's `SourceVersion` before any selector, strategy, or validator runs. A
mismatch is a `CompactionConflict` whose `Manifest` is null because no candidate
exists yet; a page without snapshot evidence fails closed as a non-retryable
`SourceUnavailable`; a continuation page whose snapshot differs from the pinned
one fails as a retryable `SourceUnavailable`. The pinned snapshot's upper
sequence is the branch tip used below.

`SourceThrough` is an eligibility bound, not the branch tip. The first-party
compactor reads the branch to its tip, hands collaborators only entries whose
sequence does not exceed `SourceThrough`, and keeps the tip for sequence
allocation of the activated entry. A bound beyond the tip names a range the
branch does not have and fails as a non-retryable `SourceUnavailable`. Because
the selector cannot see the ineligible tail, the compactor rejects a cut with
`NoSafeCut` when any covered entry is the causal parent of an entry beyond the
bound.

## Manifest, checkpoint, and durable record

The manifest proves what a candidate represents without pretending that a
summary is original history. Source hashes are computed from canonical,
versioned session serialization.

```csharp
namespace AgentKit;

public sealed record CompactionSourceManifestEntry(
    SessionEntryId EntryId,
    SessionSequence Sequence,
    SchemaVersion SchemaVersion,
    ContentHash ContentHash);

public sealed record CompactionProducer(
    CompactionStrategyKey StrategyKey,
    CompactionStrategyVersion StrategyVersion,
    CompactionSummaryGeneratorKey? SummaryGeneratorKey,
    CompactionSummaryGeneratorVersion? SummaryGeneratorVersion,
    ProviderId? ProviderId,
    ModelId? ModelId,
    ProviderModelRevision? ModelRevision,
    ProviderResponseId? ProviderResponseId,
    ContentHash SettingsFingerprint,
    bool Deterministic,
    ModelUsage? Usage,
    ExtensionData Extensions);

public sealed record CompactionManifest(
    CompactionManifestId Id,
    CompactionManifestVersion Version,
    CompactionOperationContext Context,
    BranchId BranchId,
    SessionVersion SourceVersion,
    CompactionSourceRange CoveredRange,
    SessionSequence RetainedSuffixStart,
    ImmutableArray<CompactionSourceManifestEntry> Sources,
    ContentHash CoveredContentHash,
    CompactionProducer Producer,
    CompactionProfileKey ProfileKey,
    CompactionProfileVersion ProfileVersion,
    ContextEpoch ContextEpoch,
    ContentHash EffectiveInstructionsFingerprint,
    ContentHash ConfigurationFingerprint,
    CompactionSizeEstimate Before,
    CompactionSizeEstimate After,
    DateTimeOffset ProducedAt,
    ExtensionData Extensions);

public enum CompactionStateKind
{
    PendingToolCall,
    ToolEffect,
    Approval,
    DeferredOperation,
    Goal,
    Resource,
    InstructionEpoch
}

public sealed record CompactionStateReference(
    CompactionStateKind Kind,
    SessionEntryId SourceEntryId,
    ContentHash ContentHash,
    ExtensionData State);

public sealed record CompactionCheckpoint(
    ImmutableArray<ContentPart> Summary,
    ImmutableArray<CompactionStateReference> State,
    ExtensionData Extensions);

public sealed record CompactionCandidate(
    CompactionManifest Manifest,
    CompactionCheckpoint Checkpoint);

public sealed record CompactionValidationStamp(
    CompactionValidatorVersion ValidatorVersion,
    ContentHash CandidateHash,
    DateTimeOffset ValidatedAt);

public sealed record ValidatedCompaction(
    CompactionCandidate Candidate,
    CompactionValidationStamp Stamp,
    ImmutableArray<CompactionValidationEvidence> Evidence);

public enum CompactionRecordStatus
{
    Active,
    HistoricalCandidate,
    Rejected
}

public sealed record CompactionRecord(
    CompactionOperationContext Context,
    CompactionRecordVersion Version,
    BranchId BranchId,
    SessionVersion SourceVersion,
    SessionVersion? ActivatedSessionVersion,
    CompactionRecordStatus Status,
    CompactionManifest Manifest,
    CompactionCheckpoint Checkpoint,
    CompactionId? Supersedes,
    CompactionFailure? Failure,
    DateTimeOffset RecordedAt,
    ExtensionData Extensions);

public sealed record CompactionSessionEntry(
    SessionEntryId Id,
    SessionAddress Address,
    OperationCorrelation Correlation,
    BranchId BranchId,
    SessionSequence Sequence,
    SessionEntryId? CausalParentId,
    DateTimeOffset RecordedAt,
    SchemaVersion SchemaVersion,
    CompactionRecord Record)
    : SessionEntry(
        Id,
        Address,
        Correlation,
        BranchId,
        Sequence,
        CausalParentId,
        RecordedAt,
        SchemaVersion);
```

An active record requires a validated candidate, a matching candidate hash, no
failure, and an activated session version. A rejected record requires a failure
and is never eligible for context assembly. Persisting rejected candidates is
off by default because it retains untrusted and potentially sensitive output.

Records are append-only facts. A newer active record names the prior
`CompactionId` in `Supersedes`; it does not mutate the older record. Original
source entries remain readable, branchable, auditable, and eligible for a later
recompaction. `CompactionRecordVersion` versions the record state/schema, while
`SessionVersion` remains the authoritative optimistic-concurrency version. The
`CompactionSessionEntry` address, correlation, branch, and timestamp must match
its nested record; construction rejects any mismatched envelope.

## Bounded summary-generation operation

A model-backed strategy delegates only the already prepared, bounded summary
operation below. The generator never receives an `IContextAssembler`,
`ICompactor`, `ISessionCoordinator`, full `CompactionSourceSnapshot`, or service
provider.

```csharp
namespace AgentKit;

public sealed record CompactionSummaryGeneratorDescriptor(
    CompactionSummaryGeneratorKey Key,
    CompactionSummaryGeneratorVersion Version,
    bool ModelBacked,
    bool Deterministic,
    int MaximumInputTokens,
    int MaximumOutputTokens);

public sealed record CompactionSummarySegment(
    ImmutableArray<SessionEntryId> SourceEntryIds,
    ImmutableArray<AgentMessage> Messages,
    ImmutableArray<CompactionStateReference> State,
    ContentHash ContentHash);

public sealed record CompactionSummaryRequest(
    CompactionOperationContext Context,
    CompactionSummaryGeneratorKey GeneratorKey,
    CompactionSourceRange CoveredRange,
    ImmutableArray<CompactionSummarySegment> Segments,
    int MaximumOutputTokens,
    ContentHash EffectiveInstructionsFingerprint,
    DateTimeOffset Deadline,
    ExtensionData Extensions);

public sealed record CompactionGeneratedSummary(
    ImmutableArray<ContentPart> Content,
    ImmutableArray<CompactionStateReference> State,
    ProviderId? ProviderId,
    ModelId? ModelId,
    ProviderModelRevision? ModelRevision,
    ProviderResponseId? ProviderResponseId,
    ModelUsage? Usage,
    ExtensionData Extensions);

public abstract record CompactionSummaryGenerationResult;

public sealed record CompactionSummaryGenerated(
    CompactionGeneratedSummary Summary)
    : CompactionSummaryGenerationResult;

public sealed record CompactionSummaryGenerationUnsupported(
    CompactionRejection Rejection)
    : CompactionSummaryGenerationResult;

public sealed record CompactionSummaryGenerationFailed(
    CompactionFailure Failure)
    : CompactionSummaryGenerationResult;

public sealed record CompactionSummaryGenerationCancelled(
    CompactionCancellation Cancellation)
    : CompactionSummaryGenerationResult;

public interface ICompactionSummaryGenerator
{
    CompactionSummaryGeneratorDescriptor Descriptor { get; }

    Task<CompactionSummaryGenerationResult> GenerateAsync(
        CompactionSummaryRequest request,
        BudgetExecutionCapability budget,
        CancellationToken cancellationToken = default);
}

public abstract record CompactionSummaryGeneratorResolution;

public sealed record CompactionSummaryGeneratorResolved(
    ICompactionSummaryGenerator Generator)
    : CompactionSummaryGeneratorResolution;

public sealed record CompactionSummaryGeneratorNotFound(
    ComponentKey<ICompactor> CompactorKey,
    CompactionSummaryGeneratorKey GeneratorKey)
    : CompactionSummaryGeneratorResolution;

public interface ICompactionSummaryGeneratorResolver
{
    ValueTask<CompactionSummaryGeneratorResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionSummaryGeneratorKey generatorKey,
        CancellationToken cancellationToken = default);
}
```

The generator returns `Task` because model-backed generation is inherently
asynchronous. Its request is a copied immutable projection whose aggregate input
and output limits have already been enforced. It cannot fetch more history, add
instructions, select a different compactor, activate a record, or recursively
assemble context.

The dependency graph is acyclic:

```text
IContextAssembler -> ICompactor -> ICompactionStrategy
    -> ICompactionSummaryGenerator
```

No edge points back to `IContextAssembler` or `ICompactor`. Provider selection,
translation, and transport stay behind the generator implementation or a still
narrower provider-neutral operation; compaction contracts do not expose the
general `IModelRequestExecutor` to strategies.

## Strategy contract

Strategies produce candidates; they do not choose source ranges, authorize
session access, validate their own claims, activate records, or decide whether
the loop retries. Deterministic extractive, model-backed semantic,
application-defined, and test strategies implement the same contract.

```csharp
namespace AgentKit;

[Flags]
public enum CompactionStrategyCapabilities
{
    None = 0,
    Extractive = 1,
    SemanticSummary = 2,
    StructuredState = 4,
    OversizedTurnRepair = 8
}

public sealed record CompactionStrategyDescriptor(
    CompactionStrategyKey Key,
    CompactionStrategyVersion Version,
    CompactionStrategyCapabilities Capabilities,
    bool Deterministic,
    CompactionSummaryGeneratorKey? SummaryGeneratorKey);

public sealed record CompactionStrategyRequest(
    CompactionRequest Request,
    CompactionSourceSnapshot Source,
    CompactionCut Cut);

public abstract record CompactionStrategyResult;

public sealed record CompactionCheckpointProduced(
    CompactionCheckpoint Checkpoint,
    CompactionProducer Producer,
    CompactionSizeEstimate After)
    : CompactionStrategyResult;

public sealed record CompactionStrategyUnsupported(CompactionRejection Rejection)
    : CompactionStrategyResult;

public sealed record CompactionStrategyFailed(CompactionFailure Failure)
    : CompactionStrategyResult;

public sealed record CompactionStrategyCancelled(
    CompactionCancellation Cancellation)
    : CompactionStrategyResult;

public interface ICompactionStrategy
{
    CompactionStrategyDescriptor Descriptor { get; }

    Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request,
        BudgetExecutionCapability budget,
        CancellationToken cancellationToken = default);
}

public abstract record CompactionStrategyResolution;

public sealed record CompactionStrategyResolved(ICompactionStrategy Strategy)
    : CompactionStrategyResolution;

public sealed record CompactionStrategyNotFound(
    ComponentKey<ICompactor> CompactorKey,
    CompactionStrategyKey StrategyKey) : CompactionStrategyResolution;

public interface ICompactionStrategyResolver
{
    ValueTask<CompactionStrategyResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionStrategyKey strategyKey,
        CancellationToken cancellationToken = default);
}
```

The strategy operation returns `Task` because model-backed production is
inherently asynchronous. An extractive strategy may return a completed task.
Model-backed strategies receive an `ICompactionSummaryGeneratorResolver` bound
to their compactor key. They prepare a bounded `CompactionSummaryRequest` and
never receive the full model request executor, context assembler, session
coordinator, or provider SDK.

The strategy resolver is bound to one compactor key and the current operation
scope. It can return only a strategy registered for that key and never falls
back to an unkeyed or engine-global instance. The owning DI scope, not the
resolution result, disposes the strategy.

Fallback to another strategy occurs only in the captured `StrategyOrder` and
only for an outcome the policy marks eligible. A strategy key/version mismatch,
missing model operation, unsupported oversized-turn repair, or absent capability
returns a typed unsupported result before source mutation.

## Validation contract

Validation is independent from production so a strategy cannot certify its own
summary. The default composite validator performs bounded structural, source,
causal, security, size, and reduction checks.

```csharp
namespace AgentKit;

public enum CompactionValidationIssueKind
{
    InvalidStructure,
    MissingSource,
    BrokenCausality,
    ForgedAuthority,
    ForgedEffect,
    ForgedTerminalOutcome,
    UnboundedContent,
    NonReducing,
    InvalidProvenance
}

public sealed record CompactionValidationIssue(
    CompactionValidationRuleId RuleId,
    CompactionValidationIssueKind Kind,
    string SafeMessage,
    ImmutableArray<SessionEntryId> SourceEntryIds);

public sealed record CompactionValidationEvidence(
    CompactionValidationRuleId RuleId,
    ContentHash EvidenceHash);

public sealed record CompactionValidationRequest(
    CompactionRequest Request,
    CompactionSourceSnapshot Source,
    CompactionCut Cut,
    CompactionCandidate Candidate);

public abstract record CompactionValidationResult;

public sealed record CompactionValidated(ValidatedCompaction Compaction)
    : CompactionValidationResult;

public sealed record CompactionValidationRejected(
    ImmutableArray<CompactionValidationIssue> Issues)
    : CompactionValidationResult;

public sealed record CompactionValidationFailed(CompactionFailure Failure)
    : CompactionValidationResult;

public interface ICompactionValidator
{
    ValueTask<CompactionValidationResult> ValidateAsync(
        CompactionValidationRequest request,
        CancellationToken cancellationToken = default);
}
```

Validators use `ValueTask` because the default checks operate on the bounded
in-memory candidate and source. A validator may await a policy or provenance
service but may not invoke a protected effect without normal authorization.
Activation rechecks the validation stamp and all immutable hashes; constructing
a `ValidatedCompaction` object is not authority.

The first-party validator re-derives the cut from the source before any other
check: covered identities must be exactly the ordered, duplicate-free source
prefix of their length; the covered range must span the first and last covered
sequences; the retained suffix start must equal the first retained entry's
sequence (or one past the last covered sequence when nothing is retained) and
lie strictly after the range; and the manifest's branch, source version,
operation context, context epoch, covered range, and retained suffix start
must agree with the request, source, and cut. Each violation is an
`InvalidStructure` issue, so neither a selector nor a strategy can persist a
record that describes coverage other than what was validated.

Authoritative checkpoint fields are copied or deterministically derived from
identified committed source records. Validators compare those fields with the
source manifest and reject invented grants, approvals, tool results, provider
success, goal transitions, and resource mutations. Generated prose cannot supply
those fields or override them.

Structural validation does not prove that arbitrary natural-language prose is
factually entailed by its sources. Summaries retain model-produced provenance
and uncertainty after validation. A host may add a bounded semantic evaluator,
but its score does not certify facts or grant authority. Source records remain
the evidence when a later operation needs an exact claim. The framework never
promotes a validated summary to system/developer instruction trust.

## Activation and optimistic concurrency

Potentially expensive production and validation occur outside the session append
lock. Activation is one idempotent protected mutation against the exact source
version.

```csharp
namespace AgentKit;

public enum CompactionCommitState
{
    NotAttempted,
    NotCommitted,
    Committed,
    Unknown
}

public sealed record CompactionActivationRequest(
    CompactionRequest Request,
    ValidatedCompaction Compaction,
    SessionVersion ExpectedVersion,
    IdempotencyKey IdempotencyKey,
    CompactionId? Supersedes);

public abstract record CompactionActivationResult;

public sealed record CompactionRecordActivated(CompactionRecord Record)
    : CompactionActivationResult;

public sealed record CompactionRecordConflict(
    SessionVersion ExpectedVersion,
    SessionVersion ActualVersion,
    CompactionManifest Manifest)
    : CompactionActivationResult;

public sealed record CompactionRecordActivationRejected(
    CompactionRejection Rejection)
    : CompactionActivationResult;

public sealed record CompactionRecordActivationFailed(
    CompactionFailure Failure)
    : CompactionActivationResult;

public sealed record CompactionRecordActivationCancelled(
    CompactionCancellation Cancellation)
    : CompactionActivationResult;

public interface ICompactionActivationCoordinator
{
    Task<CompactionActivationResult> ActivateAsync(
        CompactionActivationRequest request,
        SessionExecutionCapability session,
        SecurityGrant activationGrant,
        CancellationToken cancellationToken = default);
}
```

The session capability and activation grant are invocation-only and are never
embedded in a manifest, checkpoint, durable record, or event. The capability is
compiled from the immutable agent definition's selected session profile and
contains the exact keyed coordinator already validated for the run. The
activation coordinator rejects a capability whose session/profile binding does
not match the request; it never selects a store or coordinator itself. The
coordinator and selected session store validate the grant's audience, resource,
operation, input fingerprint, policy versions, expiry, revocation, and remaining
use immediately before append.

A version mismatch returns `CompactionRecordConflict`; it never retries against
a newer version or truncates concurrent entries. Cancellation after an append is
reconciled by the idempotency key. If commit state cannot be established, the
result reports `Unknown` and recovery resolves the durable record before any
second activation attempt.

`CompactionId` guarantees at-most-once activation of one logical checkpoint and
a stable answer for a replayed request. The first-party compactor appends under
the idempotency key `compaction:{CompactionId}`, but the appended entry carries
fresh manifest, entry, and timestamp evidence, so a store never replays the
original receipt for a retry; the store rejects the reused key with different
evidence. The compactor therefore reconciles by identity rather than by
receipt: when the source read observes a version newer than the request, when
the append conflicts, or when the append fails, it scans the branch after the
relevant tip for an active `CompactionSessionEntry` whose record carries the
same `CompactionId` and returns `CompactionSucceeded` with that committed record.
Only when reconciliation proves no record exists does an append failure surface
as a non-retryable `ActivationFailure`; a reconciliation read that itself fails
reports the commit state as unreconciled in the failure message. A retry must
replay the identical request (same `SourceVersion` and `SourceThrough`); reusing
a `CompactionId` for a different logical checkpoint is caller misuse and is not
mapped to the existing record.

Cancellation is a typed outcome, never a bare exception, once a request has been
accepted. `CompactionCancelled.CommitState` reports what the compactor
established: `NotAttempted` when the token fired before any activation append
was issued; otherwise the compactor reconciles the tip with a bounded read that
is deliberately not governed by the caller's token and reports `Committed`
together with the durable record, `NotCommitted` when the read proves no record
exists, or `Unknown` when the read itself was unavailable. A committed record is
never rolled back by cancellation.

`CompactionFailure.Retryable` is `true` only when the compactor has positive
evidence that the failure was transient (an unavailable source read, a drifted
continuation snapshot). An append failure that leaves no committed record is
non-retryable because the store does not distinguish deterministic rejection
from transport failure and the compactor must not drive an unbounded
compact-and-retry loop.

## Terminal outcomes and compactor contract

Every accepted attempt terminates in one typed result. Null checkpoint content,
a boolean, an exception string, or cancellation alone is not a terminal model.

```csharp
namespace AgentKit;

public enum CompactionRejectionKind
{
    Disabled,
    Unauthorized,
    NoSafeCut,
    SourceLimitExceeded,
    UnsupportedTrigger,
    UnsupportedStrategy,
    PolicyViolation
}

public sealed record CompactionRejection(
    CompactionRejectionKind Kind,
    string SafeMessage,
    ExtensionData Details);

public enum CompactionFailureKind
{
    SourceUnavailable,
    StrategyFailure,
    ValidationFailure,
    ActivationFailure,
    RequiredObservationUnavailable,
    ProtocolViolation,
    Unknown
}

public sealed record CompactionFailure(
    CompactionFailureKind Kind,
    string SafeMessage,
    bool Retryable,
    CompactionCommitState CommitState,
    ExtensionData Details);

public enum CompactionCancellationReason
{
    CallerCancelled,
    DeadlineExceeded,
    EngineStopping
}

public sealed record CompactionCancellation(
    CompactionCancellationReason Reason,
    CompactionCommitState CommitState,
    string SafeMessage);

public abstract record CompactionResult(CompactionOperationContext Context);

public sealed record CompactionSucceeded(
    CompactionOperationContext Context,
    CompactionRecord Record) : CompactionResult(Context);

public sealed record CompactionConflict(
    CompactionOperationContext Context,
    SessionVersion ExpectedVersion,
    SessionVersion ActualVersion,
    CompactionManifest Manifest) : CompactionResult(Context);

public sealed record CompactionCancelled(
    CompactionOperationContext Context,
    CompactionCancellation Cancellation) : CompactionResult(Context);

public sealed record CompactionRejected(
    CompactionOperationContext Context,
    CompactionRejection Rejection) : CompactionResult(Context);

public sealed record CompactionNotReducing(
    CompactionOperationContext Context,
    CompactionSizeEstimate Before,
    CompactionSizeEstimate After,
    double RequiredReductionRatio) : CompactionResult(Context);

public sealed record CompactionFailed(
    CompactionOperationContext Context,
    CompactionFailure Failure) : CompactionResult(Context);

public interface ICompactor
{
    Task<CompactionResult> CompactAsync(
        CompactionRequest request,
        SessionExecutionCapability session,
        BudgetExecutionCapability budget,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);
}
```

`ICompactor` returns `Task` because source loading, optional model production,
authorization, and session activation are inherently asynchronous. A trigger
does not guarantee a safe cut or useful reduction. The context assembler maps a
terminal non-reducing or no-safe-cut result to its typed context-limit outcome
when mandatory content still cannot fit.

`SessionExecutionCapability`, `BudgetExecutionCapability`, and
`HookDispatchContext` are separate, non-persisted invocation parameters. The
context assembler passes the exact session and budget capabilities compiled into
its run plan. An in-run caller passes its active hook lease; maintenance or
replay without a run hook lease passes `null`. None is embedded in a request,
source snapshot, manifest, checkpoint, record, result, cache key, activation
request, or event, and no implementation retains one after `CompactAsync`
completes.

## Observation contract

Compaction events are immutable, correlated, bounded observations. They do not
carry grants or raw checkpoint content. Required audit delivery is fail-closed;
best-effort telemetry may not influence activation.

```csharp
namespace AgentKit;

public enum CompactionOutcomeKind
{
    Succeeded,
    Conflict,
    Cancelled,
    Rejected,
    NotReducing,
    Failed
}

public sealed record CompactionOutcomeSummary(
    CompactionOutcomeKind Kind,
    CompactionManifestId? ManifestId,
    CompactionRecordVersion? RecordVersion,
    SessionVersion? ActivatedSessionVersion,
    string? SafeMessage);

public abstract record CompactionEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt);

public sealed record CompactionAttemptStartedEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt,
    CompactionTrigger Trigger) : CompactionEvent(Context, OccurredAt);

public sealed record CompactionCandidateValidatedEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt,
    CompactionManifest Manifest) : CompactionEvent(Context, OccurredAt);

public sealed record CompactionAttemptFinishedEvent(
    CompactionOperationContext Context,
    DateTimeOffset OccurredAt,
    CompactionOutcomeSummary Outcome) : CompactionEvent(Context, OccurredAt);

public interface ICompactionEventSink
{
    ValueTask PublishAsync(
        CompactionEvent compactionEvent,
        CancellationToken cancellationToken = default);
}

public abstract record CompactionEventDispatchResult;

public sealed record CompactionEventPublished : CompactionEventDispatchResult;

public sealed record RequiredCompactionEventUnavailable(
    CompactionEventSinkId SinkId,
    CompactionFailure Failure) : CompactionEventDispatchResult;

public interface ICompactionEventDispatcher
{
    ValueTask<CompactionEventDispatchResult> PublishAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionEvent compactionEvent,
        CancellationToken cancellationToken = default);
}
```

The default dispatcher sends events in deterministic registration order.
Required durable observations are written through an idempotent session/outbox
path before success is reported. A sink cannot mutate the candidate, veto after
commit, or receive content merely because it is registered. Diagnostic
exceptions and sensitive source text are redacted by the configured
observability profile. The dispatcher resolves only sinks associated with the
supplied compactor key; there is no unkeyed sink enumeration shared between
agents.

## First-party sealed implementation

The default class is a coordinator, not a service locator. Its constructor shows
the complete dependency direction:

```csharp
namespace AgentKit.Context.Compaction;

internal sealed class DefaultCompactor(
    ICompactionCutSelector cutSelector,
    ICompactionStrategyResolver strategies,
    ICompactionValidator validator,
    ICompactionActivationCoordinator activation,
    ISecurityAuthoritySelector securityAuthorities,
    IHookDispatcher hooks,
    ICompactionEventDispatcher events,
    IIdentifierGenerator<CompactionManifestId> manifestIds,
    TimeProvider timeProvider,
    ContextCompactionOptionsSnapshot options,
    ILogger<DefaultCompactor> logger) : ICompactor
{
}

internal sealed class ModelBackedCompactionStrategy(
    ComponentKey<ICompactor> compactorKey,
    ICompactionSummaryGeneratorResolver generators,
    ContextCompactionOptionsSnapshot options,
    TimeProvider timeProvider,
    ILogger<ModelBackedCompactionStrategy> logger) : ICompactionStrategy
{
}
```

`DefaultCompactor` selects a strategy only from the request's ordered typed keys
through the resolver bound to its immutable options snapshot. It never resolves
`IServiceProvider`, injects an unkeyed session coordinator, chooses a session
store, changes security profiles, or consults mutable global state. The
request's compactor key must match the snapshot key. `CompactAsync` validates
the supplied `SessionExecutionCapability` against the request before source
access and passes the same capability explicitly to activation. It likewise
validates the budget profile, identity, correlation, and scope, then passes that
capability through the selected strategy to any model-backed summary generator.
Every attempt reserves and settles its expected and actual work. The security
selector resolves the authority named by the captured authorization context;
separate bounded grants cover source read and activation write.

The first-party semantic cut selector, structural validator, extractive
strategy, and session-backed activation coordinator are sealed direct interface
implementations. AgentKit defines no mandatory compactor or strategy base class:
provider-backed and deterministic strategies have insufficient shared lifecycle
to justify inheritance.

## Typed options and DI surface

Package options are hard engine ceilings. A versioned profile and run override
may tighten them. The immutable request carries the effective result so option
monitors are never read mid-attempt.

```csharp
namespace AgentKit.Context.Compaction;

public static class CompactionStrategyKeys
{
    public static CompactionStrategyKey Extractive { get; } =
        new("agentkit.extractive");
}

public sealed class ContextCompactionOptions
{
    public int MaximumAttempts { get; set; } = 2;
    public int MaximumSourceEntries { get; set; } = 2_048;
    public long MaximumSourceBytes { get; set; } = 8 * 1_024 * 1_024;
    public int MaximumSummaryTokens { get; set; } = 2_048;
    public int MinimumRetainedEntries { get; set; } = 8;
    public int MaximumValidationIssues { get; set; } = 64;
    public double MinimumReductionRatio { get; set; } = 0.20;
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public bool PersistRejectedCandidates { get; set; }
}

public sealed record ContextCompactionOptionsSnapshot(
    ComponentKey<ICompactor> CompactorKey,
    int MaximumAttempts,
    int MaximumSourceEntries,
    long MaximumSourceBytes,
    int MaximumSummaryTokens,
    int MinimumRetainedEntries,
    int MaximumValidationIssues,
    double MinimumReductionRatio,
    TimeSpan AttemptTimeout,
    bool PersistRejectedCandidates);

public sealed class CompactionProfileOptions
{
    public CompactionProfileVersion Version { get; set; } = new(1);
    public bool Enabled { get; set; } = true;
    public List<CompactionStrategyKey> StrategyOrder { get; set; } =
        [CompactionStrategyKeys.Extractive];
    public bool AllowOversizedTurnRepair { get; set; }
}

public sealed record CompactionStrategyRegistration(
    CompactionStrategyDescriptor Descriptor,
    int Order,
    ImmutableArray<CompactionStrategyKey> Before,
    ImmutableArray<CompactionStrategyKey> After,
    ServiceLifetime Lifetime);

public sealed record CompactionSummaryGeneratorRegistration(
    CompactionSummaryGeneratorDescriptor Descriptor,
    ServiceLifetime Lifetime);

public enum CompactionEventDelivery
{
    Required,
    BestEffort
}

public sealed record CompactionEventSinkRegistration(
    CompactionEventSinkId Id,
    int Order,
    CompactionEventDelivery Delivery,
    ServiceLifetime Lifetime);

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentContextCompaction(
            ComponentKey<ICompactor> key,
            Action<ContextCompactionOptions>? configure = null) =>
            ContextCompactionRegistration.Add(services, key, configure);

        public IServiceCollection AddCompactionProfile(
            CompactionProfileKey profile,
            ComponentKey<ICompactor> compactor,
            Action<CompactionProfileOptions> configure) =>
            ContextCompactionRegistration.AddProfile(
                services,
                profile,
                compactor,
                configure);

        public IServiceCollection ReplaceCompactor<TCompactor>(
            ComponentKey<ICompactor> key)
            where TCompactor : class, ICompactor =>
            ContextCompactionRegistration.ReplaceCompactor<TCompactor>(
                services,
                key);

        public IServiceCollection AddCompactionStrategy<TStrategy>(
            ComponentKey<ICompactor> compactor,
            CompactionStrategyRegistration registration)
            where TStrategy : class, ICompactionStrategy =>
            ContextCompactionRegistration.AddStrategy<TStrategy>(
                services,
                compactor,
                registration);

        public IServiceCollection ReplaceCompactionStrategy<TStrategy>(
            ComponentKey<ICompactor> compactor,
            CompactionStrategyRegistration registration)
            where TStrategy : class, ICompactionStrategy =>
            ContextCompactionRegistration.ReplaceStrategy<TStrategy>(
                services,
                compactor,
                registration);

        public IServiceCollection AddCompactionSummaryGenerator<TGenerator>(
            ComponentKey<ICompactor> compactor,
            CompactionSummaryGeneratorRegistration registration)
            where TGenerator : class, ICompactionSummaryGenerator =>
            ContextCompactionRegistration.AddSummaryGenerator<TGenerator>(
                services,
                compactor,
                registration);

        public IServiceCollection
            ReplaceCompactionSummaryGenerator<TGenerator>(
                ComponentKey<ICompactor> compactor,
                CompactionSummaryGeneratorRegistration registration)
            where TGenerator : class, ICompactionSummaryGenerator =>
            ContextCompactionRegistration.ReplaceSummaryGenerator<TGenerator>(
                services,
                compactor,
                registration);

        public IServiceCollection
            ReplaceCompactionSummaryGeneratorResolver<TResolver>(
                ComponentKey<ICompactor> compactor)
            where TResolver : class, ICompactionSummaryGeneratorResolver =>
            ContextCompactionRegistration
                .ReplaceSummaryGeneratorResolver<TResolver>(
                    services,
                    compactor);

        public IServiceCollection ReplaceCompactionCutSelector<TSelector>(
            ComponentKey<ICompactor> compactor)
            where TSelector : class, ICompactionCutSelector =>
            ContextCompactionRegistration.ReplaceCutSelector<TSelector>(
                services,
                compactor);

        public IServiceCollection ReplaceCompactionValidator<TValidator>(
            ComponentKey<ICompactor> compactor)
            where TValidator : class, ICompactionValidator =>
            ContextCompactionRegistration.ReplaceValidator<TValidator>(
                services,
                compactor);

        public IServiceCollection
            ReplaceCompactionStrategyResolver<TResolver>(
                ComponentKey<ICompactor> compactor)
            where TResolver : class, ICompactionStrategyResolver =>
            ContextCompactionRegistration.ReplaceStrategyResolver<TResolver>(
                services,
                compactor);

        public IServiceCollection
            ReplaceCompactionActivationCoordinator<TCoordinator>(
                ComponentKey<ICompactor> compactor)
            where TCoordinator : class, ICompactionActivationCoordinator =>
            ContextCompactionRegistration.ReplaceActivation<TCoordinator>(
                services,
                compactor);

        public IServiceCollection AddCompactionEventSink<TSink>(
            ComponentKey<ICompactor> compactor,
            CompactionEventSinkRegistration registration)
            where TSink : class, ICompactionEventSink =>
            ContextCompactionRegistration.AddEventSink<TSink>(
                services,
                compactor,
                registration);

        public IServiceCollection ReplaceCompactionEventSink<TSink>(
            ComponentKey<ICompactor> compactor,
            CompactionEventSinkRegistration registration)
            where TSink : class, ICompactionEventSink =>
            ContextCompactionRegistration.ReplaceEventSink<TSink>(
                services,
                compactor,
                registration);

        public IServiceCollection
            ReplaceCompactionEventDispatcher<TDispatcher>(
                ComponentKey<ICompactor> compactor)
            where TDispatcher : class, ICompactionEventDispatcher =>
            ContextCompactionRegistration.ReplaceEventDispatcher<TDispatcher>(
                services,
                compactor);
    }
}
```

`AddAgentContextCompaction` is idempotent for the same component key, options,
and implementation. It validates the mutable binding options once, copies them
into an immutable `ContextCompactionOptionsSnapshot` bound to that exact key,
and uses `TryAddKeyedScoped` for the default compactor. The keyed factory passes
the matching snapshot and exact keyed collaborators; it never injects an unkeyed
`IOptions<ContextCompactionOptions>` or cross-key strategy collection.
Registration also adds one safe extractive strategy, one semantic cut selector,
one structural validator, and one session activation coordinator for that key.
It does not select or register an ambient session coordinator, and it does not
build or resolve a service provider.

Compactors, cut selectors, strategy resolvers, validators, activation
coordinators, summary-generator resolvers, and event dispatchers are singular
per compactor key. Their `Replace*` methods replace exactly that key and
contract. Strategies are additive by `CompactionStrategyKey`; summary generators
are additive by `CompactionSummaryGeneratorKey`; event sinks are additive by
`CompactionEventSinkId`. Equivalent duplicate descriptors are idempotent. A
duplicate identity with different type, version, order, lifetime, or options
fails validation unless its exact `Replace*` method is used.

Strategy and sink order is deterministic. Numeric order is applied first, then
`Before` and `After` constraints; cycles and ambiguous duplicate keys fail at
build. Registration cannot silently replace another agent's compactor, strategy,
validator, or observation path.

## Lifetimes, ownership, and cancellation

The default compactor and activation coordinator are run-scoped. They own only
attempt-local state and never retain the invocation's session capability or
session content after completion. Stateless cut selectors and validators may be
singleton when immutable and thread-safe. Strategy, summary-generator, and
event-sink registrations declare their lifetime. A model-backed or mutable
strategy or summary generator is scoped or transient unless it proves singleton
safety.

The container owns and disposes strategies, sinks, provider clients, session
collaborators, and hook infrastructure exactly once. A singleton may not capture
a run scope, hook activation lease, session snapshot, provider stream, security
grant, or options value that is allowed to vary per profile.

Cancellation flows through authorization, session reads, strategy production,
validation, required event delivery, and activation. `TimeProvider` supplies
deadlines and timestamps. Cancellation discards an unactivated candidate; it
cannot erase a committed record or covered source history. Activation with an
unknown commit state is reconciled by ID and idempotency key before retry.

## Build validation and unsupported behavior

Engine build validates every compaction-enabled context profile and agent
selection. It rejects:

- missing or ambiguous compactor, strategy, cut-selector, validator, activation,
  selected session capability, hook, security, identifier-generator, event, or
  `TimeProvider` collaborators;
- empty strategy order, unknown keys, duplicate identities, ordering cycles,
  invalid versions, or singleton-to-scoped capture;
- non-positive attempts, entries, bytes, tokens, retention, validation, or
  timeout bounds, and reduction ratios outside `(0, 1)`;
- model-backed strategies without a compatible selected model operation and
  provider-egress security path, or without exactly one registered summary
  generator matching the strategy descriptor;
- any compaction strategy or summary generator that depends on
  `IContextAssembler` or `ICompactor`, creating a composition cycle;
- oversized-turn repair selected on a strategy without that capability;
- durable activation against an ephemeral or incompatible session profile; and
- required audit delivery that cannot be made durable and idempotent.

Unsupported runtime behavior remains typed:

- disabled compaction, missing authority, unsupported trigger/strategy, source
  bounds, missing summary generator, generator limit/capability mismatch, policy
  rejection, and no safe cut return `CompactionRejected`;
- an insufficient reduction returns `CompactionNotReducing` and consumes one
  bounded attempt;
- a stale source version returns `CompactionConflict` without rebasing;
- cancellation returns `CompactionCancelled` with truthful commit state;
- unavailable source, failed strategy, invalid candidate, required observation,
  or activation failure returns `CompactionFailed`; and
- mandatory context that still cannot fit becomes the context component's typed
  `ContextLimitExceeded` outcome, never an unbounded compact-and-retry loop.

There is no silent history deletion, lossy fallback, fabricated summary,
implicit process-memory store, default remote model, cross-principal cache
reuse, or success synthesized from partial provider output. Security, session,
model, or audit unavailability fails closed before a protected effect whenever
commit has not already occurred.

## Determinism, conformance, and acceptance

For the same source version and hashes, policy/profile versions, strategy
descriptor and settings, cut-selector and validator versions, model capability
profile, and deterministic collaborators, cut selection, ordering, validation,
and activation decisions are reproducible. Model nondeterminism and usage are
recorded in `CompactionProducer`; they are never presented as deterministic
framework behavior.

Reusable conformance suites cover direct interface implementations and the
first-party defaults. They verify:

- every selected cut preserves tool, approval, deferred, input, and goal
  causality and retains the exact suffix;
- source, candidate, and validation hashes reject mutation and unknown schema
  versions;
- a forged grant, tool effect, provider success, or terminal state cannot pass
  validation;
- concurrent appends either survive unchanged or produce a typed conflict;
- repeated activation is idempotent and cancellation reports truthful commit
  state;
- non-reducing attempts stop at the configured bound;
- required events are durable while best-effort sink failure cannot corrupt
  state;
- hook context and security grants never appear in serialized records or caches;
- all singular defaults can be replaced per key and additive strategies/sinks
  remain isolated between two agents in one engine; and
- two agents with different session profiles pass only their compiled session
  capability to the same reusable compactor without cross-profile resolution;
- original covered history remains queryable, branchable, and recompacted.

## Related specifications

- [Context architecture](context.md)
- [Context compaction concept](../concepts/context-compaction.md)
- [Messages and history](messages-and-history.md)
- [Sessions](sessions.md)
- [Model and embedding providers](model-and-embedding-providers.md)
- [Permissions and human control](permissions-and-human-control.md)
- [Hooks and extensions](extensions.md)
- [Observability](observability.md)
- [Testing and evaluation](testing-and-evaluation.md)
