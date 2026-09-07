// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Mutable, validated binding options for the built-in in-memory budget authority.</summary>
public sealed class AgentBudgetOptions
{
    /// <summary>Gets or sets the maximum number of hierarchy levels a scope may nest to. Defaults to 16.</summary>
    public int MaximumScopeDepth { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum number of concurrently open, uncommitted
    /// reservations one scope may hold across every dimension. Defaults to
    /// 256.
    /// </summary>
    public int MaximumOpenReservationsPerScope { get; set; } = 256;

    /// <summary>
    /// Gets or sets the lifetime applied to a reservation whose request did
    /// not supply an explicit expiry. Defaults to five minutes.
    /// </summary>
    public TimeSpan DefaultReservationLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets how the authority reacts to unknown-cost commitments. See <see cref="BudgetUnknownCostBehavior"/>.</summary>
    public BudgetUnknownCostBehavior UnknownCostBehavior { get; set; } = BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit;

    /// <summary>Gets or sets how the authority reacts to a commit that overruns its reservation.</summary>
    public BudgetOverrunBehavior OverrunBehavior { get; set; } = BudgetOverrunBehavior.RecordAndBlockFurtherReservations;
}
