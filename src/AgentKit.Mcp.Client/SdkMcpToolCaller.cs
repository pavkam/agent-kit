// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Text.Json.Nodes;

using ModelContextProtocol;
using ModelContextProtocol.Client;

/// <summary>Adapts the official SDK client to AgentKit's reflected tool caller boundary.</summary>
internal sealed class SdkMcpToolCaller: IMcpToolCaller
{
    private readonly McpClient _client;

    /// <summary>Initializes an SDK-backed caller that owns the supplied client session.</summary>
    /// <param name="client">The connected official SDK client.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public SdkMcpToolCaller(McpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        ProtocolVersion = McpProtocolVersion.Parse(
            client.NegotiatedProtocolVersion ??
            throw new InvalidOperationException("The connected MCP client did not report a negotiated protocol version."));
    }

    /// <inheritdoc/>
    public McpProtocolVersion ProtocolVersion { get; }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<McpRemoteTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var tools = await _client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return tools.Select(static tool => new McpRemoteTool(
                new McpToolName(tool.ProtocolTool.Name),
                ReadVersion(tool.ProtocolTool.Meta)))
            .ToArray();
    }

    /// <inheritdoc/>
    public async ValueTask<JsonElement> CallAsync(
        McpToolMethodDescriptor method,
        object request,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        var arguments = new Dictionary<string, object?>
        {
            [method.RequestParameterName] = request
        };
        var options = new RequestOptions { JsonSerializerOptions = serializerOptions };
        var result = await _client.CallToolAsync(
            method.Name.Value,
            arguments,
            options: options,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.IsError is true
            ? throw new McpToolInvocationException(method.Name, $"Remote MCP tool '{method.Name}' reported a tool error.")
            : result.StructuredContent?.Clone() ??
            throw new McpToolInvocationException(
                method.Name,
                $"Remote MCP tool '{method.Name}' did not return structured content for response type '{method.ResponseType}'.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _client.DisposeAsync();

    private static ToolVersion? ReadVersion(JsonObject? metadata)
    {
        var node = metadata?[McpMetadataKeys.ToolContractVersion];
        return node is JsonValue value && value.TryGetValue<string>(out var version) && !string.IsNullOrWhiteSpace(version)
            ? new ToolVersion(version)
            : null;
    }
}
