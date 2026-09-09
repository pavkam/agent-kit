// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an unstarted reservation reached its persisted expiry before start permission was recorded.</summary>
/// <remarks>This ordinary terminal outcome forbids the controlled effect and replays the immutable effective expiry without fabricating budget-limit evidence.</remarks>
public sealed record BudgetStartExpired: BudgetStartResult
{
    /// <summary>Creates an immutable expiration receipt.</summary>
    /// <param name="reservationId">The nondefault expired reservation identity.</param>
    /// <param name="effectiveExpiry">The persisted deadline before which start permission was required.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default.</exception>
    public BudgetStartExpired(BudgetReservationId reservationId, DateTimeOffset effectiveExpiry)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default, nameof(reservationId));
        ReservationId = reservationId;
        EffectiveExpiry = effectiveExpiry;
    }

    /// <summary>Gets the nondefault identity of the reservation that cannot start.</summary>
    /// <value>The stable identity copied from the expired reservation.</value>
    public BudgetReservationId ReservationId { get; }

    /// <summary>Gets the persisted effective expiry retained when the reservation was admitted.</summary>
    /// <value>The exact admitted deadline used to decide and replay expiration.</value>
    public DateTimeOffset EffectiveExpiry { get; }
}
