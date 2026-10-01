// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one page of goal aggregates.</summary>
public sealed record GoalPage: GoalPageResult
{
    /// <summary>Initializes a page.</summary>
    /// <param name="items">The aggregates in cursor order; empty when the cursor is exhausted.</param>
    /// <param name="next">The cursor to pass for the next page, or <see langword="null"/> when this page is the last.</param>
    /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="next"/> is present and not positive.</exception>
    public GoalPage(ImmutableArray<GoalRecord> items, long? next)
    {
        ArgumentException.ThrowIfDefault(items);
        ArgumentException.ThrowIfContainsNull(items);
        if (next is { } cursor)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cursor, nameof(next));
        }

        Items = items;
        Next = next;
    }

    /// <summary>Gets the aggregates in cursor order.</summary>
    public ImmutableArray<GoalRecord> Items { get; }

    /// <summary>Gets the cursor for the next page, or <see langword="null"/> when this page is the last.</summary>
    public long? Next { get; }

    /// <inheritdoc/>
    public bool Equals(GoalPage? other) => other is not null && Next == other.Next && Items.SequenceEqual(other.Items);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Next);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
