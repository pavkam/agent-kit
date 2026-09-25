// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares ordered reranker candidates and fallback behavior.</summary>
public sealed record RerankerSelectionPolicy
{
    /// <summary>Initializes a reranker selection policy.</summary>
    /// <param name="candidates">The ordered candidate aliases.</param>
    /// <param name="fallback">The fallback mode.</param>
    /// <exception cref="ArgumentException"><paramref name="candidates"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fallback"/> is undefined.</exception>
    public RerankerSelectionPolicy(
        ImmutableArray<RerankerAlias> candidates,
        SemanticFallbackPolicy fallback = SemanticFallbackPolicy.FirstCandidateOnly)
    {
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentOutOfRangeException.ThrowIfUndefined(fallback);
        Candidates = candidates;
        Fallback = fallback;
    }

    /// <summary>Gets the ordered candidate aliases.</summary>
    public ImmutableArray<RerankerAlias> Candidates { get; init; }

    /// <summary>Gets the fallback mode.</summary>
    public SemanticFallbackPolicy Fallback { get; init; }
}
