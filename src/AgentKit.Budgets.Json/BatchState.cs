// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Binds every item key in an indivisible batch to its ordered request and immutable receipt.</summary>
/// <remarks>
/// All members of one batch share a single instance, which is how the ledger detects a retry that presents only part of a
/// bound batch or alters one member's evidence. The binding is reconstructed from the batch's single journal record, so a
/// recovered process answers the same replay with the same receipts.
/// </remarks>
internal sealed record BatchState
{
    /// <summary>Creates persisted replay state after the corresponding atomic reservation commits.</summary>
    /// <param name="request">The complete ordered original batch.</param>
    /// <param name="result">The accepted receipts in matching order.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    internal BatchState(BudgetLedgerBatchReserveRequest request, BudgetLedgerBatchReserved result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        Request = request;
        Result = result;
    }

    /// <summary>Gets the complete ordered original batch used for exact replay comparison.</summary>
    /// <value>The evidence a presented retry must equal member for member.</value>
    internal BudgetLedgerBatchReserveRequest Request { get; }

    /// <summary>Gets the accepted receipts returned by every exact replay.</summary>
    /// <value>The immutable result, never recomputed after the batch commits.</value>
    internal BudgetLedgerBatchReserved Result { get; }
}
