// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Coordinates trusted inline approval with exact binding, authorization, storage, and audit checks.</summary>
public sealed class DefaultApprovalBroker: IApprovalBroker
{
    private readonly IApprovalStore _store;
    private readonly IApprovalHandler _handler;
    private readonly IApprovalResponderAuthorizer _responderAuthorizer;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the first-party approval coordinator.</summary>
    /// <param name="store">The authoritative approval state store.</param>
    /// <param name="handler">The trusted host approval channel.</param>
    /// <param name="responderAuthorizer">The mandatory responder authority evaluator.</param>
    /// <param name="auditDispatcher">The required security-audit dispatcher.</param>
    /// <param name="auditRecordIds">The audit-record identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultApprovalBroker(
        IApprovalStore store,
        IApprovalHandler handler,
        IApprovalResponderAuthorizer responderAuthorizer,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(responderAuthorizer);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _handler = handler;
        _responderAuthorizer = responderAuthorizer;
        _auditDispatcher = auditDispatcher;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async ValueTask<ApprovalBrokerResult> RequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_store.Capabilities.ProvidesTrustedControlPlane)
        {
            return new ApprovalBrokerUnavailable("Approval storage does not provide trusted control-plane access.");
        }
        if (IsExpired(request))
        {
            return new ApprovalBrokerExpired();
        }

        ApprovalStoreCreateResult createResult;
        try
        {
            createResult = await _store.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ApprovalBrokerUnavailable("Approval storage is unavailable.");
        }

        if (createResult == ApprovalStoreCreateResult.Conflict)
        {
            return new ApprovalBrokerUnavailable("The approval request identity is already bound to different evidence.");
        }

        var retained = await ReadAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (retained is null)
        {
            return new ApprovalBrokerUnavailable("Approval storage is unavailable.");
        }
        if (retained.Request != request)
        {
            return new ApprovalBrokerUnavailable("Retained approval evidence does not match the request.");
        }
        if (retained.Response is { } replay)
        {
            return await AuditAndReturnTerminalAsync(request, replay, cancellationToken).ConfigureAwait(false);
        }
        if (IsExpired(request))
        {
            return new ApprovalBrokerExpired();
        }

        ApprovalHandlerResult handlerResult;
        try
        {
            handlerResult = await _handler.TryResolveAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ApprovalBrokerUnavailable("The approval channel failed.");
        }

        if (handlerResult is not ApprovalHandlerResponded responded)
        {
            return new ApprovalBrokerUnavailable("No approval channel resolved the request.");
        }

        var response = responded.Response;
        if (IsExpired(request) || response.RespondedAt >= request.Binding.ExpiresAt)
        {
            return new ApprovalBrokerExpired();
        }
        if (response.RequestId != request.Id
            || response.Binding != request.Binding
            || response.Binding.Request.Scope != request.Binding.Request.Scope
            || response.Binding.Request.InputFingerprint != request.Binding.Request.InputFingerprint
            || response.Binding.Request.Identity != request.Binding.Request.Identity
            || response.ApproverIdentity.TenantId != request.Binding.Request.Identity.TenantId)
        {
            return new ApprovalBrokerUnavailable("The approval response does not match the retained request binding.");
        }

        ApprovalResponderAuthorizationResult authorization;
        try
        {
            authorization = await _responderAuthorizer.AuthorizeAsync(request, response, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ApprovalBrokerUnavailable("Approval responder authorization failed.");
        }
        if (authorization is not ApprovalResponderAuthorized)
        {
            return new ApprovalBrokerUnavailable("The responder is not authorized to resolve this approval.");
        }

        ApprovalStoreResolveResult resolveResult;
        try
        {
            resolveResult = await _store.ResolveAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ApprovalBrokerUnavailable("Approval storage is unavailable.");
        }
        if (resolveResult is not (ApprovalStoreResolveResult.Resolved or ApprovalStoreResolveResult.AlreadyResolved))
        {
            return new ApprovalBrokerUnavailable("The approval response could not be committed.");
        }

        retained = await ReadAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return retained?.Response is { } terminal
            ? await AuditAndReturnTerminalAsync(request, terminal, cancellationToken).ConfigureAwait(false)
            : new ApprovalBrokerUnavailable("The retained terminal approval could not be read.");
    }

    private async ValueTask<ApprovalBrokerResult> AuditAndReturnTerminalAsync(
        ApprovalRequest request,
        ApprovalResponse terminal,
        CancellationToken cancellationToken)
    {
        var audit = new SecurityAuditRecord(
            _auditRecordIds.Create(),
            request.Binding.Request.Scope,
            request.Binding.Request.Id,
            null,
            request.Id,
            SecurityAuditEventKind.Approval,
            terminal.Resolution == ApprovalResolution.Approved
                ? SecurityAuditOutcome.Accepted
                : SecurityAuditOutcome.Denied,
            request.Binding.PolicyVersion,
            [],
            _timeProvider.GetUtcNow());
        SecurityAuditDispatchResult auditResult;
        try
        {
            auditResult = await _auditDispatcher.DispatchAsync(audit, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ApprovalBrokerUnavailable("Required approval audit failed.");
        }
        return auditResult is SecurityAuditAccepted
            ? Terminal(terminal)
            : new ApprovalBrokerUnavailable("Required approval audit was not accepted.");
    }

    private bool IsExpired(ApprovalRequest request) =>
        _timeProvider.GetUtcNow() >= request.Binding.ExpiresAt;

    private async ValueTask<ApprovalStoreReadResult?> ReadAsync(
        ApprovalRequestId requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _store.ReadAsync(requestId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ApprovalBrokerResult Terminal(ApprovalResponse response) =>
        response.Resolution == ApprovalResolution.Approved
            ? new ApprovalBrokerApproved(response)
            : new ApprovalBrokerDenied(response);
}
