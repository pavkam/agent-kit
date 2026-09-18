// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Pairs one persisted batch member's caller evidence with the identity and expiry the ledger chose for it.</summary>
/// <remarks>
/// <para>
/// An indivisible batch is written as a single journal record, and this entry is one position inside it. Keeping the
/// allocated identity and the resolved effective expiry next to the original request is what makes the batch replayable
/// exactly: recovery restores the same receipts a caller already holds instead of allocating new identities or
/// recomputing a deadline against a later clock.
/// </para>
/// <para>
/// Entry order is meaningful. Receipts are returned in original request order, so a reordered journal line would hand a
/// caller a different receipt for the same replayed key.
/// </para>
/// </remarks>
/// <param name="Request">The non-null original caller evidence for this batch position.</param>
/// <param name="ReservationId">The raw value of the non-empty identity the ledger allocated for this position.</param>
/// <param name="EffectiveExpiresAt">The exact deadline the ledger persisted, equal to the caller's expiry when one was supplied.</param>
public sealed record JsonBudgetReservationEntry(
    JsonBudgetReservationRequest Request,
    Guid ReservationId,
    DateTimeOffset EffectiveExpiresAt)
{
    /// <summary>Projects one accepted reservation receipt into its portable JSON representation.</summary>
    /// <param name="value">The non-null persisted receipt to project.</param>
    /// <returns>A document carrying the original request, the allocated identity, and the ledger-selected expiry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetReservationEntry FromDomain(BudgetLedgerReservationReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonBudgetReservationEntry(
            JsonBudgetReservationRequest.FromDomain(value.OriginalRequest),
            value.Reservation.Id.Value,
            value.EffectiveReservation.ExpiresAt);
    }

    /// <summary>Reconstructs the exact domain reservation request this entry retained.</summary>
    /// <returns>The original caller request equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Request"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">The persisted request text is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty or the persisted amount is not positive.</exception>
    public BudgetReservationRequest ToDomainRequest()
    {
        ArgumentNullException.ThrowIfNull(Request);
        return Request.ToDomain();
    }

    /// <summary>Reconstructs the identity the ledger allocated for this batch position.</summary>
    /// <returns>The persisted nondefault reservation identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ReservationId"/> is empty.</exception>
    public BudgetReservationId ToDomainReservationId() => new(ReservationId);
}
