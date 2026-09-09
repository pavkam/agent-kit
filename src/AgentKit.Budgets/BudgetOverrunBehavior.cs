// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Declares how the authority reacts when a committed actual amount overruns its reservation.</summary>
/// <remarks>
/// The first-party runtime captures this choice independently for every created
/// scope. Automatic policy clears only after eligible corrected accounting;
/// operator policy remains held until a separately authorized ledger resolution.
/// </remarks>
public enum BudgetOverrunBehavior
{
    /// <summary>The overrun is recorded and the affected dimension is blocked from further reservations.</summary>
    RecordAndBlockFurtherReservations,

    /// <summary>The overrun is recorded and requires explicit operator reconciliation before further reservations.</summary>
    RequireOperatorReconciliation
}
