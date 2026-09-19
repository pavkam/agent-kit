// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one execution lane's currently pending, not-yet-promoted admissions in admission order.</summary>
/// <remarks>The evidence is a snapshot at read time; a concurrent admission or promotion may change eligibility before a caller acts on it, so a proposed promotion is always revalidated atomically at commit time.</remarks>
public sealed record SessionPendingInputsLoaded: SessionPendingInputsResult
{
    /// <summary>Initializes pending-admission discovery evidence.</summary>
    /// <param name="agentId">The non-default agent that owns every reported admission.</param>
    /// <param name="sessionId">The non-default session that owns every reported admission.</param>
    /// <param name="executionLaneId">The non-default lane that owns every reported admission.</param>
    /// <param name="pending">The immutable, possibly empty collection of pending admissions in ascending admitted-sequence order.</param>
    /// <param name="revision">The lane's current live revision, observed atomically with <paramref name="pending"/>.</param>
    /// <param name="branchCursor">The lane's current live branch cursor, observed atomically with <paramref name="pending"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="branchCursor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revision"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="pending"/> contains a null, duplicate, promoted, or mismatched-address entry, or is not in ascending admitted-sequence order.</exception>
    /// <remarks>
    /// <paramref name="revision"/> and <paramref name="branchCursor"/> are the lane's live values, not an accepted
    /// run's acceptance-time snapshot; a caller building a mid-run <see cref="SessionInputPromotionRequest"/> has no
    /// before-run context available to call <see cref="SessionLaneStateRequest"/>, so this is the sole discovery
    /// path for that revision and cursor while a run is accepted.
    /// </remarks>
    public SessionPendingInputsLoaded(
        AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId, ImmutableArray<AdmittedInput> pending,
        SessionLaneRevision revision, SessionBranchCursor branchCursor)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(revision, default);
        ArgumentNullException.ThrowIfNull(branchCursor);
        var normalized = pending.IsDefault ? [] : pending;
        if (normalized.Length > 0)
        {
            ArgumentException.ThrowIfInvalidPromotionEligibleInputs(
                normalized, agentId, sessionId, executionLaneId, new SessionSequence(long.MaxValue), nameof(pending));
            for (var index = 1; index < normalized.Length; index++)
            {
                if (normalized[index].AdmittedSequence.Value <= normalized[index - 1].AdmittedSequence.Value)
                {
                    throw new ArgumentException("Pending admissions must be reported in ascending admitted-sequence order.", nameof(pending));
                }
            }
        }

        AgentId = agentId;
        SessionId = sessionId;
        ExecutionLaneId = executionLaneId;
        Pending = normalized;
        Revision = revision;
        BranchCursor = branchCursor;
    }

    /// <summary>Gets the agent that owns every reported admission.</summary>
    /// <value>A non-default identity shared by every entry in <see cref="Pending"/>.</value>
    public AgentId AgentId { get; }

    /// <summary>Gets the session that owns every reported admission.</summary>
    /// <value>A non-default identity shared by every entry in <see cref="Pending"/>.</value>
    public SessionId SessionId { get; }

    /// <summary>Gets the lane that owns every reported admission.</summary>
    /// <value>A non-default identity shared by every entry in <see cref="Pending"/>.</value>
    public ExecutionLaneId ExecutionLaneId { get; }

    /// <summary>Gets the currently pending admissions, in ascending admitted-sequence order.</summary>
    /// <value>A possibly empty immutable collection; each entry's <see cref="AdmittedInput.PromotedSequence"/> is <see langword="null"/>.</value>
    public ImmutableArray<AdmittedInput> Pending { get; }

    /// <summary>Gets the lane's current live revision.</summary>
    /// <value>The positive revision observed atomically with <see cref="Pending"/>; usable as a mid-run promotion's expected lane revision.</value>
    public SessionLaneRevision Revision { get; }

    /// <summary>Gets the lane's current live branch cursor.</summary>
    /// <value>The tip observed atomically with <see cref="Pending"/>; usable as a mid-run promotion's expected branch cursor.</value>
    public SessionBranchCursor BranchCursor { get; }
}
