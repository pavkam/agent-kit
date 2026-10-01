// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a child budget that could not be reserved.</summary>
public sealed record GoalBudgetRejected: GoalBudgetReserveResult
{
    /// <summary>Initializes a rejected result.</summary>
    /// <param name="rejection">The content-safe reason.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public GoalBudgetRejected(DelegationRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public DelegationRejection Rejection { get; }
}
