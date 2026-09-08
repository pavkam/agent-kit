// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one atomic idle-lane transition from admitted input to complete accepted run state.</summary>
public sealed record SessionRunStartRequest
{
    /// <summary>Initializes a complete run-acceptance proposal.</summary>
    /// <param name="context">The lane-bound before-run session context.</param><param name="initiatingAdmissionId">The admission that triggered this start.</param><param name="selectedAdmissionIds">The exact nonempty source-ordered selection.</param><param name="promotionCutoff">The inclusive admitted-input cutoff.</param><param name="expectedLaneRevision">The observed idle lane revision.</param><param name="expectedVersion">The observed canonical whole-session version.</param><param name="branchCursor">The exact observed branch tip.</param><param name="expectedFencingToken">The distributed fence, or null for a process-local store.</param><param name="runId">The new run identity.</param><param name="initialTurnId">The initial turn identity.</param><param name="promotionEntryId">The reserved promotion-marker entry identity.</param><param name="entryIds">One reserved history-entry ID per selected admission.</param><param name="messageIds">One reserved message ID per selected admission.</param><param name="acceptedEntryId">The reserved accepted-operation entry identity.</param><param name="operationStateRevision">The first total-state revision.</param><param name="sessionProfile">The retained session profile.</param><param name="configuration">The retained exact run configuration.</param><param name="inRunAuthorization">Captured authorization matching the installed in-run correlation.</param><param name="acceptedAt">The deterministic acceptance time.</param><param name="idempotencyKey">The exact start idempotency key.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception><exception cref="ArgumentOutOfRangeException">An identity or revision is default.</exception><exception cref="ArgumentException">The context or ordered selection evidence is inconsistent.</exception>
    public SessionRunStartRequest(SessionOperationContext context, AdmissionId initiatingAdmissionId,
        ImmutableArray<AdmissionId> selectedAdmissionIds, SessionSequence promotionCutoff, SessionLaneRevision expectedLaneRevision,
        SessionVersion expectedVersion, SessionBranchCursor branchCursor, FencingToken? expectedFencingToken, RunId runId,
        TurnId initialTurnId, SessionEntryId promotionEntryId, ImmutableArray<SessionEntryId> entryIds,
        ImmutableArray<MessageId> messageIds, SessionEntryId acceptedEntryId,
        OperationStateRevision operationStateRevision, SessionProfileReference sessionProfile,
        RunConfigurationReference configuration, SecurityAuthorizationContext inRunAuthorization,
        DateTimeOffset acceptedAt, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        ArgumentException.ThrowIfSessionContextNotBeforeRun(context);
        ArgumentOutOfRangeException.ThrowIfEqual(initiatingAdmissionId, default); ArgumentException.ThrowIfInvalidPromotionAdmissions(selectedAdmissionIds);
        ArgumentException.ThrowIfDoesNotContain(selectedAdmissionIds, initiatingAdmissionId, nameof(selectedAdmissionIds));
        ArgumentOutOfRangeException.ThrowIfEqual(expectedLaneRevision, default); ArgumentNullException.ThrowIfNull(branchCursor);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default); ArgumentOutOfRangeException.ThrowIfEqual(initialTurnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(promotionEntryId, default); ArgumentOutOfRangeException.ThrowIfEqual(acceptedEntryId, default);
        ArgumentException.ThrowIfDefaultOrEmpty(entryIds); ArgumentException.ThrowIfDefaultOrEmpty(messageIds);
        ArgumentException.ThrowIfDefaultEmptyOrDuplicate(entryIds); ArgumentException.ThrowIfDefaultEmptyOrDuplicate(messageIds);
        ArgumentException.ThrowIfNotEqual(entryIds.Length, selectedAdmissionIds.Length, nameof(entryIds));
        ArgumentException.ThrowIfNotEqual(messageIds.Length, selectedAdmissionIds.Length, nameof(messageIds));
        ArgumentException.ThrowIfNotEqual(entryIds.Contains(promotionEntryId), false, nameof(entryIds));
        ArgumentException.ThrowIfNotEqual(entryIds.Contains(acceptedEntryId), false, nameof(entryIds));
        ArgumentException.ThrowIfNotEqual(promotionEntryId == acceptedEntryId, false, nameof(acceptedEntryId));
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(inRunAuthorization);
        ArgumentException.ThrowIfNotEqual(
            context.Authorization.ConfigurationVersion, configuration.ConfigurationVersion, nameof(context));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        var expectedCorrelation = new InRunOperationCorrelation(context.Correlation.OperationId, runId, initialTurnId);
        ArgumentException.ThrowIfNotEqual(inRunAuthorization.Scope.Correlation, expectedCorrelation, nameof(inRunAuthorization));
        ArgumentException.ThrowIfNotEqual(inRunAuthorization.Scope.AgentId, context.AgentId, nameof(inRunAuthorization));
        ArgumentException.ThrowIfNotEqual(inRunAuthorization.Scope.SessionId, context.SessionId, nameof(inRunAuthorization));
        ArgumentException.ThrowIfNotEqual(inRunAuthorization.Identity, context.Identity, nameof(inRunAuthorization));
        ArgumentException.ThrowIfNotEqual(inRunAuthorization.ConfigurationVersion, configuration.ConfigurationVersion, nameof(inRunAuthorization));
        Context = context; InitiatingAdmissionId = initiatingAdmissionId; SelectedAdmissionIds = selectedAdmissionIds;
        PromotionCutoff = promotionCutoff; ExpectedLaneRevision = expectedLaneRevision; ExpectedVersion = expectedVersion;
        BranchCursor = branchCursor; ExpectedFencingToken = expectedFencingToken; RunId = runId; InitialTurnId = initialTurnId;
        PromotionEntryId = promotionEntryId; EntryIds = entryIds; MessageIds = messageIds; AcceptedEntryId = acceptedEntryId; OperationStateRevision = operationStateRevision;
        SessionProfile = sessionProfile; Configuration = configuration; InRunAuthorization = inRunAuthorization;
        AcceptedAt = acceptedAt; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the before-run context.</summary><value>The authorized lane-bound trigger context.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the initiating admission.</summary><value>The exact selected trigger input.</value>
    public AdmissionId InitiatingAdmissionId { get; }
    /// <summary>Gets selected admissions.</summary><value>The nonempty source-ordered exact plan.</value>
    public ImmutableArray<AdmissionId> SelectedAdmissionIds { get; }
    /// <summary>Gets the cutoff.</summary><value>The inclusive admission sequence.</value>
    public SessionSequence PromotionCutoff { get; }
    /// <summary>Gets expected lane revision.</summary><value>The positive idle-lane version.</value>
    public SessionLaneRevision ExpectedLaneRevision { get; }
    /// <summary>Gets expected canonical session version.</summary><value>Whole-session optimistic-concurrency evidence.</value>
    public SessionVersion ExpectedVersion { get; }
    /// <summary>Gets expected branch cursor.</summary><value>The exact selected tip.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets expected distributed fence.</summary><value>A fence token, or null for local ownership.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets new run identity.</summary><value>The new unique run.</value>
    public RunId RunId { get; }
    /// <summary>Gets initial turn identity.</summary><value>The turn receiving promoted input.</value>
    public TurnId InitialTurnId { get; }
    /// <summary>Gets the promotion-marker entry identity.</summary><value>The non-default ID committed before materialized messages.</value>
    public SessionEntryId PromotionEntryId { get; }
    /// <summary>Gets reserved entry IDs.</summary><value>One ID per selected input.</value>
    public ImmutableArray<SessionEntryId> EntryIds { get; }
    /// <summary>Gets reserved message IDs.</summary><value>One ID per selected input.</value>
    public ImmutableArray<MessageId> MessageIds { get; }
    /// <summary>Gets the accepted-operation entry identity.</summary><value>The non-default ID that becomes the committed branch cursor.</value>
    public SessionEntryId AcceptedEntryId { get; }
    /// <summary>Gets first total-state revision.</summary><value>A positive revision installed with acceptance.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets retained session profile.</summary><value>The exact recovery profile.</value>
    public SessionProfileReference SessionProfile { get; }
    /// <summary>Gets retained run configuration.</summary><value>The exact recovery configuration and policy.</value>
    public RunConfigurationReference Configuration { get; }
    /// <summary>Gets in-run authorization evidence.</summary><value>The captured context matching the installed run.</value>
    public SecurityAuthorizationContext InRunAuthorization { get; }
    /// <summary>Gets acceptance time.</summary><value>The deterministic commit timestamp.</value>
    public DateTimeOffset AcceptedAt { get; }
    /// <summary>Gets start idempotency key.</summary><value>The key reconciled before state-version checks.</value>
    public IdempotencyKey IdempotencyKey { get; }
}
