// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is a budget policy's choice of which ranked candidates fit, with the count it left out.</summary>
public sealed record RetrievalBudgetDecision
{
    /// <summary>Initializes a validated decision.</summary>
    /// <param name="selected">The selected candidates, a subsequence of the request in the same order.</param>
    /// <param name="omitted">The number of candidates left out.</param>
    /// <exception cref="ArgumentException"><paramref name="selected"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="omitted"/> is negative.</exception>
    public RetrievalBudgetDecision(ImmutableArray<RetrievalCandidate> selected, int omitted)
    {
        ArgumentException.ThrowIfDefault(selected);
        ArgumentException.ThrowIfContainsNull(selected);
        ArgumentOutOfRangeException.ThrowIfNegative(omitted);
        Selected = selected;
        Omitted = omitted;
    }

    /// <summary>Gets the selected candidates in rank order.</summary>
    public ImmutableArray<RetrievalCandidate> Selected { get; }

    /// <summary>Gets the number of candidates left out.</summary>
    public int Omitted { get; }
}
