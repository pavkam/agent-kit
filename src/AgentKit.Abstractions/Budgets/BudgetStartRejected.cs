// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The reservation did not enter started accounting, so budget-controlled work must not begin.</summary>
public sealed record BudgetStartRejected: BudgetStartResult
{
    /// <summary>Initializes a rejected start result.</summary>
    /// <param name="failure">The budget fact preventing the reservation from starting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public BudgetStartRejected(BudgetLimitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the budget fact preventing the reservation from starting.</summary>
    public BudgetLimitFailure Failure { get; }
}
