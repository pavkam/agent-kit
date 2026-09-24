// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A request was rejected because the negotiated capability does not support it.</summary>
public sealed record McpResponseUnsupportedCapability: McpResponse
{
    /// <summary>Initializes an unsupported-capability response.</summary>
    /// <param name="requestId">The completed MCP request identity.</param>
    /// <param name="safeMessage">A non-sensitive summary suitable for logs and callers.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is uninitialized.</exception>
    public McpResponseUnsupportedCapability(McpRequestId requestId, string safeMessage)
        : base(requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage, nameof(safeMessage));
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive summary.</summary>
    public string SafeMessage { get; }
}
