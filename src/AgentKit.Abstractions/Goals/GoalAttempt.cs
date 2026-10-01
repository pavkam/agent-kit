// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one execution attempt against a goal.</summary>
/// <remarks>Retrying creates a new attempt with the next <see cref="Number"/> and never erases earlier evidence. <see cref="AgentId"/>, <see cref="SessionId"/>, and <see cref="RunId"/> name where the attempt executes, which for a delegated child is the target agent's own session and the run identity reserved when the attempt was claimed. The engine admits the run after the claim and assigns its own identity, so the settled <see cref="GoalOutcomeReference.RunId"/> is the authoritative identity of the run that actually executed; claiming first is what guarantees a replayed or duplicated intent never starts a second run.</remarks>
public sealed record GoalAttempt
{
    /// <summary>Initializes a validated attempt.</summary>
    /// <param name="id">The attempt identity.</param>
    /// <param name="goalId">The owning goal.</param>
    /// <param name="agentId">The executing agent.</param>
    /// <param name="sessionId">The executing session.</param>
    /// <param name="runId">The executing run.</param>
    /// <param name="number">The positive one-based attempt ordinal.</param>
    /// <param name="status">The defined attempt status.</param>
    /// <param name="reservation">The budget reservation backing the attempt.</param>
    /// <param name="outcome">The outcome reference, or <see langword="null"/> while running or when none exists.</param>
    /// <param name="startedAt">The start instant.</param>
    /// <param name="endedAt">The end instant, or <see langword="null"/> while running.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, the number is not positive, the status is undefined, or the end precedes the start.</exception>
    /// <exception cref="ArgumentException">A running attempt carries an end instant or outcome, or a settled attempt has no end instant.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public GoalAttempt(
        GoalAttemptId id,
        GoalId goalId,
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        int number,
        GoalAttemptStatus status,
        GoalBudgetReservation reservation,
        GoalOutcomeReference? outcome,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentException.ThrowIfNotEqual(status is GoalAttemptStatus.Running && (endedAt is not null || outcome is not null), false, nameof(status));
        ArgumentException.ThrowIfNotEqual(status is not GoalAttemptStatus.Running && endedAt is null, false, nameof(endedAt));
        if (endedAt is { } ended)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(ended, startedAt, nameof(endedAt));
        }

        Id = id;
        GoalId = goalId;
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        Number = number;
        Status = status;
        Reservation = reservation;
        Outcome = outcome;
        StartedAt = startedAt;
        EndedAt = endedAt;
    }

    /// <summary>Gets the attempt identity.</summary>
    public GoalAttemptId Id { get; }

    /// <summary>Gets the owning goal.</summary>
    public GoalId GoalId { get; }

    /// <summary>Gets the executing agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the executing session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the executing run.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the one-based attempt ordinal.</summary>
    public int Number { get; }

    /// <summary>Gets the attempt status.</summary>
    public GoalAttemptStatus Status { get; }

    /// <summary>Gets the budget reservation backing the attempt.</summary>
    public GoalBudgetReservation Reservation { get; }

    /// <summary>Gets the outcome reference, or <see langword="null"/>.</summary>
    public GoalOutcomeReference? Outcome { get; }

    /// <summary>Gets the start instant.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets the end instant, or <see langword="null"/> while running.</summary>
    public DateTimeOffset? EndedAt { get; }
}
