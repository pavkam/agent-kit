// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds a <see cref="GoalBudget"/> ceiling to the budget-authority scope that holds its reservation.</summary>
/// <remarks>A request carries a reservation with no scope, meaning the ceiling has been asked for but not yet reserved. The goal budget manager returns the reservation with the scope that holds it. The scope identity is evidence only; it neither grants capacity nor proves spend.</remarks>
public sealed record GoalBudgetReservation
{
    /// <summary>Initializes a reservation.</summary>
    /// <param name="budget">The non-null ceiling.</param>
    /// <param name="scopeId">The budget scope that holds the reservation, or <see langword="null"/> when not yet reserved.</param>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scopeId"/> is present and default.</exception>
    public GoalBudgetReservation(GoalBudget budget, BudgetScopeId? scopeId = null)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (scopeId is { } scope)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(scope, default, nameof(scopeId));
        }

        Budget = budget;
        ScopeId = scopeId;
    }

    /// <summary>Gets the ceiling.</summary>
    public GoalBudget Budget { get; }

    /// <summary>Gets the budget scope holding the reservation, or <see langword="null"/> when not yet reserved.</summary>
    public BudgetScopeId? ScopeId { get; }
}
