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

        var consumption = await grantStore.ValidateAndConsumeAsync(
            grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NetworkEnforcementReceipt.IsFreshExact(consumption, grant, enforcement, intent))
        {
            return consumption.Status == GrantConsumptionStatus.Consumed
                ? "The grant store did not retain a fresh exact enforcement-intent receipt."
                : consumption.SafeMessage;
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
