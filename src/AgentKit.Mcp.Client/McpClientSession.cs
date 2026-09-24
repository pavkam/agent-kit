// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using ModelContextProtocol.Client;

/// <summary>Owns one MCP client connection, catalog snapshots, and correlated invocations.</summary>
internal sealed class McpClientSession: IMcpClientSession
{
    private readonly SdkMcpSessionAdapter _adapter;
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _inFlight;
    private readonly McpClientOpenRequest _openRequest;
    private int _disposed;

    private McpClientSession(
        McpSessionId id,
        SdkMcpSessionAdapter adapter,
        McpClientOpenRequest openRequest,
        ISecurityAuthoritySelector authoritySelector,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        McpClientOptionsSnapshot options)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        Id = id;
        _adapter = adapter;
        _openRequest = openRequest;
        _authoritySelector = authoritySelector;
        _grantStore = grantStore;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _inFlight = new SemaphoreSlim(options.MaximumInFlightRequests, options.MaximumInFlightRequests);
    }

    /// <inheritdoc/>
    public McpSessionId Id { get; }

    /// <inheritdoc/>
    public McpSessionState State { get; private set; } = McpSessionState.Created;

    internal static async ValueTask<IMcpClientSession> OpenAsync(
        McpClientOpenRequest request,
        IMcpTransportFactoryCatalog transports,
        ISecurityAuthoritySelector authoritySelector,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher audit,
        IIdentifierGenerator<McpSessionId> sessionIds,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        McpClientOptionsSnapshot options,
        ToolSourceId toolSourceId,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(transports);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(sessionIds);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _ = await McpClientSecurityOperations.AuthorizeConnectAsync(
            request.Operation,
            request.Endpoint,
            authoritySelector,
            grantStore,
            audit,
            securityRequestIds,
            auditRecordIds,
            timeProvider,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("MCP session connect was denied by the security authority.");

        var resolution = await transports.ResolveAsync(request.Endpoint.Transport, cancellationToken).ConfigureAwait(false);
        if (resolution is not McpTransportFactoryResolved resolved)
        {
            throw new InvalidOperationException("No MCP transport factory is registered for the endpoint transport profile.");
        }

        var transportOpen = await resolved.Factory.OpenAsync(
            new McpTransportOpenRequest(request, request.Endpoint),
            cancellationToken).ConfigureAwait(false);
        if (transportOpen is not McpTransportOpened opened)
        {
            throw new InvalidOperationException("MCP transport failed to open.");
        }

        if (opened.Transport is not SdkMcpClientTransport sdkTransport)
        {
            await opened.Transport.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("The opened MCP transport is not an SDK client transport.");
        }

        var sessionId = sessionIds.Create();
        var logger = loggerFactory.CreateLogger<McpClientSession>();
        var sdkClient = await McpClient.CreateAsync(
            sdkTransport.ClientTransport,
            new ModelContextProtocol.Client.McpClientOptions { ProtocolVersion = null },
            loggerFactory,
            cancellationToken).ConfigureAwait(false);
        var adapter = new SdkMcpSessionAdapter(
            sdkClient,
            sessionId,
            toolSourceId,
            options,
            logger);
        return new McpClientSession(
            sessionId,
            adapter,
            request,
            authoritySelector,
            grantStore,
            securityRequestIds,
            timeProvider,
            options);
    }

    /// <inheritdoc/>
    public async ValueTask<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        State = McpSessionState.Initializing;
        var result = await _adapter.InitializeAsync(cancellationToken).ConfigureAwait(false);
        State = McpSessionState.Ready;
        return result;
    }

    /// <inheritdoc/>
    public ValueTask<McpCatalogSnapshot> GetCatalogAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        EnsureReady();
        return _adapter.GetCatalogAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<McpResponse> InvokeAsync(AuthorizedMcpRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(request);
        EnsureReady();
        if (request.Request.Operation.AgentId != _openRequest.Operation.AgentId
            || request.Request.Operation.Authorization != _openRequest.Operation.Authorization)
        {
            return new McpResponseDenied(request.Request.Id, "Operation binding mismatch.");
        }

        await _inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var grant = await McpClientSecurityOperations.AuthorizeMcpRequestAsync(
                request.Request,
                _authoritySelector,
                _grantStore,
                _requestIds,
                _timeProvider,
                cancellationToken).ConfigureAwait(false);
            if (grant is null)
            {
                return new McpResponseDenied(request.Request.Id, "MCP request denied.");
            }

            var consumption = await McpClientSecurityOperations.ConsumeGrantAsync(
                grant,
                _grantStore,
                cancellationToken).ConfigureAwait(false);
            return consumption.Status != GrantConsumptionStatus.Consumed
                ? new McpResponseDenied(request.Request.Id, "MCP grant consumption failed.")
                : await _adapter.InvokeAsync(request.Request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _inFlight.Release();
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<McpNotification> ReadNotificationsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        EnsureReady();
        return _adapter.ReadNotificationsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        State = McpSessionState.Disposed;
        _inFlight.Dispose();
        await _adapter.DisposeAsync().ConfigureAwait(false);
    }

    private void EnsureReady()
    {
        if (State is not McpSessionState.Ready)
        {
            throw new InvalidOperationException($"MCP session is not ready. Current state: {State}.");
        }
    }
}
