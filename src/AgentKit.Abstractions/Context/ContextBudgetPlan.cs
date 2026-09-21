// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The bounded outcome of one context budget allocation pass.</summary>
public sealed record ContextBudgetPlan
{
    /// <summary>Initializes one allocation plan.</summary>
    /// <param name="selected">Candidates that fit within the effective budget.</param>
    /// <param name="omitted">Candidates omitted because they did not fit.</param>
    /// <param name="estimatedTotal">The aggregate estimated cost of <paramref name="selected"/>.</param>
    /// <param name="mandatoryOverflow">
    /// <see langword="true"/> when mandatory candidates alone exceed the effective budget.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="selected"/> or <paramref name="omitted"/> is a default, uninitialized array.
    /// </exception>
    public ContextBudgetPlan(
        ImmutableArray<ContextCandidate> selected,
        ImmutableArray<ContextCandidate> omitted,
        ContextCostEstimate estimatedTotal,
        bool mandatoryOverflow)
    {
        ArgumentException.ThrowIfDefault(selected);
        ArgumentException.ThrowIfDefault(omitted);

        Selected = selected;
        Omitted = omitted;
        EstimatedTotal = estimatedTotal;
        MandatoryOverflow = mandatoryOverflow;
    }

    /// <summary>Gets candidates that fit within the effective budget.</summary>
    public ImmutableArray<ContextCandidate> Selected { get; }

    /// <summary>Gets candidates omitted because they did not fit.</summary>
    public ImmutableArray<ContextCandidate> Omitted { get; }

    /// <summary>Gets the aggregate estimated cost of <see cref="Selected"/>.</summary>
    public ContextCostEstimate EstimatedTotal { get; }

    /// <summary>Gets whether mandatory candidates alone exceed the effective budget.</summary>
    public bool MandatoryOverflow { get; }
}
