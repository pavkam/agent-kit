// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A request failed because the remote server returned a protocol error.</summary>
public sealed record McpResponseProtocolFailed: McpResponse
{
    /// <summary>Initializes a protocol failure response.</summary>
    /// <param name="requestId">The completed MCP request identity.</param>
    /// <param name="safeMessage">A non-sensitive failure summary suitable for logs and callers.</param>
    /// <param name="errorCode">The optional JSON-RPC or MCP error code when one was supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is uninitialized.</exception>
    public McpResponseProtocolFailed(McpRequestId requestId, string safeMessage, int? errorCode = null)
        : base(requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        SafeMessage = safeMessage;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the non-sensitive failure summary.</summary>
    public string SafeMessage { get; }

    /// <summary>Gets the optional JSON-RPC or MCP error code.</summary>
    public int? ErrorCode { get; }
}
