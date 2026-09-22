// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;
using System.Diagnostics;

/// <summary>
/// Adapts the legacy <see cref="ILegacyToolCallOrchestrator"/> to <see cref="IToolExecutor"/> by executing calls
/// sequentially and mapping results into <see cref="ToolCallResult"/> records.
/// </summary>
/// <remarks>
/// This executor ignores invoker lease acquisition on the legacy catalog capture; it exists only until the spec-shaped
/// <see cref="IToolExecutor"/> replaces the legacy path (workstream 4 chunks C5a and C10a).
/// </remarks>
public sealed class LegacyToolInvokerExecutor: IToolExecutor
{
    private readonly ILegacyToolCallOrchestrator _orchestrator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LegacyToolInvokerExecutor> _logger;

    /// <summary>Initializes the legacy adapter executor.</summary>
    /// <param name="orchestrator">The legacy orchestrator that resolves, authorizes, and invokes tools.</param>
    /// <param name="timeProvider">The clock used for terminal timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public LegacyToolInvokerExecutor(
        ILegacyToolCallOrchestrator orchestrator,
        TimeProvider timeProvider,
        ILogger<LegacyToolInvokerExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _orchestrator = orchestrator;
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

            var legacyContext = new ToolExecutionContext(
                call.AgentId,
                call.SessionId,
                call.CallId,
                (InRunOperationCorrelation) call.Authorization.Scope.Correlation,
                call.Authorization.Identity,
                call.Authorization,
                capability.Session.Profile);

            var legacyRequest = new LegacyToolCallRequest(
                new ToolReference(call.ProviderAlias, null, null),
                legacyContext,
                LegacyToolCallResultFactory.ParseRawArguments(call.RawArguments),
                call.RequestedAt);

            ResolvedToolInvocation resolved;
            try
            {
                resolved = await _orchestrator.InvokeAsync(legacyRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ToolLog.Failed(_logger, call.CallId, new ToolId(call.ProviderAlias.Value), exception.GetType().Name);
                resolved = new ResolvedToolInvocation(
                    new ToolReference(call.ProviderAlias, null, null),
                    ToolResultProjectionPolicyReference.Default,
                    new ToolInvocationResult(
                        new ToolCallOutcome(
                            ToolCallOutcomeKind.Failed,
                            ToolTerminalStatus.InvocationFailed,
                            SideEffectCertainty.DefinitelyNotPerformed,
                            retryable: false,
                            "The legacy tool orchestrator faulted before producing a result.",
                            ExtensionData.Empty),
                        []));
            }

            Debug.Assert(capture.Snapshot is not null, "A catalog capture must expose its snapshot.");
            results.Add(LegacyToolCallResultFactory.Create(call, resolved, _timeProvider.GetUtcNow()));
        }

        return new ToolBatchResult(results.ToImmutable());
    }
}
