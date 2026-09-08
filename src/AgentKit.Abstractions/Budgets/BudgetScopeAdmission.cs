// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures finite hierarchy and reservation bounds selected when a scope is admitted.</summary>
/// <remarks>This immutable value is persisted with its scope so later option changes and retries cannot change its capacity bounds.</remarks>
public sealed record BudgetScopeAdmission
{
    /// <summary>Initializes immutable admission facts.</summary>
    /// <param name="maximumScopeDepth">The positive maximum number of scopes in the lineage including this scope.</param>
    /// <param name="maximumOpenReservationsPerScope">The positive maximum number of capacity-retaining reservations per scope.</param>
    /// <param name="defaultReservationLifetime">The positive lifetime used when an original request omits an expiry.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied bound is not positive.</exception>
    public BudgetScopeAdmission(int maximumScopeDepth, int maximumOpenReservationsPerScope, TimeSpan defaultReservationLifetime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumScopeDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOpenReservationsPerScope);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(defaultReservationLifetime, TimeSpan.Zero);
        MaximumScopeDepth = maximumScopeDepth;
        MaximumOpenReservationsPerScope = maximumOpenReservationsPerScope;
        DefaultReservationLifetime = defaultReservationLifetime;
    }
    /// <summary>Gets the captured maximum lineage depth.</summary>
    /// <value>A positive count which includes the scope being admitted.</value>
    public int MaximumScopeDepth { get; }
    /// <summary>Gets the captured per-scope open-reservation bound.</summary>
    /// <value>A positive count applied by the ledger.</value>
    public int MaximumOpenReservationsPerScope { get; }
    /// <summary>Gets the positive default lifetime selected at scope admission.</summary>
    /// <value>A finite duration which the ledger adds once using its own clock.</value>
    public TimeSpan DefaultReservationLifetime { get; }
}
