// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Declares how the authority would react to an unknown-cost commitment.</summary>
/// <remarks>
/// This option is declared for parity with the full budgets architecture
/// but is not yet enforced by the ledger-backed first-party authority:
/// <see cref="IBudgetReservation.CommitAsync"/> takes a non-nullable
/// <see cref="decimal"/>, so there is no "unknown amount" input for this
/// reduced contract to special-case yet. A future revision that admits a
/// nullable or explicitly-unknown actual amount for the
/// <see cref="BudgetDimensions.Cost"/> dimension will act on this option.
/// </remarks>
public enum BudgetUnknownCostBehavior
{
    /// <summary>Unknown cost is allowed only when the dimension has no configured hard limit.</summary>
    AllowOnlyWithoutCostLimit,

    /// <summary>Unknown cost is always rejected.</summary>
    Reject
}
