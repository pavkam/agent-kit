// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>
/// Binds every item key in one admitted batch to its exact ordered request
/// fingerprint and caller-visible reservation receipts.
/// </summary>
/// <remarks>
/// The in-memory authority retains this receipt for its process lifetime,
/// including after release or expiry, so an item key cannot reacquire
/// capacity. It is not durable across process loss.
/// </remarks>
internal sealed class InMemoryBudgetBatchRecord(
    BudgetScopeId targetScopeId,
    ImmutableArray<BudgetReservationRequest> requests)
{
    /// <summary>Gets the original scope that admitted and owns the caller-visible receipts.</summary>
    internal BudgetScopeId TargetScopeId { get; } = targetScopeId;

    /// <summary>Gets the immutable ordered content used to validate exact idempotent replay.</summary>
    internal ImmutableArray<BudgetReservationRequest> Requests { get; } = requests;

    /// <summary>
    /// Gets or sets the caller-visible receipts after the complete hierarchy
    /// plan has been created and before ledger mutation begins.
    /// </summary>
    internal ImmutableArray<InMemoryBudgetReservation> TargetReservations { get; set; }
}
