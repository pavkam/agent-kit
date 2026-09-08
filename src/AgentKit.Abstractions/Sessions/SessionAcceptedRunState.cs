// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the complete initial total state of one durably accepted session-lane run.</summary>
/// <remarks>The sole supported recovery procedure resolves the exact retained session profile and run configuration, verifies their fingerprints and versions, then drives the same accepted operation. Missing or mismatched retained material is typed unavailable or corrupt state; recovery never binds newer configuration.</remarks>
public sealed record SessionAcceptedRunState
{
    /// <summary>Initializes complete accepted state.</summary>
    /// <param name="address">The addressed session.</param><param name="executionLaneId">The owning lane.</param><param name="laneRevision">The positive lane revision installed by acceptance.</param><param name="correlation">The active operation and run.</param><param name="operationStateRevision">The first positive total-state revision.</param><param name="identity">The immutable admitted execution identity.</param><param name="authorization">The captured in-run authorization evidence.</param><param name="sessionProfile">The exact retained session profile.</param><param name="configuration">The exact retained run configuration and policy.</param><param name="previousCursor">The branch tip before acceptance.</param><param name="committedCursor">The branch tip after the atomic transition.</param><param name="promotionCutoff">The inclusive admission cutoff.</param><param name="initiatingAdmissionId">The admission that triggered this run.</param><param name="promotedAdmissionIds">The selected admissions in source order.</param><param name="materializedEntryIds">The corresponding initial history entry identities.</param><param name="materializedMessageIds">The corresponding message identities.</param><param name="initialTurnId">The turn receiving initial input.</param><param name="acceptedAt">The deterministic commit timestamp.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception><exception cref="ArgumentOutOfRangeException">An identity or revision is default.</exception><exception cref="ArgumentException">The correlations, authorization, cursors, or ordered identity collections are inconsistent.</exception>
    public SessionAcceptedRunState(SessionAddress address, ExecutionLaneId executionLaneId, SessionLaneRevision laneRevision,
        InRunOperationCorrelation correlation, OperationStateRevision operationStateRevision, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization, SessionProfileReference sessionProfile, RunConfigurationReference configuration,
        SessionBranchCursor previousCursor, SessionBranchCursor committedCursor, SessionSequence promotionCutoff,
        AdmissionId initiatingAdmissionId, ImmutableArray<AdmissionId> promotedAdmissionIds,
        ImmutableArray<SessionEntryId> materializedEntryIds,
        ImmutableArray<MessageId> materializedMessageIds, TurnId initialTurnId, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(address); ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(laneRevision, default); ArgumentNullException.ThrowIfNull(correlation);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization); ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(previousCursor); ArgumentNullException.ThrowIfNull(committedCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(initiatingAdmissionId, default);
        ArgumentException.ThrowIfInvalidPromotionAdmissions(promotedAdmissionIds);
        ArgumentException.ThrowIfDoesNotContain(promotedAdmissionIds, initiatingAdmissionId, nameof(initiatingAdmissionId));
        ArgumentException.ThrowIfDefaultEmptyOrDuplicate(materializedEntryIds);
        ArgumentException.ThrowIfDefaultEmptyOrDuplicate(materializedMessageIds);
        ArgumentOutOfRangeException.ThrowIfEqual(initialTurnId, default);
        ArgumentException.ThrowIfNotEqual(materializedEntryIds.Length, promotedAdmissionIds.Length, nameof(materializedEntryIds));
        ArgumentException.ThrowIfNotEqual(materializedMessageIds.Length, promotedAdmissionIds.Length, nameof(materializedMessageIds));
        ArgumentException.ThrowIfNotEqual(previousCursor.BranchId, committedCursor.BranchId, nameof(committedCursor));
        ArgumentException.ThrowIfNotEqual(committedCursor.LastEntryId is not null, true, nameof(committedCursor));
        var committedEntryId = committedCursor.LastEntryId!.Value;
        ArgumentException.ThrowIfNotEqual(previousCursor.LastEntryId == committedEntryId, false, nameof(committedCursor));
        ArgumentException.ThrowIfNotEqual(materializedEntryIds.Contains(committedEntryId), false, nameof(committedCursor));
        ArgumentException.ThrowIfNotEqual(
            previousCursor.LastEntryId is { } previousEntryId && materializedEntryIds.Contains(previousEntryId),
            false,
            nameof(materializedEntryIds));
        ArgumentException.ThrowIfNotEqual(correlation.TurnId, initialTurnId, nameof(correlation));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, address.AgentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, address.SessionId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.Correlation, correlation, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.ConfigurationVersion, configuration.ConfigurationVersion, nameof(authorization));
        Address = address; ExecutionLaneId = executionLaneId; LaneRevision = laneRevision; Correlation = correlation;
        OperationStateRevision = operationStateRevision; Identity = identity; Authorization = authorization;
        SessionProfile = sessionProfile; Configuration = configuration; PreviousCursor = previousCursor; CommittedCursor = committedCursor;
        PromotionCutoff = promotionCutoff; InitiatingAdmissionId = initiatingAdmissionId;
        PromotedAdmissionIds = promotedAdmissionIds; MaterializedEntryIds = materializedEntryIds;
        MaterializedMessageIds = materializedMessageIds; InitialTurnId = initialTurnId; AcceptedAt = acceptedAt;
        State = DurableOperationState.Accepted;
    }
    /// <summary>Gets the session address.</summary><value>The exact agent/session pair.</value>
    public SessionAddress Address { get; }
    /// <summary>Gets the lane.</summary><value>The non-default owning lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the lane revision.</summary><value>The positive revision installed atomically.</value>
    public SessionLaneRevision LaneRevision { get; }
    /// <summary>Gets the active operation correlation.</summary><value>The exact operation, run, and initial turn.</value>
    public InRunOperationCorrelation Correlation { get; }
    /// <summary>Gets the total-state revision.</summary><value>The first positive accepted-state revision.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets the admitted execution identity.</summary><value>The immutable identity reused during promotion.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets in-run authorization evidence.</summary><value>The exact captured security snapshot for the active run.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the retained session profile.</summary><value>The exact profile key/version required for recovery.</value>
    public SessionProfileReference SessionProfile { get; }
    /// <summary>Gets the retained run configuration.</summary><value>The exact versioned policy/configuration reference required for recovery.</value>
    public RunConfigurationReference Configuration { get; }
    /// <summary>Gets the pre-acceptance branch cursor.</summary><value>The tip revalidated before commit.</value>
    public SessionBranchCursor PreviousCursor { get; }
    /// <summary>Gets the committed branch cursor.</summary><value>The tip after all initial entries committed.</value>
    public SessionBranchCursor CommittedCursor { get; }
    /// <summary>Gets the promotion cutoff.</summary><value>The inclusive durable admission sequence used by selection.</value>
    public SessionSequence PromotionCutoff { get; }
    /// <summary>Gets the admission that triggered the accepted run.</summary><value>A non-default member of <see cref="PromotedAdmissionIds"/>.</value>
    public AdmissionId InitiatingAdmissionId { get; }
    /// <summary>Gets promoted admission identities.</summary><value>The exact nonempty source-ordered selection.</value>
    public ImmutableArray<AdmissionId> PromotedAdmissionIds { get; }
    /// <summary>Gets materialized history entry identities.</summary><value>One entry identity for each promoted admission in source order.</value>
    public ImmutableArray<SessionEntryId> MaterializedEntryIds { get; }
    /// <summary>Gets materialized message identities.</summary><value>One message identity for each promoted admission in source order.</value>
    public ImmutableArray<MessageId> MaterializedMessageIds { get; }
    /// <summary>Gets the initial turn.</summary><value>The non-default turn receiving all initial materialized input.</value>
    public TurnId InitialTurnId { get; }
    /// <summary>Gets the acceptance time.</summary><value>The deterministic timestamp committed with the state.</value>
    public DateTimeOffset AcceptedAt { get; }
    /// <summary>Gets the lifecycle state.</summary><value>Always <see cref="DurableOperationState.Accepted"/> until a later coordinated transition replaces total state.</value>
    public DurableOperationState State { get; }
}
