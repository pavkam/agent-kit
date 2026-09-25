// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Validates grant consumption receipts for MCP client connect and request effects.</summary>
internal static class McpEnforcementReceipt
{
    internal static SecurityEnforcementRequest Create(SecurityGrant grant) =>
        new(
            grant.Scope,
            grant.Identity,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.RevocationVersion);

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

    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }
}
