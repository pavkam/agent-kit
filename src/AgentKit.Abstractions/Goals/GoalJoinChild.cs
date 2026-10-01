// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one child as a join strategy sees it: recorded ordinal, durable status, and eligibility.</summary>
/// <remarks>
/// <see cref="Eligible"/> is decided by the coordinator after it validates the child's result shape and evidence against
/// the acceptance criteria; a strategy never re-reads child prose. <see cref="SettledSequence"/> is the durable join-inbox
/// position at which the child settled; it is the only ordering a timing-sensitive strategy may use, and no strategy reads
/// task completion order.
/// </remarks>
public sealed record GoalJoinChild
{
    /// <summary>Initializes a validated join child.</summary>
    /// <param name="ordinal">The positive recorded child ordinal.</param>
    /// <param name="childGoalId">The child goal.</param>
    /// <param name="status">The child's durable status.</param>
    /// <param name="eligible"><see langword="true"/> when the child completed with a validated result.</param>
    /// <param name="outcome">The settled attempt's outcome, or <see langword="null"/>.</param>
    /// <param name="settledSequence">The durable settlement sequence, or <see langword="null"/> while unsettled.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ordinal is not positive, the goal is default, the status is undefined, or the settlement sequence is not positive.</exception>
    /// <exception cref="ArgumentException">An eligible child is not completed or has no settlement sequence.</exception>
    public GoalJoinChild(int ordinal, GoalId childGoalId, GoalStatus status, bool eligible, GoalOutcomeReference? outcome, long? settledSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal);
        ArgumentOutOfRangeException.ThrowIfEqual(childGoalId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        if (settledSequence is { } sequence)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence, nameof(settledSequence));
        }

        ArgumentException.ThrowIfNotEqual(eligible && (status != GoalStatus.Completed || settledSequence is null), false, nameof(eligible));
        Ordinal = ordinal;
        ChildGoalId = childGoalId;
        Status = status;
        Eligible = eligible;
        Outcome = outcome;
        SettledSequence = settledSequence;
    }

    /// <summary>Gets the recorded child ordinal.</summary>
    public int Ordinal { get; }

    /// <summary>Gets the child goal.</summary>
    public GoalId ChildGoalId { get; }

    /// <summary>Gets the child's durable status.</summary>
    public GoalStatus Status { get; }

    /// <summary>Gets a value indicating whether the child completed with a validated result.</summary>
    public bool Eligible { get; }

    /// <summary>Gets the settled attempt's outcome, or <see langword="null"/>.</summary>
    public GoalOutcomeReference? Outcome { get; }

    /// <summary>Gets the durable settlement sequence, or <see langword="null"/> while unsettled.</summary>
    public long? SettledSequence { get; }

    /// <summary>Gets a value indicating whether the child can still change: it is neither completed nor failed nor cancelled nor blocked.</summary>
    public bool IsOpen => Status is GoalStatus.Proposed or GoalStatus.Ready or GoalStatus.Active or GoalStatus.Waiting;
}
