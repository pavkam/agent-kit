// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Grant consumption and required-audit gating shared by network host adapters.</summary>
internal static class NetworkHostGuard
{
    /// <summary>
    /// Atomically consumes one grant and delivers required grant-consumption audit before a host effect.
    /// </summary>
    /// <param name="grant">The presented grant.</param>
    /// <param name="enforcement">The exact enforcement evidence.</param>
    /// <param name="intent">The fresh enforcement intent.</param>
    /// <param name="grantStore">The authoritative grant store.</param>
    /// <param name="auditDispatcher">The required audit dispatcher.</param>
    /// <param name="auditRecordIds">The audit record identity generator.</param>
    /// <param name="timeProvider">The clock used for audit timestamps.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns><see langword="null"/> when consumption and audit succeeded; otherwise a denial message.</returns>
    internal static async ValueTask<string?> ConsumeWithRequiredAuditAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        cancellationToken.ThrowIfCancellationRequested();

        return await SecurityGrantConsumptionHostOperations.ConsumeWithRequiredAuditAsync(
            grant,
            enforcement,
            intent,
            grantStore,
            auditDispatcher,
            auditRecordIds,
            timeProvider,
            NetworkEnforcementReceipt.IsFreshExact,
            NetworkEnforcementReceipt.DenialMessage,
            cancellationToken).ConfigureAwait(false);
    }
}
