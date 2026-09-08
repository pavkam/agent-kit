// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

using System.Diagnostics;

/// <summary>Builds and validates the atomic receipt required before a scripted language observation proceeds.</summary>
/// <remarks>The receipt is grant-store evidence for one exact query. It neither proves the scripted result was read nor that a selected store retains evidence durably.</remarks>
internal static class LanguageEnforcementReceipt
{
    /// <summary>Creates exact language-query enforcement evidence while retaining captured authorization from the grant.</summary>
    /// <param name="grant">The non-null grant presented by the query.</param>
    /// <param name="audience">The concrete scripted language boundary consuming the grant.</param>
    /// <param name="kind">The defined language query kind selecting the protected observation.</param>
    /// <param name="path">The optional canonical path observed by the query.</param>
    /// <param name="fingerprint">The exact canonical input fingerprint of the query.</param>
    /// <returns>Concrete enforcement evidence that retains compatible captured authorization selection.</returns>
    internal static SecurityEnforcementRequest Create(
        SecurityGrant grant,
        ComponentId audience,
        LanguageQueryKind kind,
        FileSystemPath? path,
        InputFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        var resources = ImmutableArray.Create(LanguageSecurityBinding.Resource(kind, path));
        return grant.Authorization is { } authorization
            ? new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, authorization, audience, SecurityOperationKind.FileRead,
                SecurityEffect.Observe, resources, fingerprint, grant.RevocationVersion)
            : new SecurityEnforcementRequest(
                grant.Scope, grant.Identity, audience, SecurityOperationKind.FileRead, SecurityEffect.Observe,
                resources, fingerprint, grant.RevocationVersion);
    }

    /// <summary>Determines whether a result permits this fresh exact language query.</summary>
    /// <param name="consumption">The non-null atomic grant-store result.</param>
    /// <param name="grant">The non-null grant presented by the query.</param>
    /// <param name="enforcement">The non-null evidence created by this boundary.</param>
    /// <param name="intent">The non-null fresh identity created for this query attempt.</param>
    /// <returns><see langword="true"/> only when the grant store retained a complete receipt for this new consumption.</returns>
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

    /// <summary>Returns a stable non-content failure message for a result that does not authorize the fresh query.</summary>
    /// <param name="consumption">The non-null result that failed fresh receipt validation.</param>
    /// <returns>The store message for an unconsumed result, or the fixed missing-receipt message for invalid consumed evidence.</returns>
    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }

    /// <summary>Compares all value fields and ordered resource contents without relying on immutable-array backing identity.</summary>
    /// <param name="actual">The enforcement retained by the validated receipt.</param>
    /// <param name="expected">The enforcement created by this boundary.</param>
    /// <returns><see langword="true"/> when the retained evidence is exactly the language query being performed.</returns>
    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this language boundary.");
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
