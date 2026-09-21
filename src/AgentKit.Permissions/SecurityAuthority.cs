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
    private readonly IIdentifierGenerator<GrantId> _grantIds;
    private readonly TimeProvider _timeProvider;
    private readonly long _policyVersion;
    private readonly long _revocationVersion;
    private readonly TimeSpan _maximumGrantLifetime;
    private readonly int _maximumGrantUses;
    private readonly SecurityPolicySnapshotReference? _boundPolicySnapshot;
    private readonly ISecurityPolicySelector _policySelector;
    private readonly ILogger<SecurityAuthority> _logger;
    private readonly IApprovalBroker? _approvalBroker;
    private readonly IIdentifierGenerator<ApprovalRequestId>? _approvalRequestIds;
    private readonly ISecurityAuditDispatcher? _auditDispatcher;
    private readonly IIdentityValidationPolicy? _identityValidation;

    /// <summary>Initializes the first-party security authority.</summary>
    /// <param name="policies">The ordered additive policies.</param>
    /// <param name="grantStore">The authoritative grant state store.</param>
    /// <param name="grantIds">The grant identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated permission configuration.</param>
    /// <param name="logger">
    /// The optional logger that receives safe authorization diagnostics; a
    /// Microsoft null logger is used when omitted.
    /// </param>
    /// <param name="policySelector">The selector that resolves the effective policy snapshot for each request.</param>
    /// <param name="identityValidation">
    /// The optional identity validation policy that revalidates request identity before policy evaluation. When
    /// omitted, the authority does not perform this check.
    /// </param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public SecurityAuthority(
        IEnumerable<ISecurityPolicy> policies,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<GrantId> grantIds,
        TimeProvider timeProvider,
        IOptions<AgentPermissionOptions> options,
        ISecurityPolicySelector policySelector,
        ILogger<SecurityAuthority>? logger = null,
        IIdentityValidationPolicy? identityValidation = null)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(grantIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(policySelector);
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
        _grantIds = grantIds;
        _timeProvider = timeProvider;
        _policyVersion = optionValues.PolicyVersion;
        _revocationVersion = optionValues.RevocationVersion;
        _maximumGrantLifetime = optionValues.MaximumGrantLifetime;
        _maximumGrantUses = optionValues.MaximumGrantUses;
        _boundPolicySnapshot = optionValues.PolicySnapshot;
        _policySelector = policySelector;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityAuthority>.Instance;
        _identityValidation = identityValidation;
    }

    /// <summary>Initializes the authority with approval coordination and required approval audit delivery.</summary>
    /// <param name="policies">The ordered additive policies.</param>
    /// <param name="grantStore">The authoritative grant state store.</param>
    /// <param name="grantIds">The grant identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated permission configuration.</param>
    /// <param name="approvalBroker">The trusted approval coordinator.</param>
    /// <param name="approvalRequestIds">The approval-request identity generator.</param>
    /// <param name="auditDispatcher">The required security-audit dispatcher.</param>
    /// <param name="policySelector">The selector that resolves the effective policy snapshot for each request.</param>
    /// <param name="logger">The optional safe diagnostic logger.</param>
    /// <param name="identityValidation">
    /// The optional identity validation policy that revalidates request identity before policy evaluation.
    /// </param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public SecurityAuthority(
        IEnumerable<ISecurityPolicy> policies,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<GrantId> grantIds,
        TimeProvider timeProvider,
        IOptions<AgentPermissionOptions> options,
        ISecurityPolicySelector policySelector,
        IApprovalBroker approvalBroker,
        IIdentifierGenerator<ApprovalRequestId> approvalRequestIds,
        ISecurityAuditDispatcher auditDispatcher,
        ILogger<SecurityAuthority>? logger = null,
        IIdentityValidationPolicy? identityValidation = null)
        : this(policies, grantStore, grantIds, timeProvider, options, policySelector, logger, identityValidation)
    {
        ArgumentNullException.ThrowIfNull(approvalBroker);
        ArgumentNullException.ThrowIfNull(approvalRequestIds);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        _approvalBroker = approvalBroker;
        _approvalRequestIds = approvalRequestIds;
        _auditDispatcher = auditDispatcher;
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
        var now = _timeProvider.GetUtcNow();
        if (request.Authorization is { } authorization
            && (authorization.Scope != request.Scope
                || authorization.Identity != request.Identity
                || _boundPolicySnapshot is null
                || authorization.PolicySnapshot != _boundPolicySnapshot))
        {
            return Denied(request, policyVersion, "security.captured_context_mismatch",
                "The captured authorization context cannot be evaluated by this authority version.");
        }
        if (request.Deadline <= now)
        {
            return Denied(request, policyVersion, "security.deadline_expired", "The authorization deadline has expired.");
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
                return Denied(
                    request,
                    policyVersion,
                    AgentErrorCodes.CredentialUnavailable.ToString(),
                    "Identity validation is unavailable.");
            }

            if (validation is IdentityValidationRejected rejected)
            {
                return Denied(
                    request,
                    policyVersion,
                    MapIdentityValidationDenialCode(rejected.Failure.Kind),
                    rejected.Failure.SafeMessage);
            }

            if (validation is not IdentityValidationPassed)
            {
                return Denied(
                    request,
                    policyVersion,
                    AgentErrorCodes.AuthenticationFailed.ToString(),
                    "Identity validation returned an unsupported result.");
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
            return Denied(
                request,
                policyVersion,
                "security.policy_snapshot_unavailable",
                "Policy snapshot selection is unavailable.");
        }

        switch (snapshotSelection)
        {
            case SecurityPolicySnapshotUnavailable unavailable:
                return Denied(
                    request,
                    policyVersion,
                    "security.policy_snapshot_unavailable",
                    unavailable.SafeReason);
            case SecurityPolicySnapshotStale stale:
                return Denied(
                    request,
                    policyVersion,
                    "security.policy_snapshot_stale",
                    stale.SafeReason);
            case SecurityPolicySnapshotResolved:
                break;
            default:
                return Denied(
                    request,
                    policyVersion,
                    "security.policy_snapshot_unavailable",
                    "Policy snapshot selection returned an unsupported result.");
        }

        var policyContext = SecurityPolicyEvaluationContexts.Create(
            request,
            policyVersion,
            _boundPolicySnapshot,
            new SecurityRevocationVersion(_revocationVersion),
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
                return Denied(
                    request,
                    policyVersion,
                    "security.policy_evaluation_failed",
                    "Security policy evaluation failed.");
            }

            if (result.Kind == SecurityPolicyResultKind.Deny)
            {
                hardDenial ??= result;
                continue;
            }

            if (result.Kind == SecurityPolicyResultKind.Allow)
            {
                allowed = true;
            }
            else if (result.Kind == SecurityPolicyResultKind.RequireApproval)
            {
                approvalRequired = true;
            }

            if (result is { Kind: SecurityPolicyResultKind.Allow or SecurityPolicyResultKind.RequireApproval, Constraints: { } constraints })
            {
                constraintContributions.Add(constraints);
            }
        }

        if (hardDenial is not null)
        {
            return Denied(request, policyVersion, hardDenial.Code!, hardDenial.SafeMessage!);
        }

        if (!allowed && !approvalRequired)
        {
            return Denied(request, policyVersion, "security.no_policy", "No security policy authorized this operation.");
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
                out _))
        {
            return Denied(
                request,
                policyVersion,
                "security.constraint_intersection_empty",
                "The intersected policy constraints deny this operation.");
        }

        ApprovalRequestId? approvalRequestId = null;
        ApprovalScopeBinding? approvedBinding = null;
        if (approvalRequired)
        {
            if (_approvalBroker is null || _approvalRequestIds is null || _auditDispatcher is null)
            {
                return Denied(request, policyVersion, "security.approval_unavailable",
                    "Approval is required but approval coordination is unavailable.");
            }

            var approvalExpiry = request.Deadline < now + _maximumGrantLifetime
                ? request.Deadline
                : now + _maximumGrantLifetime;
            var binding = new ApprovalScopeBinding(
                request,
                policyVersion,
                new SecurityRevocationVersion(_revocationVersion),
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
                    ApprovalBrokerExpired => Denied(request, policyVersion, "security.approval_expired", "The required approval expired."),
                    ApprovalBrokerUnavailable => Denied(request, policyVersion, "security.approval_unavailable",
                        "Approval is required but approval coordination is unavailable."),
                    _ => Denied(request, policyVersion, "security.approval_denied", "The required approval was not granted."),
                };
            }

            now = _timeProvider.GetUtcNow();
            if (now >= request.Deadline
                || now >= approved.Response.Binding.ExpiresAt
                || approved.Response.RequestId != approval.Id
                || approved.Response.Binding != binding
                || approved.Response.Resolution != ApprovalResolution.Approved
                || approved.Response.Binding.RevocationVersion.Value != _revocationVersion)
            {
                return Denied(request, policyVersion, "security.approval_stale",
                    "The approval no longer authorizes this request.");
            }

            approvedBinding = approved.Response.Binding;
        }

        // When a human approved a binding, the issued grant is bounded by exactly what was approved: it must not
        // outlive the approved expiry merely because the lifetime window is re-based on the post-approval clock.
        var maximumExpiry = now + _maximumGrantLifetime;
        if (approvedBinding is not null && approvedBinding.ExpiresAt < maximumExpiry)
        {
            maximumExpiry = approvedBinding.ExpiresAt;
        }

        var grantId = _grantIds.Create();
        var revocationVersion = new SecurityRevocationVersion(_revocationVersion);
        var expiresAt = request.Deadline < maximumExpiry ? request.Deadline : maximumExpiry;
        var allowedUses = Math.Min(request.RequestedUses, approvedBinding?.AllowedUses ?? _maximumGrantUses);
        var grant = request.Authorization is { } capturedForGrant
            ? new SecurityGrant(
                grantId, request.Id, request.Scope, request.Identity, capturedForGrant, request.Audience,
                request.Kind, request.Effect, request.Resources, request.InputFingerprint, policyVersion,
                revocationVersion, now, expiresAt, allowedUses)
            : new SecurityGrant(
                grantId, request.Id, request.Scope, request.Identity, request.Audience, request.Kind,
                request.Effect, request.Resources, request.InputFingerprint, policyVersion,
                revocationVersion, now, expiresAt, allowedUses);

        // Every registered grant is audited when a dispatcher is configured, not only the ones a human
        // approved: the ordinary allow path (AllowAllSecurityPolicy, a workspace-scoped policy, ...)
        // issues a grant just as authoritatively and must be equally visible to required audit.
        if (_auditDispatcher is not null)
        {
            var audit = new SecurityAuditRecord(
                new SecurityAuditRecordId(grant.Id.Value),
                request.Scope,
                request.Id,
                grant.Id,
                approvalRequestId,
                SecurityAuditEventKind.GrantIssued,
                SecurityAuditOutcome.Accepted,
                policyVersion,
                [],
                now);
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
                return Denied(request, policyVersion, "security.audit_unavailable",
                    "Required security audit failed.");
            }
            if (auditResult is not SecurityAuditAccepted)
            {
                return Denied(request, policyVersion, "security.audit_unavailable",
                    "Required security audit was not accepted.");
            }
        }
        await _grantStore.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, policyVersion, grant);
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
