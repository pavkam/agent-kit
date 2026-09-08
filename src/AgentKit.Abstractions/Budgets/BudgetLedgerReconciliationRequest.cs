// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests an idempotent evidence-based reconciliation of an unresolved started reservation.</summary>
public sealed record BudgetLedgerReconciliationRequest
{
    /// <summary>Initializes a reconciliation transition.</summary>
    /// <param name="reservation">The non-null exact reservation locator.</param>
    /// <param name="evidence">The non-null closed reconciliation evidence.</param>
    /// <param name="idempotencyKey">The nonblank key that replays this exact reconciliation.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="reservation"/> or <paramref name="evidence"/> is
    /// null, or <paramref name="idempotencyKey"/> is default and has null text.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> has blank text.</exception>
    public BudgetLedgerReconciliationRequest(BudgetLedgerReservationReference reservation, BudgetReconciliationEvidence evidence, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Reservation = reservation;
        Evidence = evidence;
        IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the exact unresolved reservation.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
    /// <summary>Gets the closed evidence category used by the adapter.</summary><value>Never null and never an inferred timeout result.</value>
    public BudgetReconciliationEvidence Evidence { get; }
    /// <summary>Gets the caller-owned reconciliation replay key.</summary>
    /// <value>Nonblank text scoped to this exact reservation and reconciliation evidence axis.</value>
    public IdempotencyKey IdempotencyKey { get; }
}
