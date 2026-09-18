// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>One newline-delimited authoritative transition appended to the budget-ledger journal.</summary>
/// <remarks>
/// <para>
/// A single record is the ledger's atomicity unit. An indivisible batch reservation therefore carries every member's
/// original evidence, allocated identity, and effective expiry in one line, so recovery can never observe a batch that
/// reserved some dimensions but not others.
/// </para>
/// <para>
/// A record captures the caller's immutable evidence together with every value the ledger itself chose: allocated
/// identities, resolved expiries, and the exact clock instant the transition observed. Replay re-runs the same transition
/// logic against those recorded values, so monotonic ledger revisions, accounting revisions, and overrun generations are
/// reconstructed identically rather than recomputed against a later clock.
/// </para>
/// <para>
/// Members not applicable to a record's <see cref="Kind"/> are null and are omitted from the encoded line under the
/// canonical contract. Replay validates applicability rather than trusting the writer, so a hand-edited or truncated
/// journal is rejected instead of silently producing partial accounting.
/// </para>
/// </remarks>
/// <param name="Kind">The discriminator selecting which remaining members are meaningful.</param>
/// <param name="ScopeId">The affected scope identity: the allocated scope for a creation, or the receiving scope for a batch reservation.</param>
/// <param name="Scope">The complete admitted scope evidence, present only for <see cref="JsonBudgetLedgerRecordKind.ScopeCreated"/>.</param>
/// <param name="Reservations">The ordered batch members, nonempty only for <see cref="JsonBudgetLedgerRecordKind.BatchReserved"/>.</param>
/// <param name="ReservationId">The affected reservation identity, present for every per-reservation lifecycle transition.</param>
/// <param name="Occurred">The exact ledger-clock instant the transition observed, present for every kind whose outcome depends on time.</param>
/// <param name="Actual">The nonnegative settled or corrected usage, present for <see cref="JsonBudgetLedgerRecordKind.Settled"/> and <see cref="JsonBudgetLedgerRecordKind.Corrected"/>.</param>
/// <param name="Revision">The positive caller-owned correction revision, present only for <see cref="JsonBudgetLedgerRecordKind.Corrected"/>.</param>
/// <param name="Evidence">The closed reconciliation evidence, present only for <see cref="JsonBudgetLedgerRecordKind.Reconciled"/>.</param>
/// <param name="Hold">The exact overrun generation, present only for <see cref="JsonBudgetLedgerRecordKind.OverrunHoldResolutionRecorded"/>.</param>
/// <param name="Receipt">The consumed enforcement-intent receipt retained for audit and replay conflict detection, present only for an overrun resolution.</param>
/// <param name="IdempotencyKey">The non-blank caller replay key, present for reconciliation and overrun resolution.</param>
public sealed record JsonBudgetLedgerRecord(
    JsonBudgetLedgerRecordKind Kind,
    Guid? ScopeId,
    JsonBudgetScopeCreateRequest? Scope,
    ImmutableArray<JsonBudgetReservationEntry> Reservations,
    Guid? ReservationId,
    DateTimeOffset? Occurred,
    decimal? Actual,
    long? Revision,
    JsonBudgetReconciliationEvidence? Evidence,
    JsonBudgetOverrunHoldReference? Hold,
    JsonSecurityEnforcementIntentReceipt? Receipt,
    string? IdempotencyKey)
{
    /// <summary>Creates the record describing one admitted scope and the identity allocated for it.</summary>
    /// <param name="request">The non-null complete admitted scope evidence.</param>
    /// <param name="scopeId">The nondefault identity the ledger allocated.</param>
    /// <returns>A scope-creation record carrying the full request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scopeId"/> is default.</exception>
    public static JsonBudgetLedgerRecord ForScopeCreated(BudgetLedgerScopeCreateRequest request, BudgetScopeId scopeId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(scopeId, default);
        return Empty(JsonBudgetLedgerRecordKind.ScopeCreated) with
        {
            ScopeId = scopeId.Value,
            Scope = JsonBudgetScopeCreateRequest.FromDomain(request),
        };
    }

    /// <summary>Creates the single record that commits one indivisible batch reservation.</summary>
    /// <param name="scopeId">The nondefault receiving scope identity.</param>
    /// <param name="receipts">The ordered accepted receipts, in original request order.</param>
    /// <param name="occurred">The ledger-clock instant used for the batch's expiry sweep and default lifetimes.</param>
    /// <returns>A batch record whose single append makes every member durable together.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scopeId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="receipts"/> is default or empty.</exception>
    public static JsonBudgetLedgerRecord ForBatchReserved(
        BudgetScopeId scopeId, ImmutableArray<BudgetLedgerReservationReceipt> receipts, DateTimeOffset occurred)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(scopeId, default);
        ArgumentException.ThrowIfDefaultOrEmpty(receipts);
        return Empty(JsonBudgetLedgerRecordKind.BatchReserved) with
        {
            ScopeId = scopeId.Value,
            Reservations = [.. receipts.Select(JsonBudgetReservationEntry.FromDomain)],
            Occurred = occurred,
        };
    }

    /// <summary>Creates the record describing one start attempt at a captured instant.</summary>
    /// <param name="reservationId">The nondefault reservation whose start permission or expiry is being persisted.</param>
    /// <param name="occurred">The ledger-clock instant that decides between granting start permission and expiring.</param>
    /// <returns>A start record whose replay reproduces the same branch.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default.</exception>
    public static JsonBudgetLedgerRecord ForStartMarked(BudgetReservationId reservationId, DateTimeOffset occurred)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default);
        return Empty(JsonBudgetLedgerRecordKind.StartMarked) with
        {
            ReservationId = reservationId.Value,
            Occurred = occurred,
        };
    }

    /// <summary>Creates the record describing caller-directed release of unstarted capacity.</summary>
    /// <param name="reservationId">The nondefault reservation being released.</param>
    /// <returns>A release record.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default.</exception>
    public static JsonBudgetLedgerRecord ForReleased(BudgetReservationId reservationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default);
        return Empty(JsonBudgetLedgerRecordKind.Released) with { ReservationId = reservationId.Value };
    }

    /// <summary>Creates the record describing settlement with known actual usage.</summary>
    /// <param name="reservationId">The nondefault reservation being settled.</param>
    /// <param name="actual">The nonnegative actual usage that also identifies a settlement replay.</param>
    /// <returns>A settlement record; the overrun generations it causes are recomputed deterministically on replay.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default or <paramref name="actual"/> is negative.</exception>
    public static JsonBudgetLedgerRecord ForSettled(BudgetReservationId reservationId, decimal actual)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(actual);
        return Empty(JsonBudgetLedgerRecordKind.Settled) with
        {
            ReservationId = reservationId.Value,
            Actual = actual,
        };
    }

    /// <summary>Creates the record describing one revisioned correction of settled accounting.</summary>
    /// <param name="reservationId">The nondefault reservation whose accounting is replaced.</param>
    /// <param name="correctedActual">The nonnegative authoritative replacement usage.</param>
    /// <param name="revision">The positive caller-owned correction revision.</param>
    /// <param name="occurred">The ledger-clock instant used to decide automatic hold clearance eligibility.</param>
    /// <returns>A correction record.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default, <paramref name="correctedActual"/> is negative, or <paramref name="revision"/> is not positive.</exception>
    public static JsonBudgetLedgerRecord ForCorrected(
        BudgetReservationId reservationId, decimal correctedActual, long revision, DateTimeOffset occurred)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(correctedActual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        return Empty(JsonBudgetLedgerRecordKind.Corrected) with
        {
            ReservationId = reservationId.Value,
            Actual = correctedActual,
            Revision = revision,
            Occurred = occurred,
        };
    }

    /// <summary>Creates the single record that commits one evidence-based reconciliation.</summary>
    /// <param name="reservationId">The nondefault unresolved started reservation.</param>
    /// <param name="evidence">The non-null closed reconciliation evidence.</param>
    /// <param name="idempotencyKey">The non-blank replay key bound to this exact evidence.</param>
    /// <returns>A reconciliation record that also commits the settlement or release the evidence implies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public static JsonBudgetLedgerRecord ForReconciled(
        BudgetReservationId reservationId, BudgetReconciliationEvidence evidence, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return Empty(JsonBudgetLedgerRecordKind.Reconciled) with
        {
            ReservationId = reservationId.Value,
            Evidence = JsonBudgetReconciliationEvidence.FromDomain(evidence),
            IdempotencyKey = idempotencyKey.Value,
        };
    }

    /// <summary>Creates the record that binds one operator-resolution replay key to its exact immutable evidence.</summary>
    /// <param name="request">The non-null exact resolution request, including its audit receipt.</param>
    /// <param name="occurred">The ledger-clock instant used to evaluate current hard-limit failures.</param>
    /// <returns>A resolution record written for both blocked and resolved outcomes, because both bind the replay key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static JsonBudgetLedgerRecord ForOverrunHoldResolution(
        BudgetOverrunHoldResolutionRequest request, DateTimeOffset occurred)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Empty(JsonBudgetLedgerRecordKind.OverrunHoldResolutionRecorded) with
        {
            Hold = JsonBudgetOverrunHoldReference.FromDomain(request.Hold),
            Receipt = JsonSecurityEnforcementIntentReceipt.FromDomain(request.EnforcementReceipt),
            IdempotencyKey = request.IdempotencyKey.Value,
            Occurred = occurred,
        };
    }

    /// <summary>Creates the record describing one lazy expiry sweep at a captured instant.</summary>
    /// <param name="occurred">The ledger-clock instant compared against every unstarted reservation's effective expiry.</param>
    /// <returns>A sweep record whose replay releases exactly the same reservations.</returns>
    public static JsonBudgetLedgerRecord ForExpirySweep(DateTimeOffset occurred) =>
        Empty(JsonBudgetLedgerRecordKind.ExpirySwept) with { Occurred = occurred };

    /// <summary>Gets the ordered batch members, normalizing an omitted array to an empty one.</summary>
    /// <returns>The persisted entries in original request order, or an empty sequence when the record carries none.</returns>
    /// <remarks>A decoder produces a default <see cref="ImmutableArray{T}"/> when the member is absent, so callers use this accessor instead of touching <see cref="Reservations"/> directly.</remarks>
    public ImmutableArray<JsonBudgetReservationEntry> Entries => Reservations.IsDefault ? [] : Reservations;

    /// <summary>Compares records by ordered batch contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate record to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member and nested document is equal and both batch sequences are element-wise equal in the same order.</returns>
    /// <remarks>Compiler-generated record equality compares <see cref="Reservations"/> by backing-array identity, so two records decoded from byte-identical JSON would otherwise compare unequal and break the initialization round-trip check.</remarks>
    public bool Equals(JsonBudgetLedgerRecord? other) =>
        other is not null
        && Kind == other.Kind
        && ScopeId == other.ScopeId
        && Scope == other.Scope
        && Entries.AsSpan().SequenceEqual(other.Entries.AsSpan())
        && ReservationId == other.ReservationId
        && Occurred == other.Occurred
        && Actual == other.Actual
        && Revision == other.Revision
        && Evidence == other.Evidence
        && Hold == other.Hold
        && Receipt == other.Receipt
        && string.Equals(IdempotencyKey, other.IdempotencyKey, StringComparison.Ordinal);

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonBudgetLedgerRecord?)"/>.</summary>
    /// <returns>A hash derived from every scalar member, nested document, and each ordered batch entry.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(ScopeId);
        hash.Add(Scope);
        foreach (var entry in Entries.AsSpan())
        {
            hash.Add(entry);
        }

        hash.Add(ReservationId);
        hash.Add(Occurred);
        hash.Add(Actual);
        hash.Add(Revision);
        hash.Add(Evidence);
        hash.Add(Hold);
        hash.Add(Receipt);
        hash.Add(IdempotencyKey, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static JsonBudgetLedgerRecord Empty(JsonBudgetLedgerRecordKind kind)
    {
        Debug.Assert(Enum.IsDefined(kind), "Every factory selects a defined journal record kind.");
        return new JsonBudgetLedgerRecord(kind, null, null, [], null, null, null, null, null, null, null, null);
    }
}
