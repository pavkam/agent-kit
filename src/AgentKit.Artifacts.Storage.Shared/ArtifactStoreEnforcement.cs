// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Validates and consumes a single-use grant against the exact operation an artifact store is about to perform.</summary>
/// <remarks>
/// A store builds enforcement evidence from the operation it will actually execute and asks the grant store to consume a grant that
/// binds it. Denial, exhaustion, mismatch, an unavailable grant store, or a missing authoritative receipt all refuse the operation
/// before any state is read or written. Consumed grants are never retained with stored content.
/// </remarks>
internal sealed class ArtifactStoreEnforcement
{
    private readonly ISecurityGrantStore _grants;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ComponentId _audience;

    /// <summary>Initializes enforcement for one store audience.</summary>
    /// <param name="grants">The authoritative grant store.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="audience">The store component identity grants must name.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="audience"/> is blank.</exception>
    internal ArtifactStoreEnforcement(
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        ComponentId audience)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience.Value, nameof(audience));
        _grants = grants;
        _intentIds = intentIds;
        _audience = audience;
    }

    /// <summary>Consumes the grant for one exact artifact operation.</summary>
    /// <param name="grant">The presented single-use grant, which must retain captured authorization.</param>
    /// <param name="effect">The protected effect.</param>
    /// <param name="resources">The resources the operation touches.</param>
    /// <param name="fingerprint">The canonical fingerprint of the exact operation.</param>
    /// <param name="cancellationToken">Cancels before or immediately after consumption.</param>
    /// <returns>A denial, or <see langword="null"/> when the grant was freshly and exactly consumed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled, including during a non-cooperative consumption.</exception>
    internal async ValueTask<ArtifactFailure?> ConsumeAsync(
        SecurityGrant grant,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        Debug.Assert(grant is not null, "Requests validate their grant before enforcement.");
        Debug.Assert(!resources.IsDefaultOrEmpty, "Every artifact operation names its protected resources.");
        var enforcement = new SecurityEnforcementRequest(
            grant.Scope, grant.Identity, grant.Authorization, _audience, SecurityOperationKind.Artifact, effect, resources,
            fingerprint, grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        GrantConsumptionResult consumption;
        try
        {
            consumption = await _grants.ValidateAndConsumeAsync(grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ArtifactFailure(ArtifactFailureKind.Denied, "The grant store was unavailable, so the operation was refused.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return IsFreshExact(consumption, grant, enforcement, intent)
            ? null
            : new ArtifactFailure(
                ArtifactFailureKind.Denied,
                consumption.Status is GrantConsumptionStatus.Consumed
                    ? "The grant store did not retain a fresh exact enforcement-intent receipt."
                    : consumption.SafeMessage);
    }

    private static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent) =>
        consumption.Status is GrantConsumptionStatus.Consumed
        && consumption.IntentReceipt is { } receipt
        && receipt.IntentId == intent.Id
        && receipt.GrantId == grant.Id
        && receipt.RequestId == grant.RequestId
        && receipt.RequiredFence == intent.RequiredFence
        && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
        && receipt.Enforcement.Scope == enforcement.Scope
        && receipt.Enforcement.Identity == enforcement.Identity
        && receipt.Enforcement.Authorization == enforcement.Authorization
        && receipt.Enforcement.Audience == enforcement.Audience
        && receipt.Enforcement.Kind == enforcement.Kind
        && receipt.Enforcement.Effect == enforcement.Effect
        && receipt.Enforcement.InputFingerprint == enforcement.InputFingerprint
        && receipt.Enforcement.RevocationVersion == enforcement.RevocationVersion
        && receipt.Enforcement.Resources.SequenceEqual(enforcement.Resources);
}
