// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalAttempt"/>.</summary>
/// <param name="Id">The attempt identity.</param>
/// <param name="GoalId">The owning goal.</param>
/// <param name="AgentId">The executing agent.</param>
/// <param name="SessionId">The executing session.</param>
/// <param name="RunId">The executing run.</param>
/// <param name="Number">The one-based ordinal.</param>
/// <param name="Status">The attempt status.</param>
/// <param name="Reservation">The backing budget reservation.</param>
/// <param name="Outcome">The outcome reference, or <see langword="null"/>.</param>
/// <param name="StartedAt">The start instant.</param>
/// <param name="EndedAt">The end instant, or <see langword="null"/>.</param>
internal sealed record GoalAttemptDocument(
    Guid Id,
    Guid GoalId,
    Guid AgentId,
    Guid SessionId,
    Guid RunId,
    int Number,
    GoalAttemptStatus Status,
    GoalBudgetReservationDocument Reservation,
    GoalOutcomeDocument? Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt)
{
    /// <summary>Converts an attempt to its persisted form.</summary>
    /// <param name="value">The non-null attempt.</param>
    /// <returns>The document.</returns>
    internal static GoalAttemptDocument FromDomain(GoalAttempt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value,
            value.GoalId.Value,
            value.AgentId.Value,
            value.SessionId.Value,
            value.RunId.Value,
            value.Number,
            value.Status,
            GoalBudgetReservationDocument.FromDomain(value.Reservation),
            value.Outcome is null ? null : GoalOutcomeDocument.FromDomain(value.Outcome),
            value.StartedAt,
            value.EndedAt);
    }

    /// <summary>Restores the attempt, re-running its validation.</summary>
    /// <returns>The attempt.</returns>
    internal GoalAttempt ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Reservation);
        return new(
            new GoalAttemptId(Id),
            new GoalId(GoalId),
            new AgentId(AgentId),
            new SessionId(SessionId),
            new RunId(RunId),
            Number,
            Status,
            Reservation.ToDomain(),
            Outcome?.ToDomain(),
            StartedAt,
            EndedAt);
    }
}
