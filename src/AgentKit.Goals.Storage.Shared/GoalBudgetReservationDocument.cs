// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalBudgetReservation"/>.</summary>
/// <param name="Budget">The reserved ceiling.</param>
/// <param name="ScopeId">The holding budget scope, or <see langword="null"/>.</param>
internal sealed record GoalBudgetReservationDocument(GoalBudgetDocument Budget, Guid? ScopeId)
{
    /// <summary>Converts a reservation to its persisted form.</summary>
    /// <param name="value">The non-null reservation.</param>
    /// <returns>The document.</returns>
    internal static GoalBudgetReservationDocument FromDomain(GoalBudgetReservation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(GoalBudgetDocument.FromDomain(value.Budget), value.ScopeId?.Value);
    }

    /// <summary>Restores the reservation, re-running its validation.</summary>
    /// <returns>The reservation.</returns>
    internal GoalBudgetReservation ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Budget);
        return new(Budget.ToDomain(), ScopeId is { } scope ? new BudgetScopeId(scope) : null);
    }
}
