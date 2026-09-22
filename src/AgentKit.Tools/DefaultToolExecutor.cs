// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.Extensions.Options;

/// <summary>
/// Runs the spec-shaped tool pipeline: resolve, validate, authorize, schedule, invoke with retries, and normalize one
/// terminal <see cref="ToolCallResult"/> per call.
/// </summary>
/// <remarks>
/// Durable recording, result projection, hooks, and artifact spill for oversized results belong to later workstream
/// chunks; this executor preflights every call, executes prepared entries through <see cref="IToolScheduler"/>, and
/// does not invoke <see cref="IToolResultProjector"/>.
/// </remarks>
public sealed class DefaultToolExecutor: IToolExecutor
{
    private readonly IToolResolver _resolver;
    private readonly IToolArgumentValidator _argumentValidator;
    private readonly ISecurityAuthoritySelector _securityAuthorities;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IToolScheduler _scheduler;
    private readonly ToolSchemaLimits _argumentValidationLimits;
    private readonly ToolRuntimeOptions _runtimeOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultToolExecutor> _logger;

    /// <summary>Initializes the first-party spec-shaped tool executor.</summary>
    /// <param name="resolver">The catalog resolver that binds aliases and acquires invoker leases.</param>
    /// <param name="argumentValidator">The argument validator applied after resolution.</param>
    /// <param name="securityAuthorities">The selector that activates the security authority for each call.</param>
    /// <param name="securityRequestIds">The identifier generator for invocation authorization requests.</param>
    /// <param name="scheduler">The scheduler that invokes prepared entries under barrier-segment policy.</param>
    /// <param name="argumentValidationLimits">The bounds applied to argument validation for each call.</param>
    /// <param name="runtimeOptions">The configured tool runtime limits and scheduling defaults.</param>
    /// <param name="timeProvider">The replaceable clock used for timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultToolExecutor(
        IToolResolver resolver,
        IToolArgumentValidator argumentValidator,
        ISecurityAuthoritySelector securityAuthorities,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IToolScheduler scheduler,
        ToolSchemaLimits argumentValidationLimits,
        IOptions<ToolRuntimeOptions> runtimeOptions,
        TimeProvider timeProvider,
        ILogger<DefaultToolExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(argumentValidator);
        ArgumentNullException.ThrowIfNull(securityAuthorities);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(argumentValidationLimits);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _resolver = resolver;
        _argumentValidator = argumentValidator;
        _securityAuthorities = securityAuthorities;
        _securityRequestIds = securityRequestIds;
        _scheduler = scheduler;
        _argumentValidationLimits = argumentValidationLimits;
        _runtimeOptions = runtimeOptions.Value;
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

        var orderedResults = new ToolCallResult?[calls.Length];
        var batchEntries = ImmutableArray.CreateBuilder<ToolBatchEntry>(calls.Length);
        var resultIndexByCallId = new Dictionary<ToolCallId, int>(calls.Length);

        for (var index = 0; index < calls.Length; index++)
        {
            var request = calls[index];
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var preflight = await PreflightAsync(capture, request, cancellationToken).ConfigureAwait(false);
            if (preflight.EarlyResult is { } early)
            {
                orderedResults[index] = early;
                continue;
            }

            batchEntries.Add(preflight.Entry!);
            resultIndexByCallId[preflight.Entry!.Invocation.CallId] = index;
        }

        if (batchEntries.Count > 0)
        {
            var anchor = calls[0]!;
            var deadline = _timeProvider.GetUtcNow().Add(_runtimeOptions.InvocationTimeout);
            var batch = new ToolBatch(
                anchor.AgentId,
                anchor.SessionId,
                anchor.RunId,
                batchEntries.ToImmutable(),
                _runtimeOptions.BatchFailureMode,
                _runtimeOptions.UnknownSchedulingMode,
                deadline);

            var scheduled = await _scheduler.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            foreach (var result in scheduled.Results)
            {
                if (resultIndexByCallId.TryGetValue(result.CallId, out var resultIndex))
                {
                    orderedResults[resultIndex] = result;
                }
            }
        }

        var builder = ImmutableArray.CreateBuilder<ToolCallResult>(calls.Length);
        for (var index = 0; index < orderedResults.Length; index++)
        {
            if (orderedResults[index] is not { } result)
            {
                throw new InvalidOperationException("Every tool call must produce exactly one terminal result.");
            }

            builder.Add(result);
        }

        return new ToolBatchResult(builder.ToImmutable());
    }

    private async Task<PreflightOutcome> PreflightAsync(
        IToolCatalogCapture capture,
        ToolCallRequest request,
        CancellationToken cancellationToken)
    {
        var completedAt = _timeProvider.GetUtcNow();
        var resolution = await _resolver.ResolveAsync(capture, request, cancellationToken).ConfigureAwait(false);
        if (resolution is ToolCallUnresolved unresolved)
        {
            return new PreflightOutcome(
                ToolCallResultComposer.PreInvocation(
                    request,
                    unresolved.Status,
                    unresolved.SafeReason,
                    toolId: null,
                    toolVersion: null,
                    effects: null,
                    ToolRuntimeNormalizationDefaults.RejectionSnapshot,
                    completedAt),
                null);
        }

        var resolved = (ToolCallResolved) resolution;
        var lease = resolved.Lease;
        var validation = await _argumentValidator
            .ValidateAsync(resolved.Call, _argumentValidationLimits, cancellationToken)
            .ConfigureAwait(false);
        if (validation is ToolCallValidationFailed failed)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            return new PreflightOutcome(ToolCallResultComposer.FromValidationFailure(failed, _timeProvider.GetUtcNow()), null);
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
            await lease.DisposeAsync().ConfigureAwait(false);
            return new PreflightOutcome(
                ToolCallResultComposer.PreInvocation(
                    request,
                    ToolTerminalStatus.InvocationFailed,
                    "The tool could not be authorized.",
                    validated.Tool.Id,
                    validated.ToolVersion,
                    validated.Tool.Effects,
                    ToolRuntimeNormalizationDefaults.ForResolvedTool(validated.ExecutionPolicy),
                    _timeProvider.GetUtcNow()),
                null);
        }

        if (invocationGrant is null)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            return new PreflightOutcome(
                ToolCallResultComposer.PreInvocation(
                    request,
                    ToolTerminalStatus.Denied,
                    "The tool invocation was denied.",
                    validated.Tool.Id,
                    validated.ToolVersion,
                    validated.Tool.Effects,
                    ToolRuntimeNormalizationDefaults.ForResolvedTool(validated.ExecutionPolicy),
                    _timeProvider.GetUtcNow()),
                null);
        }

        var invocationStartedAt = _timeProvider.GetUtcNow();
        var deadline = invocationStartedAt.Add(_runtimeOptions.InvocationTimeout);
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

        var entry = new ToolBatchEntry(
            context,
            lease,
            validated.Tool.ExecutionHints,
            validated.SourceOrdinal);

        return new PreflightOutcome(null, entry);
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
            deadline: _timeProvider.GetUtcNow().Add(_runtimeOptions.InvocationTimeout));

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

    private sealed record PreflightOutcome(ToolCallResult? EarlyResult, ToolBatchEntry? Entry);
}
