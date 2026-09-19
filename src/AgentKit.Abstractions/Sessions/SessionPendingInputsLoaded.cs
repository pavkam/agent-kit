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
    /// <exception cref="ArgumentException"><paramref name="pending"/> contains a null, duplicate, promoted, or mismatched-address entry, or is not in ascending admitted-sequence order.</exception>
    public SessionPendingInputsLoaded(
        AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId, ImmutableArray<AdmittedInput> pending)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
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
}
