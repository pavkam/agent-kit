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
    private readonly IHookDispatcher? _hookDispatcher;
    private readonly ILogger<LegacyToolInvokerExecutor> _logger;

    /// <summary>Initializes the legacy adapter executor.</summary>
    /// <param name="orchestrator">The legacy orchestrator that resolves, authorizes, and invokes tools.</param>
    /// <param name="timeProvider">The clock used for terminal timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <param name="hookDispatcher">Optional hook dispatcher for before/result tool hooks.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public LegacyToolInvokerExecutor(
        ILegacyToolCallOrchestrator orchestrator,
        TimeProvider timeProvider,
        ILogger<LegacyToolInvokerExecutor> logger,
        IHookDispatcher? hookDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _orchestrator = orchestrator;
        _timeProvider = timeProvider;
        _hookDispatcher = hookDispatcher;
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

            var rawArguments = call.RawArguments;
            if (capability.Hooks is { } hooks && _hookDispatcher is not null)
            {
                var callPart = ToolExecutionHookDispatcher.CreateCallPart(capture.Snapshot, call);
                var before = await ToolExecutionHookDispatcher.DispatchBeforeToolInvocationAsync(
                    _hookDispatcher,
                    hooks,
                    call.AgentId,
                    call.SessionId,
                    callPart,
                    cancellationToken).ConfigureAwait(false);
                if (before.Veto is { } veto)
                {
                    results.Add(
                        ToolCallResultComposer.PreInvocation(
                            call,
                            ToolTerminalStatus.Unsupported,
                            $"The call was vetoed before invocation: {veto.SafeReason}",
                            toolId: null,
                            toolVersion: null,
                            effects: null,
                            ToolRuntimeNormalizationDefaults.RejectionSnapshot,
                            _timeProvider.GetUtcNow()));
                    continue;
                }

                rawArguments = ToolExecutionHookDispatcher.ToRawArguments(before.Arguments);
            }

            var legacyRequest = new LegacyToolCallRequest(
                new ToolReference(call.ProviderAlias, null, null),
                legacyContext,
                LegacyToolCallResultFactory.ParseRawArguments(rawArguments),
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
            var terminal = LegacyToolCallResultFactory.Create(call, resolved, _timeProvider.GetUtcNow());
            terminal = await ToolExecutionHookDispatcher.DispatchToolResultAsync(
                _hookDispatcher,
                capability.Hooks,
                terminal.AgentId,
                terminal.SessionId,
                terminal.RunId,
                terminal,
                cancellationToken).ConfigureAwait(false);
            results.Add(terminal);
        }

        return new ToolBatchResult(results.ToImmutable());
    }
}
