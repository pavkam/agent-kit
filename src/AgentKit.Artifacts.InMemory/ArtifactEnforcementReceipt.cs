// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

using System.Diagnostics;

/// <summary>Builds and validates the atomic permission-to-begin receipt for one in-memory artifact-store operation.</summary>
/// <remarks>The receipt is grant-store evidence for one exact operation. It does not prove that artifact state changed or that a selected store is durable.</remarks>
internal static class ArtifactEnforcementReceipt
{
    /// <summary>Creates exact artifact enforcement evidence while preserving captured authorization from the grant.</summary>
    /// <param name="grant">The non-null grant presented by the artifact-store operation.</param>
    /// <param name="scope">The non-null exact execution scope of the operation.</param>
    /// <param name="identity">The non-null authenticated execution identity of the operation.</param>
    /// <param name="audience">The concrete artifact-store boundary consuming the grant.</param>
    /// <param name="effect">The defined protected effect about to access artifact state.</param>
    /// <param name="resources">The initialized ordered canonical artifact resources for the effect.</param>
    /// <param name="fingerprint">The exact canonical operation fingerprint.</param>
    /// <returns>Concrete evidence that retains the grant's snapshot-bound authorization selection.</returns>
    internal static SecurityEnforcementRequest Create(
        SecurityGrant grant,
        SecurityAuthorizationScope scope,
        ExecutionIdentity identity,
        ComponentId audience,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        return grant.Authorization is { } authorization
            ? new SecurityEnforcementRequest(
                scope, identity, authorization, audience, SecurityOperationKind.Artifact, effect, resources,
                fingerprint, grant.RevocationVersion)
            : new SecurityEnforcementRequest(
                scope, identity, audience, SecurityOperationKind.Artifact, effect, resources, fingerprint,
                grant.RevocationVersion);
    }

    /// <summary>Determines whether a result permits this new exact artifact-store operation.</summary>
    /// <param name="consumption">The non-null atomic grant-store result.</param>
    /// <param name="grant">The non-null grant presented to the store.</param>
    /// <param name="enforcement">The non-null concrete evidence presented to the store.</param>
    /// <param name="intent">The non-null freshly generated operation identity.</param>
    /// <returns><see langword="true"/> only when the store retained a complete receipt for this newly consumed use.</returns>
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

    /// <summary>Returns a stable non-content error when a consumed result lacks the required authoritative receipt.</summary>
    /// <param name="consumption">The non-null consumption result that failed fresh exact validation.</param>
    /// <returns>The store message for an unconsumed result, or a fixed missing-receipt message for an invalid consumed result.</returns>
    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }

    /// <summary>Compares every enforcement field, including resource sequence contents, without immutable-array backing identity.</summary>
    /// <param name="actual">The enforcement retained by the validated receipt.</param>
    /// <param name="expected">The enforcement created by this artifact-store boundary.</param>
    /// <returns><see langword="true"/> when the receipt carries exact concrete artifact operation evidence.</returns>
    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this artifact-store boundary.");
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
