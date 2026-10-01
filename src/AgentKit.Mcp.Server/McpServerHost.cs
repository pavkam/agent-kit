// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>Hosts one MCP server endpoint using the official SDK transport adapters.</summary>
public sealed class McpServerHost(IServiceProvider services): IMcpServer
{
    private readonly IServiceProvider _services = services;

    /// <inheritdoc/>
    public Task RunAsync(McpServerEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Transport is not McpStdioTransportProfile)
        {
            throw new NotSupportedException("Only stdio MCP server transports are supported by McpServerHost today.");
        }

        var loggerFactory = _services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var handlers = _services.GetServices<IMcpPrimitiveHandler>().ToArray();
        var observation = new McpServerObservation(loggerFactory.CreateLogger<McpServerObservation>());
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = endpoint.Key.Value, Version = "1.0" },
            Handlers = new McpServerHandlers
            {
                CallToolHandler = (context, ct) => DispatchToolCallAsync(endpoint.Key, handlers, observation, context, ct),
            },
        };
        var transport = new StreamServerTransport(Console.OpenStandardInput(), Console.OpenStandardOutput());
        var server = McpServer.Create(transport, options, loggerFactory);
        return server.RunAsync(cancellationToken);
    }

    private static ValueTask<CallToolResult> DispatchToolCallAsync(
        McpServerKey serverKey,
        IMcpPrimitiveHandler[] handlers,
        McpServerObservation observation,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return observation.ObserveToolCallAsync(
            serverKey.Value,
            context.Params?.Name ?? string.Empty,
            () => DispatchCoreAsync(serverKey, handlers, context, cancellationToken),
            McpServerObservation.ClassifyToolResult);
    }

    private static async ValueTask<CallToolResult> DispatchCoreAsync(
        McpServerKey serverKey,
        IMcpPrimitiveHandler[] handlers,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        if (handlers.Length == 0)
        {
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "No MCP primitive handlers are registered." }] };
        }

        var toolName = context.Params?.Name ?? string.Empty;
        JsonDocument? args = null;
        if (context.Params?.Arguments is { Count: > 0 } argumentDictionary)
        {
            args = JsonDocument.Parse(JsonSerializer.Serialize(argumentDictionary));
        }
        var operation = McpServerOperationFactory.CreateDefault();
        var request = new McpToolsCallRequest(
            new McpRequestId(Guid.NewGuid()),
            operation,
            new ToolCallId(Guid.NewGuid()),
            toolName,
            args);
        var peer = new McpPeerContext(serverKey);
        foreach (var handler in handlers)
        {
            var response = await handler.HandleAsync(peer, request, cancellationToken).ConfigureAwait(false);
            if (response is McpResponseSucceeded succeeded)
            {
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = succeeded.Result.RootElement.GetRawText() }],
                };
            }

            if (response is McpResponseDenied denied)
            {
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = denied.SafeMessage }] };
            }
        }

        return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "Tool dispatch failed." }] };
    }

}
