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

        var consumption = await grantStore.ValidateAndConsumeAsync(
            grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(consumption, grant, enforcement, intent))
        {
            return FileSystemEnforcementReceipt.DenialMessage(consumption);
        }

        var auditRecord = new SecurityAuditRecord(
            auditRecordIds.Create(),
            grant.Scope,
            grant.RequestId,
            grant.Id,
            null,
            SecurityAuditEventKind.GrantConsumptionIntent,
            SecurityAuditOutcome.Accepted,
            grant.PolicyVersion,
            ImmutableDictionary<string, RedactedAuditValue>.Empty
                .Add("audience", RedactedAuditValue.FromComponentId(enforcement.Audience))
                .Add("effect", RedactedAuditValue.FromEffect(enforcement.Effect))
                .Add("fingerprint", RedactedAuditValue.FromFingerprint(new ContentHash(enforcement.InputFingerprint.Value)))
                .Add("kind", RedactedAuditValue.FromOperationKind(enforcement.Kind)),
            timeProvider.GetUtcNow());
        SecurityAuditDispatchResult auditResult;
        try
        {
            auditResult = await auditDispatcher.DispatchAsync(auditRecord, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return "Required security audit failed.";
        }

        return auditResult is SecurityAuditAccepted ? null : "Required security audit failed.";
    }
}
