// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of <see cref="BudgetReservationRequest"/>, one member of an indivisible reservation batch.</summary>
/// <remarks>
/// The ledger detects a conflicting retry by comparing a presented batch member with the persisted one, so every caller
/// member is kept verbatim, including a requested expiry the caller supplied. A null <see cref="ExpiresAt"/> means the
/// caller deferred to the scope's captured default lifetime and is preserved as null; the instant the ledger actually chose
/// is recorded separately on <see cref="JsonBudgetReservationEntry"/>.
/// </remarks>
/// <param name="ScopeId">The raw value of the non-empty scope the amount is reserved against.</param>
/// <param name="Dimension">The non-blank canonical dimension key being reserved.</param>
/// <param name="Amount">The positive amount of capacity requested.</param>
/// <param name="Unit">The non-blank canonical unit text <paramref name="Amount"/> is expressed in.</param>
/// <param name="OperationId">The raw value of the non-empty operation the reservation is made on behalf of.</param>
/// <param name="ExpiresAt">The caller-supplied expiry, or <see langword="null"/> when the caller accepted the scope default.</param>
/// <param name="IdempotencyKey">The non-blank caller key that replays this exact batch member.</param>
public sealed record JsonBudgetReservationRequest(
    Guid ScopeId,
    string Dimension,
    decimal Amount,
    string Unit,
    Guid OperationId,
    DateTimeOffset? ExpiresAt,
    string IdempotencyKey)
{
    /// <summary>Projects one domain reservation request into its portable JSON representation.</summary>
    /// <param name="value">The non-null original caller request to project.</param>
    /// <returns>A document carrying the unwrapped identities, the exact amount, and the caller's own expiry choice.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetReservationRequest FromDomain(BudgetReservationRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonBudgetReservationRequest(
            value.ScopeId.Value,
            value.Dimension.Value,
            value.Amount,
            value.Unit.Value,
            value.OperationId.Value,
            value.ExpiresAt,
            value.IdempotencyKey.Value);
    }

    /// <summary>Reconstructs the exact domain reservation request this document was projected from.</summary>
    /// <returns>A request equal to the projected original, with an absent caller expiry preserved as null.</returns>
    /// <exception cref="ArgumentException">The persisted dimension, unit, or replay-key text is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty or the persisted amount is not positive.</exception>
    public BudgetReservationRequest ToDomain() => new(
        new BudgetScopeId(ScopeId),
        new BudgetDimension(Dimension),
        Amount,
        new BudgetUnit(Unit),
        new OperationId(OperationId),
        ExpiresAt,
        new IdempotencyKey(IdempotencyKey));
}
