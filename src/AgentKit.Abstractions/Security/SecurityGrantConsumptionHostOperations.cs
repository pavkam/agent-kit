// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Shared grant consumption and required grant-consumption audit orchestration for host boundaries.
/// </summary>
public static class SecurityGrantConsumptionHostOperations
{
    /// <summary>
    /// Validates receipt freshness for one consumption result.
    /// </summary>
    /// <param name="consumption">The atomic grant-store result.</param>
    /// <param name="grant">The presented grant.</param>
    /// <param name="enforcement">The concrete enforcement evidence.</param>
    /// <param name="intent">The fresh enforcement intent.</param>
    /// <returns><see langword="true"/> when the store retained a complete receipt for this newly consumed use.</returns>
    public delegate bool FreshExactValidator(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent);

    /// <summary>
    /// Builds a denial message when <see cref="FreshExactValidator"/> rejects a consumption result.
    /// </summary>
    /// <param name="consumption">The consumption result that failed fresh exact validation.</param>
    /// <returns>A stable, non-content denial message.</returns>
    public delegate string DenialMessageFactory(GrantConsumptionResult consumption);

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
    /// <param name="isFreshExact">Boundary-specific receipt validation.</param>
    /// <param name="denialMessage">Boundary-specific denial messaging.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns><see langword="null"/> when consumption and audit succeeded; otherwise a denial message.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static async ValueTask<string?> ConsumeWithRequiredAuditAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        FreshExactValidator isFreshExact,
        DenialMessageFactory denialMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(isFreshExact);
        ArgumentNullException.ThrowIfNull(denialMessage);
        cancellationToken.ThrowIfCancellationRequested();

        var consumption = await grantStore.ValidateAndConsumeAsync(
            grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!isFreshExact(consumption, grant, enforcement, intent))
        {
            return denialMessage(consumption);
        }

        var auditRecord = CreateGrantConsumptionIntentRecord(
            grant, enforcement, auditRecordIds.Create(), timeProvider.GetUtcNow());
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

    /// <summary>Creates one accepted grant-consumption intent audit record for a host boundary effect.</summary>
    /// <param name="grant">The consumed grant.</param>
    /// <param name="enforcement">The exact enforcement evidence.</param>
    /// <param name="recordId">The audit record identity.</param>
    /// <param name="timestamp">The audit timestamp.</param>
    /// <returns>A grant-consumption intent audit record.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static SecurityAuditRecord CreateGrantConsumptionIntentRecord(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityAuditRecordId recordId,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentOutOfRangeException.ThrowIfEqual(recordId, default);
        return new SecurityAuditRecord(
            recordId,
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
            timestamp);
    }
}
