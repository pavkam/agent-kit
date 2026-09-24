// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A request was denied before or without sending a protocol frame.</summary>
public sealed record McpResponseDenied: McpResponse
{
    /// <summary>Initializes a denial response.</summary>
    /// <param name="requestId">The completed MCP request identity.</param>
    /// <param name="safeMessage">A non-sensitive denial summary suitable for logs and callers.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is uninitialized.</exception>
    public McpResponseDenied(McpRequestId requestId, string safeMessage)
        : base(requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive denial summary.</summary>
    public string SafeMessage { get; }
}
