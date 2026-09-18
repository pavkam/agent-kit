// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityEnforcementIntentReceipt"/>, the proof that a grant store atomically consumed one use and retained permission to start one exact effect.</summary>
/// <remarks>
/// <para>
/// The receipt is the durable half of grant consumption, so it is the document a JSON grant store writes in the same
/// transaction as the use it spends. It proves only that permission to start was retained; it never proves the protected
/// effect began or completed, and recovery must still reconcile the effect separately using its own semantic idempotency
/// identity. Reconstructing a receipt therefore restores evidence, never authority to repeat an effect.
/// </para>
/// <para>
/// <see cref="RequiredFence"/> is optional because a local effect needs no distributed ownership fence. A null value is
/// preserved exactly rather than normalized to zero, since <see cref="FencingToken"/> treats a non-positive value as invalid
/// and an invented fence would claim ownership evidence the grant store never issued.
/// </para>
/// </remarks>
/// <param name="IntentId">The raw value of the non-empty stable consumption-attempt identity.</param>
/// <param name="GrantId">The raw value of the non-empty grant whose use was spent.</param>
/// <param name="RequestId">The raw value of the non-empty security request that issued the grant.</param>
/// <param name="Enforcement">The non-null recomputed concrete effect evidence checked before consumption.</param>
/// <param name="RequiredFence">The raw positive value of the required distributed ownership fence, or <see langword="null"/> for a local effect.</param>
/// <param name="EffectFingerprint">The non-blank grant-store-computed digest over the complete enforcement and intent evidence.</param>
/// <param name="ConsumedAt">The injected-clock instant at which the use and the receipt committed together.</param>
public sealed record JsonSecurityEnforcementIntentReceipt(
    Guid IntentId,
    Guid GrantId,
    Guid RequestId,
    JsonSecurityEnforcementRequest Enforcement,
    long? RequiredFence,
    string EffectFingerprint,
    DateTimeOffset ConsumedAt)
{
    /// <summary>Projects one domain consumed-intent receipt into its portable JSON representation.</summary>
    /// <param name="value">The non-null receipt to project.</param>
    /// <returns>A document carrying the unwrapped identities, the projected enforcement evidence, the optional fence, and the fingerprint text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityEnforcementIntentReceipt FromDomain(SecurityEnforcementIntentReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonSecurityEnforcementIntentReceipt(
            value.IntentId.Value,
            value.GrantId.Value,
            value.RequestId.Value,
            JsonSecurityEnforcementRequest.FromDomain(value.Enforcement),
            value.RequiredFence?.Value,
            value.EffectFingerprint.Value,
            value.ConsumedAt);
    }

    /// <summary>Reconstructs the exact domain receipt this document was projected from.</summary>
    /// <returns>A receipt equal to the projected original, with an absent fence preserved as null.</returns>
    /// <remarks>
    /// Each identity, the fence, and the fingerprint are rebuilt through their own validating constructors before
    /// <see cref="SecurityEnforcementIntentReceipt"/> revalidates the combination, so a corrupted or partially written receipt
    /// fails closed on read rather than presenting itself as proof that a use was legitimately consumed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Enforcement"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty, a present <see cref="RequiredFence"/> is not positive, or the nested enforcement evidence carries an out-of-range value.</exception>
    /// <exception cref="ArgumentException"><see cref="EffectFingerprint"/> is blank or the nested enforcement evidence is invalid.</exception>
    public SecurityEnforcementIntentReceipt ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Enforcement);
        FencingToken? requiredFence = RequiredFence is { } fence ? new FencingToken(fence) : null;
        return new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(IntentId),
            new GrantId(GrantId),
            new SecurityRequestId(RequestId),
            Enforcement.ToDomain(),
            requiredFence,
            new ContentHash(EffectFingerprint),
            ConsumedAt);
    }
}
