// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

using System.Diagnostics;

/// <summary>Builds and validates the enforcement evidence for consuming a tool-issued web-search egress grant.</summary>
/// <remarks>
/// The search provider is the effecting component for the tool-issued grant, so it consumes that grant with its own
/// enforcement intent and required audit before resolving or sending. The lower network boundaries consume their own,
/// separately issued grants, so this evidence can never substitute for either.
/// </remarks>
internal static class NetworkWebSearchEnforcement
{
    /// <summary>Creates enforcement evidence that restates exactly what the grant authorizes.</summary>
    /// <param name="grant">The tool-issued grant being consumed.</param>
    /// <returns>Enforcement evidence naming the grant's audience, resources, fingerprint, and revocation version.</returns>
    internal static SecurityEnforcementRequest Create(SecurityGrant grant)
    {
        Debug.Assert(grant is not null, "The caller consumes a grant issued by the authority.");
        return new SecurityEnforcementRequest(
            grant.Scope,
            grant.Identity,
            grant.Authorization,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.RevocationVersion);
    }

    /// <summary>Determines whether a consumption result authorizes this new exact search egress.</summary>
    /// <param name="consumption">The atomic grant-store result.</param>
    /// <param name="grant">The grant presented to the store.</param>
    /// <param name="enforcement">The evidence presented to the store.</param>
    /// <param name="intent">The freshly generated attempt identity.</param>
    /// <returns><see langword="true"/> only for a newly consumed use with a complete matching receipt.</returns>
    internal static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        Debug.Assert(consumption is not null, "The grant store always returns a result.");
        Debug.Assert(grant is not null, "The caller presents the grant it consumed.");
        Debug.Assert(enforcement is not null, "The caller presents the evidence it consumed.");
        Debug.Assert(intent is not null, "The caller presents the fresh intent it consumed.");
        return consumption.Status is GrantConsumptionStatus.Consumed
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

    /// <summary>Returns a stable non-content denial message for a consumption that failed fresh exact validation.</summary>
    /// <param name="consumption">The failed consumption result.</param>
    /// <returns>The store message, or a fixed message when a consumed result lacks its receipt.</returns>
    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        Debug.Assert(consumption is not null, "The grant store always returns a result.");
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }
}
