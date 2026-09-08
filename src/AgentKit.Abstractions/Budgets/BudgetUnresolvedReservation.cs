// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes a reservation that started but has no settled usage and therefore must remain charged.</summary>
public sealed record BudgetUnresolvedReservation
{
    /// <summary>Initializes unresolved started evidence.</summary>
    /// <param name="receipt">The non-null accepted reservation receipt.</param>
    /// <param name="startedAt">The ledger-clock instant durably recorded by <c>MarkStarted</c> before the caller may begin its effect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startedAt"/> is at or after the persisted effective
    /// reservation expiry and therefore cannot describe a permitted start.
    /// </exception>
    public BudgetUnresolvedReservation(BudgetLedgerReservationReceipt receipt, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startedAt, receipt.EffectiveReservation.ExpiresAt);
        Receipt = receipt;
        StartedAt = startedAt;
    }
    /// <summary>Gets immutable reservation and expiry evidence.</summary><value>Never null.</value>
    public BudgetLedgerReservationReceipt Receipt { get; }
    /// <summary>Gets the persisted pre-effect start-permission timestamp.</summary>
    /// <value>A ledger-selected timestamp that permits an effect to begin; it never proves that the effect began.</value>
    public DateTimeOffset StartedAt { get; }
}
