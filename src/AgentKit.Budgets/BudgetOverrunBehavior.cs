// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Declares how the authority reacts when a committed actual amount overruns its reservation.</summary>
/// <remarks>
/// Both members currently produce the same observable behavior in
/// <see cref="InMemoryBudgetAuthority"/>: the affected scope's dimension is
/// blocked from further reservations until an operator or a future
/// reconciliation path clears it. <see cref="RequireOperatorReconciliation"/>
/// is declared for forward compatibility with a not-yet-implemented
/// reconciliation workflow; this reduced implementation does not yet
/// distinguish it from <see cref="RecordAndBlockFurtherReservations"/>.
/// </remarks>
public enum BudgetOverrunBehavior
{
    /// <summary>The overrun is recorded and the affected dimension is blocked from further reservations.</summary>
    RecordAndBlockFurtherReservations,

    /// <summary>The overrun is recorded and requires explicit operator reconciliation before further reservations.</summary>
    RequireOperatorReconciliation
}
