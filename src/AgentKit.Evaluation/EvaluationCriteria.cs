// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Holds the immutable expected criteria of one evaluation case, at most one per criterion kind.</summary>
public sealed record EvaluationCriteria
{
    /// <summary>Gets the criteria of a case that expects nothing beyond running to a terminal result.</summary>
    public static EvaluationCriteria Empty { get; } = new([]);

    /// <summary>Initializes validated criteria.</summary>
    /// <param name="items">The criteria in authored order; an empty array is valid.</param>
    /// <exception cref="ArgumentException"><paramref name="items"/> is the default array, contains null, or repeats a criterion key.</exception>
    public EvaluationCriteria(ImmutableArray<EvaluationCriterion> items)
    {
        ArgumentException.ThrowIfDefault(items);
        ArgumentException.ThrowIfContainsNull(items);
        HashSet<EvaluationCriterionKey> keys = [];
        foreach (var item in items)
        {
            ArgumentException.ThrowIfNotEqual(keys.Add(item.Key), true, nameof(items));
        }

        Items = items;
    }

    /// <summary>Gets the criteria in authored order.</summary>
    public ImmutableArray<EvaluationCriterion> Items { get; }

    /// <summary>Finds the criterion of one concrete type.</summary>
    /// <typeparam name="TCriterion">The sealed criterion type to find.</typeparam>
    /// <returns>The criterion, or <see langword="null"/> when the case carries none of that type.</returns>
    public TCriterion? Find<TCriterion>()
        where TCriterion : EvaluationCriterion
    {
        foreach (var item in Items)
        {
            if (item is TCriterion match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Reports whether the case carries a criterion of the given kind.</summary>
    /// <param name="key">The criterion kind.</param>
    /// <returns><see langword="true"/> when one criterion has that key.</returns>
    public bool Contains(EvaluationCriterionKey key)
    {
        foreach (var item in Items)
        {
            if (item.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(EvaluationCriteria? other) => other is not null && Items.SequenceEqual(other.Items);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
