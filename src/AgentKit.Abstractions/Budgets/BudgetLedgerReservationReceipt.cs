// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Preserves original reservation evidence and the ledger's persisted effective expiry.</summary>
public sealed record BudgetLedgerReservationReceipt
{
    /// <summary>Initializes an accepted reservation receipt.</summary>
    /// <param name="reservation">The non-null exact persisted locator.</param>
    /// <param name="originalRequest">The non-null caller request, including a possibly null requested expiry.</param>
    /// <param name="effectiveReservation">The non-null accepted effective expiry.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    /// <exception cref="ArgumentException">The copied original request is invalid, targets another scope, conflicts with a scope-bound operation, or names a different explicit expiry.</exception>
    public BudgetLedgerReservationReceipt(BudgetLedgerReservationReference reservation, BudgetReservationRequest originalRequest, BudgetEffectiveReservation effectiveReservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(effectiveReservation);
        ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(originalRequest, nameof(originalRequest));
        ArgumentException.ThrowIfNotEqual(reservation.Scope.Id, originalRequest.ScopeId, nameof(originalRequest));
        if (reservation.Scope.Address.OperationId is { } operationId)
        {
            ArgumentException.ThrowIfNotEqual(operationId, originalRequest.OperationId, nameof(originalRequest));
        }
        if (originalRequest.ExpiresAt is { } requestedExpiry)
        {
            ArgumentException.ThrowIfNotEqual(requestedExpiry, effectiveReservation.ExpiresAt, nameof(effectiveReservation));
        }
        Reservation = reservation;
        OriginalRequest = originalRequest;
        EffectiveReservation = effectiveReservation;
    }
    /// <summary>Gets the exact persisted reservation locator.</summary>
    /// <value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
    /// <summary>Gets immutable caller evidence.</summary>
    /// <value>The original request used for replay conflict detection.</value>
    public BudgetReservationRequest OriginalRequest { get; }
    /// <summary>Gets the persisted effective expiry.</summary>
    /// <value>Ledger-selected admission evidence.</value>
    public BudgetEffectiveReservation EffectiveReservation { get; }
}
