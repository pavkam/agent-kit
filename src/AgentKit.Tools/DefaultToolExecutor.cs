// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Diagnostics;

/// <summary>
/// Runs the spec-shaped sequential tool pipeline: resolve, validate, authorize, invoke, normalize, and record one
/// terminal <see cref="ToolCallResult"/> per call.
/// </summary>
/// <remarks>
/// Scheduling, retries, durable recording, hooks, and oversized-result spill belong to later workstream chunks; this
/// executor executes calls sequentially and does not invoke <see cref="IToolResultProjector"/>.
/// </remarks>
public sealed class DefaultToolExecutor: IToolExecutor
{
    private readonly IToolResolver _resolver;
    private readonly IToolArgumentValidator _argumentValidator;
    private readonly ISecurityAuthoritySelector _securityAuthorities;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IToolResultNormalizer _resultNormalizer;
    private readonly ToolSchemaLimits _argumentValidationLimits;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultToolExecutor> _logger;

    /// <summary>Initializes the first-party spec-shaped tool executor.</summary>
    /// <param name="resolver">The catalog resolver that binds aliases and acquires invoker leases.</param>
    /// <param name="argumentValidator">The argument validator applied after resolution.</param>
    /// <param name="securityAuthorities">The selector that activates the security authority for each call.</param>
    /// <param name="securityRequestIds">The identifier generator for invocation authorization requests.</param>
    /// <param name="resultNormalizer">The normalizer that maps raw invocation evidence into terminal content.</param>
    /// <param name="argumentValidationLimits">The bounds applied to argument validation for each call.</param>
    /// <param name="timeProvider">The replaceable clock used for timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultToolExecutor(
        IToolResolver resolver,
        IToolArgumentValidator argumentValidator,
        ISecurityAuthoritySelector securityAuthorities,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IToolResultNormalizer resultNormalizer,
        ToolSchemaLimits argumentValidationLimits,
        TimeProvider timeProvider,
        ILogger<DefaultToolExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(argumentValidator);
        ArgumentNullException.ThrowIfNull(securityAuthorities);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(resultNormalizer);
        ArgumentNullException.ThrowIfNull(argumentValidationLimits);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _resolver = resolver;
        _argumentValidator = argumentValidator;
        _securityAuthorities = securityAuthorities;
        _securityRequestIds = securityRequestIds;
        _resultNormalizer = resultNormalizer;
        _argumentValidationLimits = argumentValidationLimits;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolBatchResult> ExecuteAsync(
        IToolCatalogCapture capture,
        ImmutableArray<ToolCallRequest> calls,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfDefault(calls);

        if (calls.Length == 0)
        {
            return new ToolBatchResult([]);
        }

        var results = ImmutableArray.CreateBuilder<ToolCallResult>(calls.Length);
        foreach (var call in calls)
        {
            ArgumentNullException.ThrowIfNull(call);
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ExecuteSingleAsync(capture, call, cancellationToken).ConfigureAwait(false));
        }

        return new ToolBatchResult(results.ToImmutable());
    }

    private async Task<ToolCallResult> ExecuteSingleAsync(
        IToolCatalogCapture capture,
        ToolCallRequest request,
        CancellationToken cancellationToken)
    {
        var completedAt = _timeProvider.GetUtcNow();
        var resolution = await _resolver.ResolveAsync(capture, request, cancellationToken).ConfigureAwait(false);
        if (resolution is ToolCallUnresolved unresolved)
        {
            return ToolCallResultComposer.PreInvocation(
                request,
                unresolved.Status,
                unresolved.SafeReason,
                toolId: null,
                toolVersion: null,
                effects: null,
                ToolRuntimeNormalizationDefaults.RejectionSnapshot,
                completedAt);
        }

        var resolved = (ToolCallResolved) resolution;
        await using var lease = resolved.Lease;
        var validation = await _argumentValidator.ValidateAsync(resolved.Call, _argumentValidationLimits, cancellationToken)
            .ConfigureAwait(false);
        if (validation is ToolCallValidationFailed failed)
        {
            return ToolCallResultComposer.FromValidationFailure(failed, _timeProvider.GetUtcNow());
        }

        var validated = ((ToolCallValidated) validation).Call;
        SecurityGrant? invocationGrant;
        try
        {
            invocationGrant = await AuthorizeInvocationAsync(validated, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ToolLog.Failed(_logger, request.CallId, validated.Tool.Id, exception.GetType().Name);
            return ToolCallResultComposer.PreInvocation(
                request,
                ToolTerminalStatus.InvocationFailed,
                "The tool could not be authorized.",
                validated.Tool.Id,
                validated.ToolVersion,
                validated.Tool.Effects,
                ToolRuntimeNormalizationDefaults.ForResolvedTool(validated.ExecutionPolicy),
                _timeProvider.GetUtcNow());
        }

        if (invocationGrant is null)
        {
            return ToolCallResultComposer.PreInvocation(
                request,
                ToolTerminalStatus.Denied,
                "The tool invocation was denied.",
                validated.Tool.Id,
                validated.ToolVersion,
                validated.Tool.Effects,
                ToolRuntimeNormalizationDefaults.ForResolvedTool(validated.ExecutionPolicy),
                _timeProvider.GetUtcNow());
        }

        var invocationStartedAt = _timeProvider.GetUtcNow();
        var deadline = invocationStartedAt.AddMinutes(1);
        var context = new ToolInvocationContext(
            validated.AgentId,
            validated.SessionId,
            validated.RunId,
            validated.TurnId,
            validated.OperationId,
            validated.CallId,
            validated.Tool,
            validated.ToolVersion,
            validated.Arguments,
            invocationGrant,
            attempt: 1,
            validated.RequestedAt,
            invocationStartedAt,
            deadline,
            NoopToolProgressReporter.Instance);

        ToolInvocationResult invocation;
        try
        {
            invocation = await lease.Invoker.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ToolLog.Failed(_logger, request.CallId, validated.Tool.Id, exception.GetType().Name);
            invocation = new ToolInvocationResult(
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Failed,
                    ToolTerminalStatus.InvocationFailed,
                    SideEffectCertainty.DefinitelyNotPerformed,
                    retryable: false,
                    "The tool invoker faulted before producing a result.",
                    ExtensionData.Empty),
                []);
        }

        var snapshot = ToolRuntimeNormalizationDefaults.ForResolvedTool(validated.ExecutionPolicy);
        var normalization = await _resultNormalizer
            .NormalizeAsync(validated, invocation, snapshot, cancellationToken)
            .ConfigureAwait(false);
        return ToolCallResultComposer.FromInvocation(
            request,
            validated,
            invocation,
            normalization,
            invocationGrant,
            invocationStartedAt,
            _timeProvider.GetUtcNow());
    }

    private async Task<SecurityGrant?> AuthorizeInvocationAsync(ValidatedToolCall call, CancellationToken cancellationToken)
    {
        var selection = await _securityAuthorities.SelectAsync(call.Authorization, cancellationToken).ConfigureAwait(false);
        if (selection is not SecurityAuthoritySelected selected || selected.Authorization != call.Authorization)
        {
            return null;
        }

        var effect = call.Tool.Effects.Effect;
        var request = new SecurityRequest(
            id: _securityRequestIds.Create(),
            scope: call.Authorization.Scope,
            toolCallId: call.CallId,
            identity: call.Authorization.Identity,
            authorization: call.Authorization,
            audience: ToolInvocationSecurityBinding.SecurityAudience,
            kind: ToolInvocationSecurityBinding.OperationKind(effect),
            effect: ToolInvocationSecurityBinding.ToSecurityEffect(effect),
            resources: [ToolInvocationSecurityBinding.Resource(call.Tool.Id, call.ToolVersion)],
            inputFingerprint: ToolInvocationSecurityBinding.InvocationFingerprint(call.CallId, call.InputFingerprint),
            deadline: _timeProvider.GetUtcNow().AddMinutes(1));

        var decision = await selected.Authority.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        return decision switch
        {
            SecurityAllowed allowed => EnsureAuthorization(allowed.Grant, call.Authorization),
            SecurityDenied => null,
            _ => null,
        };
    }

    private static SecurityGrant EnsureAuthorization(SecurityGrant grant, SecurityAuthorizationContext authorization)
    {
        Debug.Assert(grant is not null && authorization is not null, "Authorization evidence must be present.");
        return grant.Authorization is not null
            ? grant
            : new SecurityGrant(
                grant.Id,
                grant.RequestId,
                grant.Scope,
                grant.Identity,
                authorization,
                grant.Audience,
                grant.Kind,
                grant.Effect,
                grant.Resources,
                grant.InputFingerprint,
                grant.PolicyVersion,
                grant.RevocationVersion,
                grant.NotBefore,
                grant.ExpiresAt,
                grant.AllowedUses);
    }
}
