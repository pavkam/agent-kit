// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports truthful accounting facts that prevent operator clearance without mutating the hold.</summary>
public sealed record BudgetOverrunHoldResolutionBlocked: BudgetOverrunHoldResolutionResult
{
    /// <summary>Creates a blocked outcome.</summary><param name="hold">The requested generation.</param><param name="currentOverruns">Current row overruns at the boundary and dimension.</param><param name="hardLimitFailures">Current finite hard-ceiling exhaustion evidence.</param><exception cref="ArgumentNullException"><paramref name="hold"/> is null.</exception><exception cref="ArgumentException">An array is default, contains null, or both arrays are empty.</exception>
    public BudgetOverrunHoldResolutionBlocked(BudgetOverrunHoldReference hold, ImmutableArray<BudgetOverrunHold> currentOverruns, ImmutableArray<BudgetLimitFailure> hardLimitFailures)
    {
        ArgumentNullException.ThrowIfNull(hold); ArgumentException.ThrowIfDefault(currentOverruns); ArgumentException.ThrowIfContainsNull(currentOverruns); ArgumentException.ThrowIfDefault(hardLimitFailures); ArgumentException.ThrowIfContainsNull(hardLimitFailures);
        ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers(currentOverruns, hardLimitFailures);
        Hold = hold; CurrentOverruns = currentOverruns; HardLimitFailures = hardLimitFailures;
    }
    /// <summary>Gets the requested generation.</summary><value>The exact unresolved reference.</value>
    public BudgetOverrunHoldReference Hold { get; }
    /// <summary>Gets current row overruns.</summary><value>An initialized immutable array.</value>
    public ImmutableArray<BudgetOverrunHold> CurrentOverruns { get; }
    /// <summary>Gets hard ceiling failures.</summary><value>An initialized immutable array.</value>
    public ImmutableArray<BudgetLimitFailure> HardLimitFailures { get; }

    /// <summary>Compares the requested generation and both ordered blocker arrays by content.</summary><param name="other">The candidate result.</param><returns>True when every value is equal.</returns>
    public bool Equals(BudgetOverrunHoldResolutionBlocked? other) =>
        other is not null
        && Hold == other.Hold
        && CurrentOverruns.SequenceEqual(other.CurrentOverruns)
        && HardLimitFailures.SequenceEqual(other.HardLimitFailures);

    /// <summary>Computes an ordered content hash.</summary><returns>A hash over the hold and every blocker.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(Hold);
        foreach (var overrun in CurrentOverruns)
        {
            hash.Add(overrun);
        }

        foreach (var failure in HardLimitFailures)
        {
            hash.Add(failure);
        }

        return hash.ToHashCode();
    }
}
