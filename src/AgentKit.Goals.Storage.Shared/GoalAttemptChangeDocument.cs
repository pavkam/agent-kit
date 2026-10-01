// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalAttemptChange"/>.</summary>
/// <param name="Start">The attempt being started, or <see langword="null"/> for a settlement.</param>
/// <param name="AttemptId">The attempt being settled, or <see langword="null"/> for a start.</param>
/// <param name="Status">The settlement status, or <see langword="null"/> for a start.</param>
/// <param name="Outcome">The settlement outcome, or <see langword="null"/>.</param>
/// <param name="EndedAt">The settlement instant, or <see langword="null"/> for a start.</param>
internal sealed record GoalAttemptChangeDocument(
    GoalAttemptDocument? Start,
    Guid? AttemptId,
    GoalAttemptStatus? Status,
    GoalOutcomeDocument? Outcome,
    DateTimeOffset? EndedAt)
{
    /// <summary>Converts an attempt change to its persisted form.</summary>
    /// <param name="value">The non-null change.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The change is of an unsupported kind.</exception>
    internal static GoalAttemptChangeDocument FromDomain(GoalAttemptChange value) => value switch
    {
        GoalAttemptStart start => new(GoalAttemptDocument.FromDomain(start.Attempt), null, null, null, null),
        GoalAttemptSettlement settlement => new(
            null,
            settlement.AttemptId.Value,
            settlement.Status,
            settlement.Outcome is null ? null : GoalOutcomeDocument.FromDomain(settlement.Outcome),
            settlement.EndedAt),
        _ => throw new ArgumentException("The attempt change is of an unsupported kind.", nameof(value)),
    };

    /// <summary>Restores the change, re-running its validation.</summary>
    /// <returns>The change.</returns>
    /// <exception cref="ArgumentException">The document names neither a start nor a complete settlement.</exception>
    internal GoalAttemptChange ToDomain() => (Start, AttemptId, Status, EndedAt) switch
    {
        ({ } start, null, null, null) => new GoalAttemptStart(start.ToDomain()),
        (null, { } attempt, { } status, { } ended) => new GoalAttemptSettlement(new GoalAttemptId(attempt), status, Outcome?.ToDomain(), ended),
        _ => throw new ArgumentException("The persisted attempt change is neither a start nor a complete settlement."),
    };
}
