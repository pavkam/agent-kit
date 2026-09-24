// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Handles a server-initiated elicitation request to the client host.</summary>
public sealed record McpElicitationCreateRequest: McpRequest
{
    /// <summary>Initializes an elicitation request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="payload">The protocol elicitation payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    public McpElicitationCreateRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        JsonDocument payload)
        : base(id, operation, toolCallId: null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Payload = payload;
    }

    /// <summary>Gets the protocol elicitation payload.</summary>
    public JsonDocument Payload { get; }
}
