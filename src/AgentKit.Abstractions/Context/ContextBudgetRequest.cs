// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable evidence a budget allocator uses for one selection pass.</summary>
public sealed record ContextBudgetRequest
{
    /// <summary>Initializes one budget allocation request.</summary>
    /// <param name="budget">The envelope within which candidates must fit.</param>
    /// <param name="candidates">The merged candidate set in deterministic contributor order.</param>
    /// <param name="overflowBehavior">How mandatory overflow must be handled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="overflowBehavior"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="candidates"/> is a default, uninitialized array.</exception>
    public ContextBudgetRequest(
        ContextBudget budget,
        ImmutableArray<ContextCandidate> candidates,
        ContextOverflowBehavior overflowBehavior)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentOutOfRangeException.ThrowIfUndefined(overflowBehavior);

        Budget = budget;
        Candidates = candidates;
        OverflowBehavior = overflowBehavior;
    }

    /// <summary>Gets the envelope within which candidates must fit.</summary>
    public ContextBudget Budget { get; }

    /// <summary>Gets the merged candidate set in deterministic contributor order.</summary>
    public ImmutableArray<ContextCandidate> Candidates { get; }

    /// <summary>Gets how mandatory overflow must be handled.</summary>
    public ContextOverflowBehavior OverflowBehavior { get; }
}
