// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The batch reserved no capacity because one member failed admission.</summary>
public sealed record BudgetBatchRejected: BudgetBatchReservationResult
{
    /// <summary>Initializes a rejected batch result.</summary>
    /// <param name="failure">The first deterministic limit failure preventing admission.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public BudgetBatchRejected(BudgetLimitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the failure preventing the entire batch from being reserved.</summary>
    public BudgetLimitFailure Failure { get; }
}
