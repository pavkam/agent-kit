// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>The immutable base for one typed MCP client or server request.</summary>
/// <remarks>
/// This is a closed discriminated hierarchy declared in this assembly. Requests
/// carry the immutable operation context captured when the connection opened and
/// optional <see cref="ToolCallId"/> correlation for tool effects. They are not
/// an event-name plus untyped payload escape hatch.
/// </remarks>
public abstract record McpRequest
{
    /// <summary>Initializes one correlated MCP request.</summary>
    /// <param name="id">The non-default MCP request identity for this session.</param>
    /// <param name="operation">The protected operation context bound to this connection.</param>
    /// <param name="toolCallId">Optional AgentKit tool-call correlation for effectful tool requests.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    private protected McpRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        ToolCallId? toolCallId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentNullException.ThrowIfNull(operation);
        Id = id;
        Operation = operation;
        ToolCallId = toolCallId;
    }

    /// <summary>Gets the MCP request identity unique within the session.</summary>
    public McpRequestId Id { get; }

    /// <summary>Gets the immutable operation context captured for this connection.</summary>
    public ProtectedSemanticOperationContext Operation { get; }

    /// <summary>Gets optional AgentKit tool-call correlation.</summary>
    public ToolCallId? ToolCallId { get; }
}
