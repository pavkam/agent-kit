// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the pure result of applying one transition request to one stored goal aggregate.</summary>
/// <remarks>Reduction has no side effects: the adapter persists <see cref="Record"/> only for <see cref="GoalReductionKind.Applied"/>, so every adapter that runs the same reducer agrees on status, attempt, and replay semantics.</remarks>
internal sealed class GoalReduction
{
    private GoalReduction(GoalReductionKind kind, GoalRecord? record, GoalStoreFailure? failure)
    {
        Kind = kind;
        Record = record;
        Failure = failure;
    }

    /// <summary>Gets the outcome class.</summary>
    internal GoalReductionKind Kind { get; }

    /// <summary>Gets the aggregate after reduction, or <see langword="null"/> when rejected.</summary>
    internal GoalRecord? Record { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> unless rejected.</summary>
    internal GoalStoreFailure? Failure { get; }

    /// <summary>Creates an applied result.</summary>
    /// <param name="record">The new aggregate to persist.</param>
    /// <returns>An applied reduction.</returns>
    internal static GoalReduction Applied(GoalRecord record)
    {
        Debug.Assert(record is not null, "An applied reduction carries its new aggregate.");
        return new(GoalReductionKind.Applied, record, null);
    }

    /// <summary>Creates a replayed result.</summary>
    /// <param name="record">The stored aggregate an equivalent earlier request produced.</param>
    /// <returns>A replayed reduction.</returns>
    internal static GoalReduction Replayed(GoalRecord record)
    {
        Debug.Assert(record is not null, "A replayed reduction carries the stored aggregate.");
        return new(GoalReductionKind.Replayed, record, null);
    }

    /// <summary>Creates a rejected result.</summary>
    /// <param name="kind">The failure class.</param>
    /// <param name="message">The content-safe explanation.</param>
    /// <returns>A rejected reduction.</returns>
    internal static GoalReduction Rejected(GoalStoreFailureKind kind, string message) =>
        new(GoalReductionKind.Rejected, null, new GoalStoreFailure(kind, message));
}
