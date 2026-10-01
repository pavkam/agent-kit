// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a budget policy to choose how many ranked candidates fit a retrieval budget.</summary>
public sealed record RetrievalBudgetRequest
{
    /// <summary>Initializes a validated request.</summary>
    /// <param name="budget">The effective budget, already narrowed to the profile's ceiling.</param>
    /// <param name="candidates">The authorized candidates in final rank order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="candidates"/> is default or contains null.</exception>
    public RetrievalBudgetRequest(RetrievalBudget budget, ImmutableArray<RetrievalCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentException.ThrowIfContainsNull(candidates);
        Budget = budget;
        Candidates = candidates;
    }

    /// <summary>Gets the effective budget.</summary>
    public RetrievalBudget Budget { get; }

    /// <summary>Gets the authorized candidates in final rank order.</summary>
    public ImmutableArray<RetrievalCandidate> Candidates { get; }
}
