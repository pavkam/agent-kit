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
    private readonly AgentPermissionOptions _options;
    private readonly ILogger<SecurityAuthority> _logger;

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
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public SecurityAuthority(
        IEnumerable<ISecurityPolicy> policies,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<GrantId> grantIds,
        TimeProvider timeProvider,
        IOptions<AgentPermissionOptions> options,
        ILogger<SecurityAuthority>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(grantIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.PolicyVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.RevocationVersion);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Value.MaximumGrantLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumGrantUses);
        _policies = [.. policies];
        _grantStore = grantStore;
        _grantIds = grantIds;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityAuthority>.Instance;
    }

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
        var policyVersion = new SecurityPolicyVersion(_options.PolicyVersion);
        var now = _timeProvider.GetUtcNow();
        if (request.Deadline <= now)
        {
            return Denied(request, policyVersion, "security.deadline_expired", "The authorization deadline has expired.");
        }

        var allowed = false;
        foreach (var policy in _policies)
        {
            SecurityPolicyResult result;
            try
            {
                result = await policy.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
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
                return Denied(request, policyVersion, result.Code!, result.SafeMessage!);
            }

            allowed |= result.Kind == SecurityPolicyResultKind.Allow;
        }

        if (!allowed)
        {
            return Denied(request, policyVersion, "security.no_policy", "No security policy authorized this operation.");
        }

        var maximumExpiry = now + _options.MaximumGrantLifetime;
        var grant = new SecurityGrant(
            _grantIds.Create(),
            request.Id,
            request.Scope,
            request.Identity,
            request.Audience,
            request.Kind,
            request.Effect,
            request.Resources,
            request.InputFingerprint,
            policyVersion,
            new SecurityRevocationVersion(_options.RevocationVersion),
            now,
            request.Deadline < maximumExpiry ? request.Deadline : maximumExpiry,
            Math.Min(request.RequestedUses, _options.MaximumGrantUses));
        await _grantStore.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, policyVersion, grant);
    }

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
