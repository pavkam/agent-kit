// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Threading.Channels;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>Adapts the official SDK client to AgentKit MCP session semantics.</summary>
internal sealed class SdkMcpSessionAdapter
{
    private readonly McpClient _client;
    private readonly McpSessionId _sessionId;
    private readonly ToolSourceId _toolSourceId;
    private readonly McpClientOptionsSnapshot _options;
    private readonly ILogger _logger;
    private readonly Channel<McpNotification> _notifications;
    private IAsyncDisposable? _toolListChangedRegistration;
    private McpCatalogVersion _catalogVersion = new(1);
    private McpCatalogSnapshot? _catalog;
    private int _disposed;

    internal SdkMcpSessionAdapter(
        McpClient client,
        McpSessionId sessionId,
        ToolSourceId toolSourceId,
        McpClientOptionsSnapshot options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _sessionId = sessionId;
        _toolSourceId = toolSourceId;
        _options = options;
        _logger = logger;
        _notifications = Channel.CreateUnbounded<McpNotification>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true,
        });
        RegisterNotificationHandlers();
    }

    internal McpProtocolVersion ProtocolVersion =>
        McpProtocolVersion.Parse(
            _client.NegotiatedProtocolVersion ??
            throw new InvalidOperationException("The MCP client did not report a negotiated protocol version."));

    internal async ValueTask<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var capabilities = McpToolCatalogMapper.ToCapabilitySet(_client.ServerCapabilities);
        var info = _client.ServerInfo;
        _ = await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
        return new McpInitializeResult(
            ProtocolVersion,
            capabilities,
            info?.Name,
            info?.Version);
    }

    internal ValueTask<McpCatalogSnapshot> GetCatalogAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        return _catalog is null
            ? RefreshCatalogAsync(cancellationToken)
            : ValueTask.FromResult(_catalog);
    }

    internal async ValueTask<McpResponse> InvokeAsync(McpToolsCallRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, object?>? arguments = null;
        if (request.Arguments is not null)
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(request.Arguments.RootElement)
                ?? [];
        }

        try
        {
            var result = await _client.CallToolAsync(
                request.ToolName,
                arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(result));
            return new McpResponseSucceeded(request.Id, document);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new McpResponseCancelled(request.Id, SideEffectCertainty.DefinitelyNotPerformed);
        }
        catch (Exception exception)
        {
            return new McpResponseProtocolFailed(request.Id, exception.GetType().Name);
        }
    }

    internal async ValueTask<McpResponse> InvokeAsync(McpRequest request, CancellationToken cancellationToken) =>
        request switch
        {
            McpToolsCallRequest toolCall => await InvokeAsync(toolCall, cancellationToken).ConfigureAwait(false),
            _ => new McpResponseUnsupportedCapability(request.Id, request.GetType().Name),
        };

    internal IAsyncEnumerable<McpNotification> ReadNotificationsAsync(CancellationToken cancellationToken) =>
        _notifications.Reader.ReadAllAsync(cancellationToken);

    internal async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = _notifications.Writer.TryComplete();
        if (_toolListChangedRegistration is not null)
        {
            await _toolListChangedRegistration.DisposeAsync().ConfigureAwait(false);
            _toolListChangedRegistration = null;
        }

        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private void RegisterNotificationHandlers()
    {
        _toolListChangedRegistration = _client.RegisterNotificationHandler(
            NotificationMethods.ToolListChangedNotification,
            HandleToolListChangedNotificationAsync);
    }

    private async ValueTask HandleToolListChangedNotificationAsync(
        JsonRpcNotification notification,
        CancellationToken cancellationToken)
    {
        _ = notification;
        try
        {
            _ = await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
            await _notifications.Writer.WriteAsync(
                new McpToolsListChangedNotification(_sessionId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            McpClientLog.CatalogRefreshFailed(_logger, ProtocolVersion, exception.GetType().Name);
            if (_options.UnknownNotificationPolicy == McpUnknownNotificationPolicy.FailSession)
            {
                _ = _notifications.Writer.TryComplete(exception);
            }
        }
    }

    private async ValueTask<McpCatalogSnapshot> RefreshCatalogAsync(CancellationToken cancellationToken)
    {
        var tools = await _client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var toolDescriptors = tools
            .Select(tool => McpToolCatalogMapper.ToDescriptor(
                tool.ProtocolTool,
                _toolSourceId,
                new McpToolName(tool.ProtocolTool.Name)))
            .ToImmutableArray();
        ImmutableArray<McpResourceDescriptor> resources = [];
        ImmutableArray<McpPromptDescriptor> prompts = [];
        if (_client.ServerCapabilities?.Resources is not null)
        {
            var listed = await _client.ListResourcesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            resources = [.. listed.Select(static resource => McpToolCatalogMapper.ToResource(resource.ProtocolResource))];
        }

        if (_client.ServerCapabilities?.Prompts is not null)
        {
            var listed = await _client.ListPromptsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            prompts = [.. listed.Select(static prompt => McpToolCatalogMapper.ToPrompt(prompt.ProtocolPrompt))];
        }

        _catalogVersion = new McpCatalogVersion(_catalogVersion.Value + 1);
        _catalog = new McpCatalogSnapshot(
            _sessionId,
            _catalogVersion,
            toolDescriptors,
            resources,
            prompts,
            McpToolCatalogMapper.ToCapabilitySet(_client.ServerCapabilities));
        McpClientLog.CatalogPublished(_logger, ProtocolVersion, _catalogVersion, toolDescriptors.Length);
        return _catalog;
    }
}
