// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalBudget"/>.</summary>
/// <param name="MaximumTurns">The model-turn ceiling.</param>
/// <param name="MaximumToolCalls">The tool-call ceiling.</param>
/// <param name="MaximumChildren">The child-goal ceiling.</param>
internal sealed record GoalBudgetDocument(int MaximumTurns, int MaximumToolCalls, int MaximumChildren)
{
    /// <summary>Converts a budget to its persisted form.</summary>
    /// <param name="value">The non-null budget.</param>
    /// <returns>The document.</returns>
    internal static GoalBudgetDocument FromDomain(GoalBudget value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.MaximumTurns, value.MaximumToolCalls, value.MaximumChildren);
    }

    /// <summary>Restores the budget, re-running its validation.</summary>
    /// <returns>The budget.</returns>
    internal GoalBudget ToDomain() => new(MaximumTurns, MaximumToolCalls, MaximumChildren);
}
