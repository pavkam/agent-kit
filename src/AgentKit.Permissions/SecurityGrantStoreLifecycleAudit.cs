// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Emits grant-lifecycle audit records from grant stores without coupling stores to authority logic.</summary>
public static class SecurityGrantStoreLifecycleAudit
{
    /// <summary>
    /// Dispatches one grant-lifecycle audit record and reports whether a successful consumption must be refused because
    /// required delivery was not accepted.
    /// </summary>
    /// <param name="dispatcher">The optional audit dispatcher configured for the store.</param>
    /// <param name="auditRecordIds">The audit-record identity generator.</param>
    /// <param name="options">The permission options governing default audit delivery.</param>
    /// <param name="grant">The grant whose lifecycle changed.</param>
    /// <param name="outcome">The lifecycle outcome being recorded.</param>
    /// <param name="occurredAt">The occurrence instant.</param>
    /// <param name="cancellationToken">Cancels before dispatch completes.</param>
    /// <returns>
    /// <see langword="true"/> only when <paramref name="outcome"/> is <see cref="SecurityAuditOutcome.Accepted"/>
    /// and required delivery was not accepted, meaning consumption must be refused; otherwise <see langword="false"/>.
    /// </returns>
    public static async ValueTask<bool> RefuseConsumptionIfRequiredAuditFailedAsync(
        ISecurityAuditDispatcher? dispatcher,
        IIdentifierGenerator<SecurityAuditRecordId>? auditRecordIds,
        IOptions<AgentPermissionOptions>? options,
        SecurityGrant grant,
        SecurityAuditOutcome outcome,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (dispatcher is null || auditRecordIds is null || options?.Value is not { } permissionOptions)
        {
            return false;
        }

        if (outcome != SecurityAuditOutcome.Accepted)
        {
            await DispatchCoreAsync(
                dispatcher,
                auditRecordIds,
                grant,
                outcome,
                occurredAt,
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        var record = new SecurityAuditRecord(
            auditRecordIds.Create(),
            grant.Scope,
            grant.RequestId,
            grant.Id,
            null,
            SecurityAuditEventKind.GrantLifecycle,
            outcome,
            grant.PolicyVersion,
            [],
            occurredAt);
        SecurityAuditDispatchResult dispatchResult;
        try
        {
            dispatchResult = await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return permissionOptions.AuditDelivery == SecurityAuditDelivery.Required;
        }

        return permissionOptions.AuditDelivery == SecurityAuditDelivery.Required
            && dispatchResult is not SecurityAuditAccepted;
    }

    private static async ValueTask DispatchCoreAsync(
        ISecurityAuditDispatcher dispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        SecurityGrant grant,
        SecurityAuditOutcome outcome,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var record = new SecurityAuditRecord(
            auditRecordIds.Create(),
            grant.Scope,
            grant.RequestId,
            grant.Id,
            null,
            SecurityAuditEventKind.GrantLifecycle,
            outcome,
            grant.PolicyVersion,
            [],
            occurredAt);
        try
        {
            _ = await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Denied lifecycle audit is best-effort; Required consumption path uses inline dispatch above.
        }
    }
}
