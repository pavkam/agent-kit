// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The reservation was rejected because it would exceed an enforced limit.</summary>
public sealed record BudgetRejected: BudgetReservationResult
{
    /// <summary>Initializes a new instance of the <see cref="BudgetRejected"/> record.</summary>
    /// <param name="failure">Why the reservation was rejected.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public BudgetRejected(BudgetLimitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets why the reservation was rejected.</summary>
    public BudgetLimitFailure Failure { get; init; }
}
