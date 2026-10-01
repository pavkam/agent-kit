// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Settles the running attempt as the goal leaves <see cref="GoalStatus.Active"/> or <see cref="GoalStatus.Waiting"/> for a settled status.</summary>
/// <remarks>The outcome is a reference to what the attempt produced; it never proves the result is correct. A cancelled or lost attempt may carry no outcome.</remarks>
public sealed record GoalAttemptSettlement: GoalAttemptChange
{
    /// <summary>Initializes a settlement change.</summary>
    /// <param name="attemptId">The running attempt being settled.</param>
    /// <param name="status">The terminal attempt status; <see cref="GoalAttemptStatus.Running"/> is rejected.</param>
    /// <param name="outcome">The outcome reference, or <see langword="null"/> when none exists.</param>
    /// <param name="endedAt">The settlement instant.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attemptId"/> is default, or <paramref name="status"/> is undefined or running.</exception>
    public GoalAttemptSettlement(GoalAttemptId attemptId, GoalAttemptStatus status, GoalOutcomeReference? outcome, DateTimeOffset endedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(attemptId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfEqual(status, GoalAttemptStatus.Running);
        AttemptId = attemptId;
        Status = status;
        Outcome = outcome;
        EndedAt = endedAt;
    }

    /// <summary>Gets the running attempt being settled.</summary>
    public GoalAttemptId AttemptId { get; }

    /// <summary>Gets the terminal attempt status.</summary>
    public GoalAttemptStatus Status { get; }

    /// <summary>Gets the outcome reference, or <see langword="null"/>.</summary>
    public GoalOutcomeReference? Outcome { get; }

    /// <summary>Gets the settlement instant.</summary>
    public DateTimeOffset EndedAt { get; }
}
