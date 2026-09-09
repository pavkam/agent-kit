// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that active truthful overrun holds blocked an otherwise atomic reservation batch.</summary>
public sealed record BudgetLedgerBatchReserveHeld: BudgetLedgerBatchReserveResult
{
    /// <summary>Creates a held outcome.</summary><param name="holds">The nonempty ordered active hold evidence.</param><exception cref="ArgumentException"><paramref name="holds"/> is default, empty, or contains null.</exception>
    public BudgetLedgerBatchReserveHeld(ImmutableArray<BudgetOverrunHold> holds) { ArgumentException.ThrowIfDefaultOrEmpty(holds); ArgumentException.ThrowIfContainsNull(holds); Holds = holds; }
    /// <summary>Gets the blocking facts.</summary><value>A nonempty immutable array ordered by charged lineage and generation.</value>
    public ImmutableArray<BudgetOverrunHold> Holds { get; }

    /// <summary>Compares ordered blocking facts by content.</summary><param name="other">The candidate outcome.</param><returns>True when ordered holds are equal.</returns>
    public bool Equals(BudgetLedgerBatchReserveHeld? other) => other is not null && Holds.SequenceEqual(other.Holds);
    /// <summary>Computes an ordered content hash.</summary><returns>A hash over every hold.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var hold in Holds)
        {
            hash.Add(hold);
        }

        return hash.ToHashCode();
    }
}
