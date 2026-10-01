// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Bounds the cost one judge evaluator may spend, separately from the budgets of the agent runs it judges.</summary>
/// <remarks>
/// The budget is accounted atomically across every concurrent evaluation that shares one evaluator instance. It is
/// evaluator-local accounting: the evaluator has no run scope to reserve through the hierarchical budget authority, so exhaustion
/// is recorded as an inconclusive outcome rather than a budget-authority limit failure.
/// </remarks>
public sealed record ModelJudgeBudget
{
    /// <summary>Initializes a validated budget.</summary>
    /// <param name="maximumCalls">The positive greatest number of judge model requests.</param>
    /// <param name="maximumTokens">The positive greatest number of reported tokens, or <see langword="null"/> for no token bound.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    public ModelJudgeBudget(int maximumCalls, long? maximumTokens = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCalls);
        if (maximumTokens is { } tokens)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokens, nameof(maximumTokens));
        }

        MaximumCalls = maximumCalls;
        MaximumTokens = maximumTokens;
    }

    /// <summary>Gets the greatest number of judge model requests.</summary>
    public int MaximumCalls { get; }

    /// <summary>Gets the greatest number of reported tokens, or <see langword="null"/>.</summary>
    public long? MaximumTokens { get; }
}
