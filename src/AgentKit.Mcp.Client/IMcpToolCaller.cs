// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Isolates reflected tool invocation from the official MCP SDK session.</summary>
internal interface IMcpToolCaller: IAsyncDisposable
{
    /// <summary>Gets the negotiated MCP wire revision.</summary>
    public McpProtocolVersion ProtocolVersion { get; }

    /// <summary>Lists the bounded tool identity metadata advertised by the server.</summary>
    /// <param name="cancellationToken">A token that cancels the listing request.</param>
    /// <returns>The advertised remote tools.</returns>
    public ValueTask<IReadOnlyList<McpRemoteTool>> ListToolsAsync(CancellationToken cancellationToken);

    /// <summary>Calls one reflected tool and returns its structured JSON result.</summary>
    /// <param name="method">The captured reflected tool method.</param>
    /// <param name="request">The request object to place under the reflected parameter name.</param>
    /// <param name="serializerOptions">The serializer options shared by request and response mapping.</param>
    /// <param name="cancellationToken">A token that cancels waiting for the remote call.</param>
    /// <returns>The structured JSON result.</returns>
    public ValueTask<JsonElement> CallAsync(
        McpToolMethodDescriptor method,
        object request,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken);
}
