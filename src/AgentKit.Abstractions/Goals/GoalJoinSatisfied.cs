// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a join that decided from the durable child set.</summary>
/// <remarks>
/// <see cref="Results"/> are the children the strategy yielded, in recorded ordinal order. <see cref="Winner"/> names the one
/// chosen child for first-success strategies. <see cref="CutoffSequence"/> is the highest durable settlement sequence the
/// decision considered, so replaying the same durable state reproduces the same decision exactly.
/// </remarks>
public sealed record GoalJoinSatisfied: GoalJoinDecision
{
    /// <summary>Initializes a satisfied decision.</summary>
    /// <param name="results">The yielded children in ordinal order.</param>
    /// <param name="winner">The chosen child, or <see langword="null"/> for strategies without a single winner.</param>
    /// <param name="cutoffSequence">The non-negative highest settlement sequence considered.</param>
    /// <exception cref="ArgumentException"><paramref name="results"/> is default, or a present winner is not among the results.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cutoffSequence"/> is negative.</exception>
    public GoalJoinSatisfied(ImmutableArray<GoalJoinChild> results, GoalId? winner, long cutoffSequence)
    {
        ArgumentException.ThrowIfDefault(results);
        ArgumentException.ThrowIfContainsNull(results);
        if (winner is { } chosen)
        {
            ArgumentException.ThrowIfNotEqual(results.Any(result => result.ChildGoalId == chosen), true, nameof(winner));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(cutoffSequence);
        Results = results;
        Winner = winner;
        CutoffSequence = cutoffSequence;
    }

    /// <summary>Gets the yielded children in ordinal order.</summary>
    public ImmutableArray<GoalJoinChild> Results { get; }

    /// <summary>Gets the chosen child, or <see langword="null"/>.</summary>
    public GoalId? Winner { get; }

    /// <summary>Gets the highest settlement sequence the decision considered.</summary>
    public long CutoffSequence { get; }

    /// <inheritdoc/>
    public bool Equals(GoalJoinSatisfied? other) =>
        other is not null && Winner == other.Winner && CutoffSequence == other.CutoffSequence && Results.SequenceEqual(other.Results);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Winner);
        hash.Add(CutoffSequence);
        foreach (var result in Results)
        {
            hash.Add(result);
        }

        return hash.ToHashCode();
    }
}
