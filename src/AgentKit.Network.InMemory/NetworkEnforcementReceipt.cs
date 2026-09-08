// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>Validates the permission-to-start receipt required before a scripted network effect becomes observable.</summary>
/// <remarks>A receipt authorizes one fresh scripted attempt and does not prove its scripted resolution or response completed.</remarks>
internal static class NetworkEnforcementReceipt
{
    /// <summary>Creates exact network enforcement evidence while preserving the grant's captured authorization when it exists.</summary>
    /// <param name="grant">The non-null grant presented by the network operation.</param>
    /// <param name="audience">The concrete network boundary consuming the grant.</param>
    /// <param name="resources">The initialized ordered canonical network resources.</param>
    /// <param name="fingerprint">The exact canonical network input fingerprint.</param>
    /// <returns>Concrete enforcement evidence that retains captured authorization for snapshot-bound grants.</returns>
    internal static SecurityEnforcementRequest Create(
        SecurityGrant grant,
        ComponentId audience,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        return grant.Authorization is { } authorization
            ? new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, authorization, audience, SecurityOperationKind.Network,
                SecurityEffect.Egress, resources, fingerprint, grant.RevocationVersion)
            : new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, audience, SecurityOperationKind.Network, SecurityEffect.Egress,
                resources, fingerprint, grant.RevocationVersion);
    }

    /// <summary>Determines whether a consumption result authorizes this new exact scripted network effect.</summary>
    /// <param name="consumption">The non-null atomic grant-store result.</param>
    /// <param name="grant">The non-null grant presented to the store.</param>
    /// <param name="enforcement">The non-null concrete evidence presented to the store.</param>
    /// <param name="intent">The non-null freshly generated attempt identity.</param>
    /// <returns><see langword="true"/> only for a newly consumed use with a complete matching receipt.</returns>
    internal static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);

        return consumption.Status is GrantConsumptionStatus.Consumed
            && consumption.IntentReceipt is { } receipt
            && receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
            && HasExactEnforcement(receipt.Enforcement, enforcement);
    }

    /// <summary>Compares every enforcement field, including the resource sequence, without relying on immutable-array backing identity.</summary>
    /// <param name="actual">The non-null enforcement retained by the receipt.</param>
    /// <param name="expected">The non-null enforcement sent for this effect.</param>
    /// <returns><see langword="true"/> when the receipt records the exact concrete effect evidence.</returns>
    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this network boundary.");
        return actual.Scope == expected.Scope
            && actual.Identity == expected.Identity
            && actual.Authorization == expected.Authorization
            && actual.Audience == expected.Audience
            && actual.Kind == expected.Kind
            && actual.Effect == expected.Effect
            && actual.InputFingerprint == expected.InputFingerprint
            && actual.RevocationVersion == expected.RevocationVersion
            && actual.Resources.SequenceEqual(expected.Resources);
    }
}
