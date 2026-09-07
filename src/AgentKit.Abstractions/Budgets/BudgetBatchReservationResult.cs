// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed terminal outcome of one atomic batch reservation request.</summary>
/// <remarks>
/// Concrete outcomes are <see cref="BudgetBatchReserved"/> and
/// <see cref="BudgetBatchRejected"/>. No external assembly can add another outcome.
/// </remarks>
public abstract record BudgetBatchReservationResult
{
    /// <summary>Initializes the closed result hierarchy.</summary>
    private protected BudgetBatchReservationResult()
    {
    }
}
