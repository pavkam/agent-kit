// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Evaluates additive policies, applies deny precedence, and alone issues registered bounded grants.</summary>
/// <remarks>Zero applicable allow proposals fail closed. Policy implementations never mint grants, mutate use state, or perform the requested effect.</remarks>
public sealed class SecurityAuthority: ISecurityAuthority
{
    private readonly ImmutableArray<ISecurityPolicy> _policies;
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityGrantIssuer _grantIssuer;
    private readonly ISecurityDecisionStore _decisionStore;
    private readonly TimeProvider _timeProvider;
    private readonly long _policyVersion;
    private readonly ISecurityRevocationGeneration _revocationGeneration;
    private readonly TimeSpan _maximumGrantLifetime;
    private readonly int _maximumGrantUses;
    private readonly SecurityPolicySnapshotReference? _boundPolicySnapshot;
    private readonly ISecurityPolicySelector _policySelector;
    private readonly ILogger<SecurityAuthority> _logger;
    private readonly IApprovalBroker _approvalBroker;
    private readonly IIdentifierGenerator<ApprovalRequestId> _approvalRequestIds;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly IIdentityValidationPolicy? _identityValidation;

    /// <summary>Initializes the first-party security authority.</summary>
    /// <param name="policies">The ordered additive policies.</param>
    /// <param name="grantStore">The authoritative grant state store.</param>
    /// <param name="grantIssuer">The grant issuer that mints bounded grants after a terminal allow decision.</param>
    /// <param name="decisionStore">The store that retains every terminal decision.</param>
    /// <param name="revocationGeneration">The live revocation epoch source.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated permission configuration.</param>
    /// <param name="policySelector">The selector that resolves the effective policy snapshot for each request.</param>
    /// <param name="approvalBroker">The trusted approval coordinator.</param>
    /// <param name="approvalRequestIds">The approval-request identity generator.</param>
    /// <param name="auditDispatcher">The required security-audit dispatcher.</param>
    /// <param name="auditRecordIds">The audit-record identity generator.</param>
    /// <param name="logger">
    /// The optional logger that receives safe authorization diagnostics; a
    /// Microsoft null logger is used when omitted.
    /// </param>
    /// <param name="identityValidation">
    /// The optional identity validation policy that revalidates request identity before policy evaluation. When
    /// omitted, the authority does not perform this check.
    /// </param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public SecurityAuthority(
        IEnumerable<ISecurityPolicy> policies,
        ISecurityGrantStore grantStore,
        ISecurityGrantIssuer grantIssuer,
        ISecurityDecisionStore decisionStore,
        ISecurityRevocationGeneration revocationGeneration,
        TimeProvider timeProvider,
        IOptions<AgentPermissionOptions> options,
        ISecurityPolicySelector policySelector,
        IApprovalBroker approvalBroker,
        IIdentifierGenerator<ApprovalRequestId> approvalRequestIds,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ILogger<SecurityAuthority>? logger = null,
        IIdentityValidationPolicy? identityValidation = null)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(grantIssuer);
        ArgumentNullException.ThrowIfNull(decisionStore);
        ArgumentNullException.ThrowIfNull(revocationGeneration);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(policySelector);
        ArgumentNullException.ThrowIfNull(approvalBroker);
        ArgumentNullException.ThrowIfNull(approvalRequestIds);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        var optionValues = options.Value;
        ArgumentNullException.ThrowIfNull(optionValues);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(optionValues.PolicyVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(optionValues.RevocationVersion);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(optionValues.MaximumGrantLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(optionValues.MaximumGrantUses);
        if (optionValues.PolicySnapshot is { } snapshot)
        {
            ArgumentException.ThrowIfNotEqual(snapshot.Version.Value, optionValues.PolicyVersion, nameof(options));
        }
        _policies = [.. policies];
        _grantStore = grantStore;
        _grantIssuer = grantIssuer;
        _decisionStore = decisionStore;
        _revocationGeneration = revocationGeneration;
        _timeProvider = timeProvider;
        _policyVersion = optionValues.PolicyVersion;
        _maximumGrantLifetime = optionValues.MaximumGrantLifetime;
        _maximumGrantUses = optionValues.MaximumGrantUses;
        _boundPolicySnapshot = optionValues.PolicySnapshot;
        _policySelector = policySelector;
        _approvalBroker = approvalBroker;
        _approvalRequestIds = approvalRequestIds;
        _auditDispatcher = auditDispatcher;
        _auditRecordIds = auditRecordIds;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityAuthority>.Instance;
        _identityValidation = identityValidation;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default) =>
        AuthorizeAsync(request, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(request.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(request.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(request.Resources);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.RequestedUses);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.SecurityAuthorize,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SecurityAuthorize },
                { AgentKitTagNames.AgentId, request.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, request.Scope.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityRequestId, request.Id.ToString() },
                { AgentKitTagNames.ToolCallId, request.ToolCallId?.ToString() },
                { AgentKitTagNames.SecurityOperationKind, request.Kind.ToString() },
                { AgentKitTagNames.SecurityEffect, request.Effect.ToString() },
            });
        SecurityLog.AuthorizationStarted(_logger, request.Id, request.Kind, request.Effect);

        try
        {
            var decision = await AuthorizeCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = decision is SecurityAllowed ? "allowed" : "denied";
            activity.SetSuccessful(outcome);
            SecurityMetrics.Decisions.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome),
                new KeyValuePair<string, object?>(AgentKitTagNames.SecurityOperationKind, request.Kind.ToString()),
                new KeyValuePair<string, object?>(AgentKitTagNames.SecurityEffect, request.Effect.ToString()));

            if (decision is SecurityAllowed allowed)
            {
                SecurityLog.AuthorizationAllowed(_logger, request.Id, allowed.Grant.Id);
            }
            else
            {
                SecurityLog.AuthorizationDenied(_logger, request.Id);
            }

            return decision;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            SecurityMetrics.Decisions.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled"));
            SecurityLog.AuthorizationCancelled(_logger, request.Id);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("faulted", errorType);
            SecurityMetrics.Decisions.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "faulted"));
            SecurityLog.AuthorizationFaulted(
                _logger,
                request.Id,
                exception.GetType().FullName ?? exception.GetType().Name);
            throw;
        }
    }

    private async ValueTask<SecurityDecision> AuthorizeCoreAsync(
        SecurityRequest request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated security request is required by the authority core.");
        var policyVersion = new SecurityPolicyVersion(_policyVersion);
        SecurityRevocationVersion revocationVersion;
        try
        {
            revocationVersion = await _revocationGeneration.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return await DenyAsync(
                request,
                policyVersion,
                "security.revocation_unavailable",
                "The revocation generation is unavailable.",
                new AuthorizationEvaluationTrace(),
                cancellationToken).ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow();
        var trace = new AuthorizationEvaluationTrace();
        var requestAuditDenied = await DispatchRequestAuditAsync(request, policyVersion, now, cancellationToken)
            .ConfigureAwait(false);
        if (requestAuditDenied is not null)
        {
            return await PersistDecisionAsync(request, policyVersion, requestAuditDenied, cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.Authorization is { } authorization
            && (authorization.Scope != request.Scope
                || authorization.Identity != request.Identity
                || _boundPolicySnapshot is null
                || authorization.PolicySnapshot != _boundPolicySnapshot))
        {
            return await DenyAsync(
                request,
                policyVersion,
                "security.captured_context_mismatch",
                "The captured authorization context cannot be evaluated by this authority version.",
                trace,
                cancellationToken).ConfigureAwait(false);
        }
        if (request.Deadline <= now)
        {
            return await DenyAsync(
                request,
                policyVersion,
                "security.deadline_expired",
                "The authorization deadline has expired.",
                trace,
                cancellationToken).ConfigureAwait(false);
        }

        if (_identityValidation is not null)
        {
            IdentityValidationResult validation;
            try
            {
                validation = await _identityValidation.ValidateAsync(request.Identity, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return await DenyAsync(
                    request,
                    policyVersion,
                    AgentErrorCodes.CredentialUnavailable.ToString(),
                    "Identity validation is unavailable.",
                    trace,
                    cancellationToken).ConfigureAwait(false);
            }

            if (validation is IdentityValidationRejected rejected)
            {
                return await DenyAsync(
                    request,
                    policyVersion,
                    MapIdentityValidationDenialCode(rejected.Failure.Kind),
                    rejected.Failure.SafeMessage,
                    trace,
                    cancellationToken).ConfigureAwait(false);
            }

            if (validation is not IdentityValidationPassed)
            {
                return await DenyAsync(
                    request,
                    policyVersion,
                    AgentErrorCodes.AuthenticationFailed.ToString(),
                    "Identity validation returned an unsupported result.",
                    trace,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        SecurityPolicySnapshotResult snapshotSelection;
        try
        {
            snapshotSelection = await _policySelector.SelectAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return await DenyAsync(
                request,
                policyVersion,
                "security.policy_snapshot_unavailable",
                "Policy snapshot selection is unavailable.",
                trace,
                cancellationToken).ConfigureAwait(false);
        }

        switch (snapshotSelection)
        {
            case SecurityPolicySnapshotUnavailable unavailable:
                return await DenyAsync(
                    request,
                    policyVersion,
                    "security.policy_snapshot_unavailable",
                    unavailable.SafeReason,
                    trace,
                    cancellationToken).ConfigureAwait(false);
            case SecurityPolicySnapshotStale stale:
                return await DenyAsync(
                    request,
                    policyVersion,
                    "security.policy_snapshot_stale",
                    stale.SafeReason,
                    trace,
                    cancellationToken).ConfigureAwait(false);
            case SecurityPolicySnapshotResolved:
                break;
            default:
                return await DenyAsync(
                    request,
                    policyVersion,
                    "security.policy_snapshot_unavailable",
                    "Policy snapshot selection returned an unsupported result.",
                    trace,
                    cancellationToken).ConfigureAwait(false);
        }

        var policyContext = SecurityPolicyEvaluationContexts.Create(
            request,
            policyVersion,
            _boundPolicySnapshot,
            revocationVersion,
            now);
        var allowed = false;
        var approvalRequired = false;
        SecurityPolicyResult? hardDenial = null;
        var constraintContributions = new List<SecurityAllowConstraints>();
        foreach (var policy in _policies)
        {
            SecurityPolicyResult result;
            try
            {
                result = await policy.EvaluateAsync(request, policyContext, cancellationToken).ConfigureAwait(false);
                ValidatePolicyResult(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return await DenyAsync(
                    request,
                    policyVersion,
                    "security.policy_evaluation_failed",
                    "Security policy evaluation failed.",
                    trace,
                    cancellationToken).ConfigureAwait(false);
            }

            if (result.Kind == SecurityPolicyResultKind.Deny)
            {
                hardDenial ??= result;
                continue;
            }

            if (result.Kind == SecurityPolicyResultKind.Allow)
            {
                allowed = true;
                trace.RecordContributingPolicy(result.Code!);
            }
            else if (result.Kind == SecurityPolicyResultKind.RequireApproval)
            {
                approvalRequired = true;
                trace.RecordContributingPolicy(result.Code!);
            }

            if (result is { Kind: SecurityPolicyResultKind.Allow or SecurityPolicyResultKind.RequireApproval, Constraints: { } constraints })
            {
                constraintContributions.Add(constraints);
            }
        }

        if (hardDenial is not null)
        {
            return await DenyAsync(
                request,
                policyVersion,
                hardDenial.Code!,
                hardDenial.SafeMessage!,
                trace,
                cancellationToken,
                winningPolicyCode: hardDenial.Code).ConfigureAwait(false);
        }

        if (!allowed && !approvalRequired)
        {
            return await DenyAsync(
                request,
                policyVersion,
                "security.no_policy",
                "No security policy authorized this operation.",
                trace,
                cancellationToken).ConfigureAwait(false);
        }

        var hostMaximumExpiry = now + _maximumGrantLifetime;
        if (hostMaximumExpiry > request.Deadline)
        {
            hostMaximumExpiry = request.Deadline;
        }

        if (!SecurityAllowConstraintsAlgebra.TryIntersect(
                constraintContributions,
                request,
                hostMaximumExpiry,
                _maximumGrantUses,
                out var intersection))
        {
            return await DenyAsync(
                request,
                policyVersion,
                "security.constraint_intersection_empty",
                "The intersected policy constraints deny this operation.",
                trace,
                cancellationToken).ConfigureAwait(false);
        }

        if (intersection is not null)
        {
            trace.SetIntersection(intersection);
        }

        ApprovalRequestId? approvalRequestId = null;
        ApprovalScopeBinding? approvedBinding = null;
        if (approvalRequired)
        {
            var approvalExpiry = request.Deadline < now + _maximumGrantLifetime
                ? request.Deadline
                : now + _maximumGrantLifetime;
            var binding = new ApprovalScopeBinding(
                request,
                policyVersion,
                revocationVersion,
                now,
                approvalExpiry,
                Math.Min(request.RequestedUses, _maximumGrantUses));
            approvalRequestId = _approvalRequestIds.Create();
            var approval = new ApprovalRequest(
                approvalRequestId.Value,
                binding,
                $"Approve {request.Kind} for {request.Audience.Value}.",
                now);
            var approvalResult = await _approvalBroker.RequestAsync(approval, cancellationToken).ConfigureAwait(false);
            if (approvalResult is not ApprovalBrokerApproved approved)
            {
                // Each non-approval outcome keeps its own stable code: an infrastructure outage, an expired
                // deadline, and an explicit human denial are different facts and must stay distinguishable in audit.
                return approvalResult switch
                {
                    ApprovalBrokerExpired => await DenyAsync(
                        request,
                        policyVersion,
                        "security.approval_expired",
                        "The required approval expired.",
                        trace,
                        cancellationToken).ConfigureAwait(false),
                    ApprovalBrokerUnavailable => await DenyAsync(
                        request,
                        policyVersion,
                        "security.approval_unavailable",
                        "Approval is required but approval coordination is unavailable.",
                        trace,
                        cancellationToken).ConfigureAwait(false),
                    _ => await DenyAsync(
                        request,
                        policyVersion,
                        "security.approval_denied",
                        "The required approval was not granted.",
                        trace,
                        cancellationToken).ConfigureAwait(false),
                };
            }

            now = _timeProvider.GetUtcNow();
            if (now >= request.Deadline
                || now >= approved.Response.Binding.ExpiresAt
                || approved.Response.RequestId != approval.Id
                || approved.Response.Binding != binding
                || approved.Response.Resolution != ApprovalResolution.Approved
                || approved.Response.Binding.RevocationVersion != revocationVersion)
            {
                return await DenyAsync(
                    request,
                    policyVersion,
                    "security.approval_stale",
                    "The approval no longer authorizes this request.",
                    trace,
                    cancellationToken).ConfigureAwait(false);
            }

            approvedBinding = approved.Response.Binding;
        }

        var grant = await _grantIssuer.IssueAsync(
            request,
            policyVersion,
            revocationVersion,
            approvedBinding,
            cancellationToken).ConfigureAwait(false);

        var decisionAuditDenied = await DispatchDecisionAuditAsync(
            request,
            policyVersion,
            now,
            trace,
            SecurityAuditOutcome.Accepted,
            trace.WinningAllowPolicyCode ?? "security.allowed",
            cancellationToken).ConfigureAwait(false);
        if (decisionAuditDenied is not null)
        {
            return await PersistDecisionAsync(request, policyVersion, decisionAuditDenied, cancellationToken)
                .ConfigureAwait(false);
        }

        var grantAuditDenied = await DispatchGrantIssuedAuditAsync(
            request,
            policyVersion,
            grant,
            approvalRequestId,
            now,
            cancellationToken).ConfigureAwait(false);
        if (grantAuditDenied is not null)
        {
            return await PersistDecisionAsync(request, policyVersion, grantAuditDenied, cancellationToken)
                .ConfigureAwait(false);
        }

        await _grantStore.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return await PersistDecisionAsync(
            request,
            policyVersion,
            new SecurityAllowed(request.Id, policyVersion, grant),
            cancellationToken).ConfigureAwait(false);
    }

    private static string MapIdentityValidationDenialCode(IdentityFailureKind kind) =>
        kind == IdentityFailureKind.Expired
            ? AgentErrorCodes.AuthorizationDenied.ToString()
            : AgentErrorCodes.AuthenticationFailed.ToString();

    private static SecurityDenied Denied(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        string code,
        string message) => new(request.Id, policyVersion, new SecurityDenial(code, message));

    private async ValueTask<SecurityDecision> DenyAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        string code,
        string message,
        AuthorizationEvaluationTrace trace,
        CancellationToken cancellationToken,
        string? winningPolicyCode = null)
    {
        var decision = Denied(request, policyVersion, code, message);
        var auditDenied = await DispatchDecisionAuditAsync(
            request,
            policyVersion,
            _timeProvider.GetUtcNow(),
            trace,
            SecurityAuditOutcome.Denied,
            code,
            cancellationToken,
            winningPolicyCode ?? trace.WinningAllowPolicyCode).ConfigureAwait(false);
        return await PersistDecisionAsync(request, policyVersion, auditDenied ?? decision, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<SecurityDecision> PersistDecisionAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityDecision decision,
        CancellationToken cancellationToken)
    {
        try
        {
            await _decisionStore.RecordAsync(decision, cancellationToken).ConfigureAwait(false);
            return decision;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Denied(
                request,
                policyVersion,
                "security.decision_store_unavailable",
                "Security decision storage is unavailable.");
        }
    }

    private async ValueTask<SecurityDenied?> DispatchRequestAuditAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var record = new SecurityAuditRecord(
            _auditRecordIds.Create(),
            request.Scope,
            request.Id,
            null,
            null,
            SecurityAuditEventKind.Request,
            SecurityAuditOutcome.Accepted,
            policyVersion,
            SecurityAuthorityAudit.CreateRequestFields(request),
            occurredAt);
        return await DispatchRequiredAuditAsync(request, policyVersion, record, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SecurityDenied?> DispatchDecisionAuditAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        DateTimeOffset occurredAt,
        AuthorizationEvaluationTrace trace,
        SecurityAuditOutcome outcome,
        string decisionCode,
        CancellationToken cancellationToken,
        string? winningPolicyCode = null)
    {
        var record = new SecurityAuditRecord(
            _auditRecordIds.Create(),
            request.Scope,
            request.Id,
            null,
            null,
            SecurityAuditEventKind.Decision,
            outcome,
            policyVersion,
            SecurityAuthorityAudit.CreateDecisionFields(
                decisionCode,
                winningPolicyCode ?? trace.WinningAllowPolicyCode,
                trace.Intersection),
            occurredAt);
        return await DispatchRequiredAuditAsync(request, policyVersion, record, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SecurityDenied?> DispatchGrantIssuedAuditAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityGrant grant,
        ApprovalRequestId? approvalRequestId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var record = new SecurityAuditRecord(
            new SecurityAuditRecordId(grant.Id.Value),
            request.Scope,
            request.Id,
            grant.Id,
            approvalRequestId,
            SecurityAuditEventKind.GrantIssued,
            SecurityAuditOutcome.Accepted,
            policyVersion,
            [],
            occurredAt);
        return await DispatchRequiredAuditAsync(request, policyVersion, record, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SecurityDenied?> DispatchRequiredAuditAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityAuditRecord record,
        CancellationToken cancellationToken)
    {
        SecurityAuditDispatchResult auditResult;
        try
        {
            auditResult = await _auditDispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Denied(request, policyVersion, "security.audit_unavailable", "Required security audit failed.");
        }

        return auditResult is SecurityAuditAccepted
            ? null
            : Denied(request, policyVersion, "security.audit_unavailable", "Required security audit was not accepted.");
    }

    /// <summary>Validates that a policy contribution still satisfies its immutable result contract.</summary>
    /// <param name="result">The contribution returned by the evaluated policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The contribution kind is undefined.</exception>
    /// <exception cref="ArgumentException">A non-abstaining contribution omits its safe evidence.</exception>
    private static void ValidatePolicyResult(SecurityPolicyResult? result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfUndefined(result.Kind);
        if (result.Kind == SecurityPolicyResultKind.Abstain)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(result.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.SafeMessage);
    }
}
