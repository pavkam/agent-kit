// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Maps inbound MCP tool requests onto the shared tool executor.</summary>
/// <remarks>Each handled request runs under an <c>mcp.server.request</c> activity with bounded outcome metrics and content-free logs.</remarks>
public sealed class AgentKitPrimitiveHandler: IMcpPrimitiveHandler
{
    private readonly IServiceProvider _services;
    private readonly McpServerObservation _observation;

    /// <summary>Initializes the handler over the composition it resolves tool-execution collaborators from.</summary>
    /// <param name="services">The non-null provider used to resolve the tool executor, catalog capture factory, and session coordinators per request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public AgentKitPrimitiveHandler(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        _observation = new McpServerObservation(loggerFactory.CreateLogger<McpServerObservation>());
    }

    /// <inheritdoc/>
    public ValueTask<McpResponse> HandleAsync(
        McpPeerContext peer,
        McpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(request);
        return _observation.ObserveRequestAsync(
            peer.ServerKey.Value,
            McpServerObservation.OperationOf(request),
            () => HandleCoreAsync(request, cancellationToken),
            McpServerObservation.Classify);
    }

    private async ValueTask<McpResponse> HandleCoreAsync(McpRequest request, CancellationToken cancellationToken)
    {
        if (request is not McpToolsCallRequest toolCall)
        {
            return new McpResponseUnsupportedCapability(request.Id, request.GetType().Name);
        }

        var executor = _services.GetService<IToolExecutor>();
        var captureFactory = _services.GetService<IToolRunCatalogCaptureFactory>();
        var sessionCoordinator = _services.GetService<ISessionCoordinator>();
        var runCoordinator = _services.GetService<ISessionRunCoordinator>();
        if (executor is null || captureFactory is null || sessionCoordinator is null || runCoordinator is null)
        {
            return new McpResponseDenied(
                toolCall.Id,
                "MCP tool dispatch requires IToolExecutor, IToolRunCatalogCaptureFactory, and session coordinators.");
        }

        if (toolCall.Operation.Correlation is not InRunOperationCorrelation correlation)
        {
            return new McpResponseDenied(toolCall.Id, "MCP tool dispatch requires an in-run operation correlation.");
        }

        var operation = toolCall.Operation;
        var sessionId = operation.Authorization.Scope.SessionId
            ?? throw new InvalidOperationException("MCP tool dispatch requires a session-bound authorization scope.");
        var capture = captureFactory.Create(new RunToolCatalogCaptureRequest(
            operation.AgentId,
            sessionId,
            correlation.RunId,
            operation.Authorization));
        var profile = new SessionProfileSnapshot(
            new SessionProfileReference(new SessionProfileKey("mcp-server"), new SessionProfileVersion(1)),
            new ComponentKey<ISessionCoordinator>("coordinator"),
            new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
            new SessionStoreKey("agentkit.in-memory"),
            SessionStoreCapabilities.None,
            requiresDurableStore: false,
            requiresDistributedFencing: false,
            new SessionRetentionProfileKey("default"),
            SessionBusyBehavior.Reject,
            maximumAppendEntries: 64,
            maximumPageSize: 64,
            verifySnapshotHashes: false,
            deleteOnDispose: false,
            new ContentHash("sha256:mcp-server-session"));

        // The executor commits an accepted-call record before invoking any tool, so dispatch needs the session branch
        // that receives it. The lane follows the loop's default for runs without an explicit lane admission. A session
        // that cannot be loaded fails closed rather than invoking an unrecorded effect.
        var laneId = new ExecutionLaneId(sessionId.Value);
        var sessionContext = new SessionOperationContext(
            operation.AgentId, sessionId, laneId, correlation, operation.Identity, operation.Authorization);
        var session = await sessionCoordinator.LoadAsync(sessionContext, profile, cancellationToken).ConfigureAwait(false);
        if (session is not SessionLoaded loaded)
        {
            return new McpResponseDenied(
                toolCall.Id,
                "MCP tool dispatch requires a loadable session to record the call before invoking it.");
        }

        var capability = new ToolExecutionCapability(
            new SessionExecutionCapability(profile, sessionCoordinator, runCoordinator),
            new BudgetExecutionCapability(
                new BudgetProfileKey("mcp-server"),
                new BudgetProfileVersion(1),
                operation.Identity,
                correlation,
                new McpServerBudgetScope()),
            new ToolCallSessionTarget(loaded.Descriptor.ActiveBranchId, laneId),
            [.. capture.Snapshot.ExecutionPolicies.Values
                .Distinct()
                .Select(static reference => new ToolExecutionPolicyBinding(reference))]);
        var rawArguments = toolCall.Arguments is null
            ? []
            : ImmutableArray.Create(Encoding.UTF8.GetBytes(toolCall.Arguments.RootElement.GetRawText()));
        var turnId = correlation.TurnId
            ?? throw new InvalidOperationException("MCP tool dispatch requires a turn identity.");
        var call = new ToolCallRequest(
            operation.AgentId,
            sessionId,
            correlation.RunId,
            turnId,
            correlation.OperationId,
            toolCall.ToolCallId ?? new ToolCallId(Guid.NewGuid()),
            operation.Authorization,
            capture.Snapshot.Version,
            sourceOrdinal: 0,
            new ToolAlias(toolCall.ToolName),
            rawArguments,
            DateTimeOffset.UtcNow);
        var batch = await executor.ExecuteAsync(capture, [call], capability, cancellationToken).ConfigureAwait(false);
        if (batch.Results.Length != 1)
        {
            return new McpResponseDenied(toolCall.Id, "Tool executor returned an unexpected batch shape.");
        }

        var result = batch.Results[0];
        if (result.Status is not ToolTerminalStatus.Succeeded)
        {
            return new McpResponseDenied(toolCall.Id, result.Error?.SafeMessage ?? "Tool execution failed.");
        }

        var text = result.Content.OfType<TextPart>().FirstOrDefault()?.Text ?? string.Empty;
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(new { result = text }));
        return new McpResponseSucceeded(toolCall.Id, payload);
    }

    private sealed class McpServerBudgetScope: IBudgetScope
    {
        public BudgetScopeId Id { get; } = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        public BudgetScopeAddress Address { get; } = new(
            new TenantId("mcp-server"),
            new PrincipalId("peer"),
            new AgentId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new SessionId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            new RunId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            new OperationId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")));

        public ValueTask<BudgetReservationResult> ReserveAsync(
            BudgetReservationRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<BudgetReservationResult>(
                new BudgetRejected(new BudgetLimitFailure(
                    Id,
                    request.Dimension,
                    BudgetLimitKind.Hard,
                    configuredValue: 0,
                    observedValue: new BudgetQuantity(System.Numerics.BigInteger.Zero, 0),
                    requestedAmount: new BudgetQuantity(System.Numerics.BigInteger.One, 0),
                    request.Unit,
                    "MCP server tool dispatch does not reserve budget dimensions.")));

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
            ImmutableArray<BudgetReservationRequest> requests,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
