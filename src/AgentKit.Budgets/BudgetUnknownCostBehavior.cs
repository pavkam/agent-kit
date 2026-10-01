// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Declares how the authority would react to an unknown-cost commitment.</summary>
/// <remarks>
/// The ledger-backed first-party authority applies this option to
/// reservations that declare an unknown cost estimate on the
/// <see cref="BudgetDimensions.Cost"/> dimension
/// (<c>BudgetReservationRequest.CostEstimateUnknown</c>). Commitment is not
/// affected: <see cref="IBudgetReservation.CommitAsync"/> takes a non-nullable
/// <see cref="decimal"/>, so an unknown actual amount is not a commit input.
/// </remarks>
public enum BudgetUnknownCostBehavior
{
    /// <summary>Unknown cost is allowed only when the dimension has no configured hard limit.</summary>
    AllowOnlyWithoutCostLimit,

    /// <summary>Unknown cost is always rejected.</summary>
    Reject
}
