// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

/// <summary>Grant consumption and required-audit gating for in-memory host capabilities.</summary>
internal static class InMemoryFileSystemHostGuard
{
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
            FileSystemEnforcementReceipt.IsFreshExact,
            FileSystemEnforcementReceipt.DenialMessage,
            cancellationToken).ConfigureAwait(false);
    }
}
