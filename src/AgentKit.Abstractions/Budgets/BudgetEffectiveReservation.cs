// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the one effective expiry chosen by the ledger for an accepted reservation.</summary>
public sealed record BudgetEffectiveReservation
{
    /// <summary>Initializes persisted effective expiry evidence.</summary><param name="expiresAt">The ledger-clock expiry instant.</param>
    public BudgetEffectiveReservation(DateTimeOffset expiresAt) => ExpiresAt = expiresAt;
    /// <summary>Gets the persisted deadline used for expiry and replay.</summary><value>An absolute instant, never recalculated on retry.</value>
    public DateTimeOffset ExpiresAt { get; }
}
