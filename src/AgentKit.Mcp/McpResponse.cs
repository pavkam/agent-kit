// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>The immutable base for one terminal MCP request outcome.</summary>
/// <remarks>
/// This is a closed discriminated hierarchy covering success, protocol failure,
/// denial, unsupported capability, cancellation, and unknown side-effect
/// certainty. Its constructor is <see langword="private protected"/> so only
/// this assembly can extend it.
/// </remarks>
public abstract record McpResponse
{
    /// <summary>Initializes a terminal MCP response.</summary>
    /// <param name="requestId">The MCP request identity this response completes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestId"/> is default.</exception>
    private protected McpResponse(McpRequestId requestId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        RequestId = requestId;
    }

    /// <summary>Gets the MCP request identity this response completes.</summary>
    public McpRequestId RequestId { get; }
}
