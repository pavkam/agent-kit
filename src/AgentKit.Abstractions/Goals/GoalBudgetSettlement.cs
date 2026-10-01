// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the result of settling a goal budget reservation.</summary>
public sealed record GoalBudgetSettlement
{
    /// <summary>Initializes a settlement result.</summary>
    /// <param name="withinBudget"><see langword="true"/> when the known usage fits inside the reserved ceiling.</param>
    public GoalBudgetSettlement(bool withinBudget) => WithinBudget = withinBudget;

    /// <summary>Gets a value indicating whether the known usage fits inside the reserved ceiling. Unknown token usage is never counted as an overrun.</summary>
    public bool WithinBudget { get; }
}
