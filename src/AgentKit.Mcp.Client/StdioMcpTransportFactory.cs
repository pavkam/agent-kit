// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Opens stdio MCP transports through the protected process boundary.</summary>
internal sealed class StdioMcpTransportFactory(
    IProcessExecutorSelector processExecutors,
    ISecurityAuthoritySelector securityAuthorities,
    ISecurityGrantStore grantStore,
    ISecurityAuditDispatcher audit,
    IIdentifierGenerator<SecurityRequestId> securityRequestIds,
    IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
    IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
    IIdentifierGenerator<ProcessOperationId> processOperationIds,
    McpClientOptionsSnapshot clientOptions,
    TimeProvider timeProvider): IMcpTransportFactory
{
    private readonly IProcessExecutorSelector _processExecutors = processExecutors;
    private readonly ISecurityAuthoritySelector _securityAuthorities = securityAuthorities;
    private readonly ISecurityGrantStore _grantStore = grantStore;
    private readonly ISecurityAuditDispatcher _audit = audit;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds = securityRequestIds;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds = auditRecordIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds = intentIds;
    private readonly IIdentifierGenerator<ProcessOperationId> _processOperationIds = processOperationIds;
    private readonly McpClientOptionsSnapshot _clientOptions = clientOptions;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Type TransportProfileType => typeof(McpStdioTransportProfile);

    /// <inheritdoc/>
    public async ValueTask<McpTransportOpenResult> OpenAsync(
        McpTransportOpenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Endpoint.Transport is not McpStdioTransportProfile stdio)
        {
            return new McpTransportOpenFailed(
                "The stdio transport factory received a non-stdio endpoint profile.");
        }

        var operation = request.SessionOpenRequest.Operation;
        var connectGrant = await McpClientSecurityOperations.AuthorizeConnectAsync(
            operation,
            request.Endpoint,
            _securityAuthorities,
            _grantStore,
            _audit,
            _securityRequestIds,
            _auditRecordIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        if (connectGrant is null)
        {
            return new McpTransportOpenDenied("Stdio MCP connect denied by security authority.");
        }

        var connectConsumption = await McpClientSecurityOperations.ConsumeGrantAsync(
            connectGrant,
            _grantStore,
            _audit,
            _auditRecordIds,
            _intentIds,
            _timeProvider,
            cancellationToken).ConfigureAwait(false);
        if (connectConsumption.Status is not GrantConsumptionStatus.Consumed)
        {
            return new McpTransportOpenDenied("Stdio MCP connect grant consumption failed.");
        }

        var selection = await _processExecutors.SelectAsync(_clientOptions.StdioProcessExecutorKey, cancellationToken)
            .ConfigureAwait(false);
        if (selection is not ProcessExecutorSelected selected)
        {
            return new McpTransportOpenFailed(
                $"Process executor '{_clientOptions.StdioProcessExecutorKey.Value}' is not registered.");
        }

        var bounds = request.Endpoint.Bounds;
        var limits = new ProcessResourceLimits(
            bounds.RequestTimeout,
            bounds.MaximumMessageBytes,
            bounds.ShutdownTimeout,
            ProcessStandardInputDelivery.Streaming);
        var startRequest = CreateStartRequest(operation, stdio, limits);
        var resolution = await selected.Resolver.ResolveAsync(startRequest, cancellationToken).ConfigureAwait(false);
        if (resolution is ExecutableResolutionFailed failed)
        {
            return new McpTransportOpenFailed(failed.SafeMessage);
        }

        if (resolution is not ExecutableResolved { Resolved: var resolved })
        {
            return new McpTransportOpenDenied("Executable resolution was denied.");
        }

        var intent = ProcessStartBinding.ToResolvedProcessIntent(resolved);
        var processGrant = await AuthorizeProcessStartAsync(operation, intent, selected.Executor.SecurityAudience, cancellationToken)
            .ConfigureAwait(false);
        if (processGrant is null)
        {
            return new McpTransportOpenDenied("Stdio MCP process start denied by security authority.");
        }

        var start = await selected.Executor.StartAsync(resolved, processGrant, cancellationToken).ConfigureAwait(false);
        if (start is ProcessHandleStarted { Handle: var handle })
        {
            var streams = McpProcessHandleStreams.Start(handle, cancellationToken);
            var sdkTransport = streams.CreateTransport();
            var transport = new StdioMcpClientTransport(streams, sdkTransport);
            return new McpTransportOpened(transport);
        }

        return start switch
        {
            ProcessStartDenied denied => new McpTransportOpenDenied(denied.SafeMessage),
            ProcessStartResolutionFailed resolutionFailed => new McpTransportOpenFailed(resolutionFailed.SafeMessage),
            ProcessStartSandboxUnavailable sandbox => new McpTransportOpenFailed(sandbox.SafeMessage),
            ProcessStartCancelled => new McpTransportOpenFailed("Process start cancelled."),
            ProcessStartFailed failedStart => new McpTransportOpenFailed(failedStart.SafeMessage),
            _ => new McpTransportOpenFailed("Unsupported process start outcome."),
        };
    }

    private ProcessStartRequest CreateStartRequest(
        ProtectedSemanticOperationContext operation,
        McpStdioTransportProfile stdio,
        ProcessResourceLimits limits)
    {
        var correlation = operation.Correlation;
        RunId? runId = correlation is InRunOperationCorrelation inRun ? inRun.RunId : null;
        return new ProcessStartRequest(
            _processOperationIds.Create(),
            correlation.OperationId,
            operation.AgentId,
            runId,
            new ProcessExecutableReference(stdio.Command),
            [.. stdio.Arguments.Select(static argument => new ProcessArgument(argument))],
            new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath(".")),
            new EnvironmentProjection([]),
            standardInput: null,
            _clientOptions.StdioSandboxProfileId,
            limits,
            ProcessEffectClass.ExternalEffect);
    }

    private async ValueTask<SecurityGrant?> AuthorizeProcessStartAsync(
        ProtectedSemanticOperationContext operation,
        ResolvedProcessIntent intent,
        ComponentId audience,
        CancellationToken cancellationToken)
    {
        var request = new SecurityRequest(
            _securityRequestIds.Create(),
            operation.Authorization.Scope,
            toolCallId: null,
            operation.Identity,
            operation.Authorization,
            audience,
            SecurityOperationKind.Process,
            SecurityEffect.Execute,
            ProcessSecurityBinding.Resources(intent),
            ProcessSecurityBinding.Fingerprint(intent),
            _timeProvider.GetUtcNow().AddMinutes(5));
        var selection = await _securityAuthorities.SelectAsync(operation.Authorization, cancellationToken).ConfigureAwait(false);
        if (selection is not SecurityAuthoritySelected selected)
        {
            return null;
        }

        var decision = await selected.Authority.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return null;
        }

        await _grantStore.RegisterAsync(allowed.Grant, cancellationToken).ConfigureAwait(false);
        return allowed.Grant;
    }
}
