// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds one retrieval by item count, UTF-8 bytes, and estimated tokens.</summary>
/// <remarks>All three bounds are finite and positive, so a retrieval can never be unbounded. A per-run override or per-query budget may only narrow a host or profile ceiling, through <see cref="Narrow(RetrievalBudget)"/>.</remarks>
public sealed record RetrievalBudget
{
    /// <summary>Initializes a validated budget.</summary>
    /// <param name="maximumItems">The positive largest number of candidates returned.</param>
    /// <param name="maximumBytes">The positive largest total UTF-8 size of returned candidate text.</param>
    /// <param name="maximumTokens">The positive largest total estimated token count of returned candidate text.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    public RetrievalBudget(int maximumItems, int maximumBytes, int maximumTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTokens);
        MaximumItems = maximumItems;
        MaximumBytes = maximumBytes;
        MaximumTokens = maximumTokens;
    }

    /// <summary>Gets the largest number of candidates returned.</summary>
    public int MaximumItems { get; }

    /// <summary>Gets the largest total UTF-8 size of returned candidate text.</summary>
    public int MaximumBytes { get; }

    /// <summary>Gets the largest total estimated token count of returned candidate text.</summary>
    public int MaximumTokens { get; }

    /// <summary>Creates the budget that satisfies both this budget and another.</summary>
    /// <param name="other">The budget to narrow to.</param>
    /// <returns>A budget whose every bound is the smaller of the two.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public RetrievalBudget Narrow(RetrievalBudget other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(
            Math.Min(MaximumItems, other.MaximumItems),
            Math.Min(MaximumBytes, other.MaximumBytes),
            Math.Min(MaximumTokens, other.MaximumTokens));
    }
}
