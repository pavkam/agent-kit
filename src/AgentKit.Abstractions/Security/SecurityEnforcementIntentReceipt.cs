// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proves that the grant store atomically consumed one use and retained permission to start one exact effect.</summary>
/// <remarks>The receipt does not prove the protected effect began or completed. Recovery reconciles the effect separately using its semantic idempotency identity.</remarks>
public sealed record SecurityEnforcementIntentReceipt
{
    /// <summary>Initializes an authoritative consumed-intent receipt.</summary>
    /// <param name="intentId">The stable attempt identity.</param><param name="grantId">The consumed grant.</param><param name="requestId">The security request that issued the grant.</param><param name="enforcement">The full recomputed concrete effect evidence.</param><param name="requiredFence">The required distributed fence, or null for a local effect.</param><param name="effectFingerprint">The grant-store-computed digest over the complete enforcement and intent evidence.</param><param name="consumedAt">The injected-clock instant the use and receipt committed together.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception><exception cref="ArgumentNullException"><paramref name="enforcement"/> is null.</exception><exception cref="ArgumentException">The fingerprint is blank.</exception>
    public SecurityEnforcementIntentReceipt(SecurityEnforcementIntentId intentId, GrantId grantId,
        SecurityRequestId requestId, SecurityEnforcementRequest enforcement, FencingToken? requiredFence,
        ContentHash effectFingerprint, DateTimeOffset consumedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(intentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(grantId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        ArgumentNullException.ThrowIfNull(enforcement);
        if (requiredFence is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(requiredFence));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(effectFingerprint.Value, nameof(effectFingerprint));
        IntentId = intentId;
        GrantId = grantId;
        RequestId = requestId;
        Enforcement = enforcement;
        RequiredFence = requiredFence;
        EffectFingerprint = effectFingerprint;
        ConsumedAt = consumedAt;
    }

    /// <summary>Gets the stable consumption-attempt identity.</summary><value>The identity reconciled atomically by the grant store.</value>
    public SecurityEnforcementIntentId IntentId { get; }
    /// <summary>Gets the consumed grant identity.</summary><value>The exact non-default grant whose use was spent.</value>
    public GrantId GrantId { get; }
    /// <summary>Gets the originating security request.</summary><value>The exact non-default request identity retained by the grant.</value>
    public SecurityRequestId RequestId { get; }
    /// <summary>Gets the recomputed concrete enforcement evidence.</summary><value>The immutable scope, identity, audience, operation, resources, input fingerprint, and revocation evidence.</value>
    public SecurityEnforcementRequest Enforcement { get; }
    /// <summary>Gets the required ownership fence.</summary><value>The exact fence retained with permission to start, or null for local effects.</value>
    public FencingToken? RequiredFence { get; }
    /// <summary>Gets the exact effect fingerprint.</summary><value>A grant-store-computed digest over all enforcement and intent evidence.</value>
    public ContentHash EffectFingerprint { get; }
    /// <summary>Gets the authoritative consumption time.</summary><value>The injected-clock instant the grant use and receipt committed atomically.</value>
    public DateTimeOffset ConsumedAt { get; }
}
