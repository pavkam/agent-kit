// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Binds every item key in an atomic batch to its ordered request and immutable receipt.</summary>
internal sealed record BatchState
{
    /// <summary>Creates persisted replay state after the corresponding atomic reservation commits.</summary>
    /// <param name="request">The complete ordered original batch.</param>
    /// <param name="result">The accepted receipt in matching order.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    internal BatchState(BudgetLedgerBatchReserveRequest request, BudgetLedgerBatchReserved result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        Request = request;
        Result = result;
    }

    /// <summary>Gets the complete ordered original batch used for exact replay comparison.</summary>
    internal BudgetLedgerBatchReserveRequest Request { get; }

    /// <summary>Gets the accepted receipt returned by every exact replay.</summary>
    internal BudgetLedgerBatchReserved Result { get; }
}
