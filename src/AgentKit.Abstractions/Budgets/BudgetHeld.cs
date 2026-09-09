// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that active captured overrun holds prevented a single reservation.</summary>
public sealed record BudgetHeld: BudgetReservationResult
{
    /// <summary>Initializes a held reservation outcome.</summary>
    /// <param name="holds">The nonempty ordered hold facts that prevented admission.</param>
    /// <exception cref="ArgumentException"><paramref name="holds"/> is default, empty, or contains null.</exception>
    public BudgetHeld(ImmutableArray<BudgetOverrunHold> holds)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(holds);
        ArgumentException.ThrowIfContainsNull(holds);
        Holds = holds;
    }

    /// <summary>Gets the active holds that prevented admission.</summary>
    /// <value>Nonempty evidence ordered by the ledger's enforced lineage.</value>
    public ImmutableArray<BudgetOverrunHold> Holds { get; }

    /// <inheritdoc/>
    public bool Equals(BudgetHeld? other) => other is not null && Holds.SequenceEqual(other.Holds);

    /// <inheritdoc/>
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
