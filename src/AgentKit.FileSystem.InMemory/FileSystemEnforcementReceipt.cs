// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

/// <summary>Builds and validates the atomic receipt that permits one concrete in-memory filesystem effect.</summary>
/// <remarks>The receipt is atomically retained permission evidence for one exact effect. Its persistence depends on the selected grant store, and it never proves the host operation completed.</remarks>
internal static class FileSystemEnforcementReceipt
{
    /// <summary>Creates exact filesystem enforcement evidence while retaining captured authorization when the grant has it.</summary>
    /// <param name="grant">The non-null grant presented by the filesystem operation.</param>
    /// <param name="audience">The concrete filesystem boundary consuming the grant.</param>
    /// <param name="kind">The defined operation kind represented by the concrete effect.</param>
    /// <param name="effect">The defined protected effect about to be performed.</param>
    /// <param name="resources">The initialized ordered canonical resources for the effect.</param>
    /// <param name="fingerprint">The exact canonical input fingerprint for the effect.</param>
    /// <returns>Concrete enforcement evidence that preserves snapshot-bound authorization selection.</returns>
    internal static SecurityEnforcementRequest Create(
        SecurityGrant grant,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        return grant.Authorization is { } authorization
            ? new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, authorization, audience, kind, effect, resources, fingerprint,
                grant.RevocationVersion)
            : new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, audience, kind, effect, resources, fingerprint, grant.RevocationVersion);
    }

    /// <summary>Determines whether a consumption result permits this new exact filesystem effect.</summary>
    /// <param name="consumption">The non-null atomic grant-store result.</param>
    /// <param name="grant">The non-null grant presented to the store.</param>
    /// <param name="enforcement">The non-null concrete evidence presented to the store.</param>
    /// <param name="intent">The non-null freshly generated attempt identity.</param>
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

    /// <summary>Compares every enforcement field, including resource sequence contents, without relying on immutable-array backing identity.</summary>
    /// <param name="actual">The non-null enforcement retained by the receipt.</param>
    /// <param name="expected">The non-null enforcement sent for this filesystem effect.</param>
    /// <returns><see langword="true"/> when the receipt carries the exact concrete filesystem evidence.</returns>
    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this filesystem boundary.");
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
