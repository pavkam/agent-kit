// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A request completed with a protocol result payload.</summary>
public sealed record McpResponseSucceeded: McpResponse
{
    /// <summary>Initializes a successful MCP response.</summary>
    /// <param name="requestId">The completed MCP request identity.</param>
    /// <param name="result">The protocol result payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public McpResponseSucceeded(McpRequestId requestId, JsonDocument result)
        : base(requestId)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>Gets the protocol result payload.</summary>
    public JsonDocument Result { get; }
}
