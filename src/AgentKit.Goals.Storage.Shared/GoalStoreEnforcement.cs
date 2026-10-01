// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Validates and consumes a single-use grant against the exact operation a goal store is about to perform.</summary>
/// <remarks>
/// A store builds enforcement evidence from the operation it will actually execute and asks the grant store to consume a
/// grant that binds it. Denial, exhaustion, mismatch, an unavailable grant store, or a missing authoritative receipt all
/// refuse the operation before any state is read or written. A granted scope is also checked against the addressed goal's
/// owner, so a grant for one agent or session can never mutate another's goal.
/// </remarks>
internal sealed class GoalStoreEnforcement
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
    internal GoalStoreEnforcement(
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

    /// <summary>Consumes the grant for one exact operation.</summary>
    /// <param name="grant">The presented single-use grant.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The protected effect.</param>
    /// <param name="resources">The resources the operation touches.</param>
    /// <param name="fingerprint">The canonical fingerprint of the exact operation.</param>
    /// <param name="cancellationToken">Cancels before consumption.</param>
    /// <returns>A denial, or <see langword="null"/> when the grant was freshly and exactly consumed.</returns>
    internal async ValueTask<GoalStoreFailure?> ConsumeAsync(
        SecurityGrant grant,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        Debug.Assert(grant is not null, "Adapters validate the request before enforcing it.");
        var enforcement = new SecurityEnforcementRequest(
            grant.Scope, grant.Identity, grant.Authorization, _audience, kind, effect, resources, fingerprint, grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        GrantConsumptionResult consumption;
        try
        {
            consumption = await _grants.ValidateAndConsumeAsync(grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new GoalStoreFailure(GoalStoreFailureKind.Denied, "The grant store was unavailable, so the operation was refused.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return IsFreshExact(consumption, grant, enforcement, intent)
            ? null
            : new GoalStoreFailure(
                GoalStoreFailureKind.Denied,
                consumption.Status is GrantConsumptionStatus.Consumed
                    ? "The grant store did not retain a fresh exact enforcement-intent receipt."
                    : consumption.SafeMessage);
    }

    /// <summary>Checks that the authorized scope owns the addressed goal.</summary>
    /// <param name="grant">The consumed grant whose scope is checked.</param>
    /// <param name="ownerAgentId">The goal's owning agent.</param>
    /// <param name="sessionId">The goal's owning session.</param>
    /// <returns>A scope-mismatch failure, or <see langword="null"/> when the scope owns the goal.</returns>
    internal static GoalStoreFailure? CheckOwner(SecurityGrant grant, AgentId ownerAgentId, SessionId sessionId)
    {
        Debug.Assert(grant is not null, "Adapters validate the request before checking ownership.");
        return grant.Scope.AgentId == ownerAgentId && grant.Scope.SessionId == sessionId
            ? null
            : new GoalStoreFailure(GoalStoreFailureKind.ScopeMismatch, "The authorized agent and session do not own this goal.");
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
