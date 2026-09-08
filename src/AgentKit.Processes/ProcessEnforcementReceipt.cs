// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Validates the receipt required before operating-system process creation starts.</summary>
/// <remarks>The receipt proves permission to start one fresh attempt and does not prove that the process was created or settled.</remarks>
internal static class ProcessEnforcementReceipt
{
    /// <summary>Creates exact process enforcement evidence while preserving the grant's captured authorization when it exists.</summary>
    /// <param name="grant">The non-null grant presented by the process operation.</param>
    /// <param name="audience">The concrete process runner consuming the grant.</param>
    /// <param name="resources">The initialized ordered canonical process resources.</param>
    /// <param name="fingerprint">The exact canonical process input fingerprint.</param>
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
                grant.Scope, grant.Identity, authorization, audience, SecurityOperationKind.Process,
                SecurityEffect.Execute, resources, fingerprint, grant.RevocationVersion)
            : new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, audience, SecurityOperationKind.Process, SecurityEffect.Execute,
                resources, fingerprint, grant.RevocationVersion);
    }

    /// <summary>Determines whether a consumption result authorizes this new exact process-start effect.</summary>
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

    /// <summary>Compares every enforcement field, including resource sequence contents, without relying on immutable-array backing identity.</summary>
    /// <param name="actual">The non-null enforcement retained by the receipt.</param>
    /// <param name="expected">The non-null enforcement sent for this process effect.</param>
    /// <returns><see langword="true"/> when the receipt carries the exact concrete process evidence.</returns>
    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this process boundary.");
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
