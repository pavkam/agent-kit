// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the goal budget manager to reserve a child's budget under its parent.</summary>
public sealed record GoalBudgetReserveRequest
{
    /// <summary>Initializes a validated reserve request.</summary>
    /// <param name="delegation">The delegation whose <see cref="DelegationRequest.Budget"/> is the requested ceiling.</param>
    /// <param name="parentBudget">The parent goal's ceiling, which the child must fit inside.</param>
    /// <param name="parentScopeId">The budget scope that holds the parent's reservation, or <see langword="null"/> for a root parent.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parentScopeId"/> is present and default.</exception>
    public GoalBudgetReserveRequest(DelegationRequest delegation, GoalBudget parentBudget, BudgetScopeId? parentScopeId)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        ArgumentNullException.ThrowIfNull(parentBudget);
        if (parentScopeId is { } scope)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(scope, default, nameof(parentScopeId));
        }

        Delegation = delegation;
        ParentBudget = parentBudget;
        ParentScopeId = parentScopeId;
    }

    /// <summary>Gets the delegation whose requested budget is reserved.</summary>
    public DelegationRequest Delegation { get; }

    /// <summary>Gets the parent goal's ceiling.</summary>
    public GoalBudget ParentBudget { get; }

    /// <summary>Gets the budget scope that holds the parent's reservation, or <see langword="null"/>.</summary>
    public BudgetScopeId? ParentScopeId { get; }
}
