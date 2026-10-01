// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a join that cannot decide yet because children it depends on are still open.</summary>
public sealed record GoalJoinPending: GoalJoinDecision
{
    /// <summary>Initializes a pending decision.</summary>
    /// <param name="awaiting">The open children the decision depends on, in ordinal order.</param>
    /// <exception cref="ArgumentException"><paramref name="awaiting"/> is default.</exception>
    public GoalJoinPending(ImmutableArray<GoalId> awaiting)
    {
        ArgumentException.ThrowIfDefault(awaiting);
        Awaiting = awaiting;
    }

    /// <summary>Gets the open children the decision depends on.</summary>
    public ImmutableArray<GoalId> Awaiting { get; }

    /// <inheritdoc/>
    public bool Equals(GoalJoinPending? other) => other is not null && Awaiting.SequenceEqual(other.Awaiting);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var goal in Awaiting)
        {
            hash.Add(goal);
        }

        return hash.ToHashCode();
    }
}
