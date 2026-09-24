// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Handles a server-initiated sampling request to the client host.</summary>
/// <remarks>
/// Sampling is a reverse capability. The payload remains typed JSON so adapters
/// can map it without an untyped escape hatch.
/// </remarks>
public sealed record McpSamplingCreateMessageRequest: McpRequest
{
    /// <summary>Initializes a sampling request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="payload">The protocol sampling payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    public McpSamplingCreateMessageRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        JsonDocument payload)
        : base(id, operation, toolCallId: null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Payload = payload;
    }

    /// <summary>Gets the protocol sampling payload.</summary>
    public JsonDocument Payload { get; }
}
