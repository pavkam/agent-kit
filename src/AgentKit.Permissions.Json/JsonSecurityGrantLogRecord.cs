// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>One newline-delimited authoritative transition appended to the grant-store record log.</summary>
/// <remarks>
/// <para>
/// A single record is the store's atomicity unit. A consumption that also produces an enforcement receipt carries both the
/// new remaining-use count and the receipt, so the two can never be observed apart after process loss.
/// </para>
/// <para>
/// Members not applicable to a record's <paramref name="Kind"/> are null and are omitted from the encoded line under the
/// canonical contract. Replay validates applicability rather than trusting the writer, so a hand-edited or truncated log is
/// rejected instead of silently producing a partial state.
/// </para>
/// </remarks>
/// <param name="Kind">The discriminator selecting which remaining members are meaningful.</param>
/// <param name="Grant">The complete immutable grant, present only for <see cref="JsonSecurityGrantLogRecordKind.Registered"/>.</param>
/// <param name="GrantId">The affected grant identity, present for every kind except <see cref="JsonSecurityGrantLogRecordKind.Registered"/> and <see cref="JsonSecurityGrantLogRecordKind.Receipt"/>.</param>
/// <param name="RemainingUses">The nonnegative remaining-use count after the transition, present for <see cref="JsonSecurityGrantLogRecordKind.Consumed"/> and <see cref="JsonSecurityGrantLogRecordKind.State"/>.</param>
/// <param name="Revoked">The live revocation flag, present only for <see cref="JsonSecurityGrantLogRecordKind.State"/>.</param>
/// <param name="Receipt">The enforcement-intent receipt, present for a receipt-bearing consumption and for <see cref="JsonSecurityGrantLogRecordKind.Receipt"/>.</param>
public sealed record JsonSecurityGrantLogRecord(
    JsonSecurityGrantLogRecordKind Kind,
    JsonSecurityGrant? Grant,
    Guid? GrantId,
    int? RemainingUses,
    bool? Revoked,
    JsonSecurityEnforcementIntentReceipt? Receipt)
{
    /// <summary>Creates the record describing one grant entering the store.</summary>
    /// <param name="grant">The complete immutable grant evidence.</param>
    /// <returns>A registration record carrying the full grant.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public static JsonSecurityGrantLogRecord ForRegistration(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return new JsonSecurityGrantLogRecord(
            JsonSecurityGrantLogRecordKind.Registered, JsonSecurityGrant.FromDomain(grant), null, null, null, null);
    }

    /// <summary>Creates the record describing one consumption and its optional enforcement receipt.</summary>
    /// <param name="grantId">The consumed grant identity.</param>
    /// <param name="remainingUses">The nonnegative remaining-use count after this consumption.</param>
    /// <param name="receipt">The enforcement-intent receipt, or null for the legacy receiptless path.</param>
    /// <returns>A consumption record that commits both effects in one atomic append.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="remainingUses"/> is negative.</exception>
    public static JsonSecurityGrantLogRecord ForConsumption(
        GrantId grantId, int remainingUses, SecurityEnforcementIntentReceipt? receipt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(remainingUses);
        return new JsonSecurityGrantLogRecord(
            JsonSecurityGrantLogRecordKind.Consumed, null, grantId.Value, remainingUses, null,
            receipt is null ? null : JsonSecurityEnforcementIntentReceipt.FromDomain(receipt));
    }

    /// <summary>Creates the record describing one revocation.</summary>
    /// <param name="grantId">The revoked grant identity.</param>
    /// <returns>A revocation record.</returns>
    public static JsonSecurityGrantLogRecord ForRevocation(GrantId grantId) => new(
        JsonSecurityGrantLogRecordKind.Revoked, null, grantId.Value, null, null, null);

    /// <summary>Creates the compaction record restating one grant's live mutable state.</summary>
    /// <param name="grantId">The grant identity.</param>
    /// <param name="remainingUses">The nonnegative live remaining-use count.</param>
    /// <param name="revoked">The live revocation flag.</param>
    /// <returns>A state record emitted only while compacting.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="remainingUses"/> is negative.</exception>
    public static JsonSecurityGrantLogRecord ForState(GrantId grantId, int remainingUses, bool revoked)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(remainingUses);
        return new JsonSecurityGrantLogRecord(
            JsonSecurityGrantLogRecordKind.State, null, grantId.Value, remainingUses, revoked, null);
    }

    /// <summary>Creates the compaction record restating one retained enforcement receipt.</summary>
    /// <param name="receipt">The retained receipt evidence.</param>
    /// <returns>A receipt record emitted only while compacting.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    public static JsonSecurityGrantLogRecord ForReceipt(SecurityEnforcementIntentReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new JsonSecurityGrantLogRecord(
            JsonSecurityGrantLogRecordKind.Receipt, null, null, null, null,
            JsonSecurityEnforcementIntentReceipt.FromDomain(receipt));
    }
}
