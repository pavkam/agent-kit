// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Requests execution of one remote MCP tool.</summary>
public sealed record McpToolsCallRequest: McpRequest
{
    /// <summary>Initializes a tool call request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="toolCallId">The AgentKit tool-call correlation for this effect.</param>
    /// <param name="toolName">The protocol-facing tool name to invoke.</param>
    /// <param name="arguments">Optional JSON arguments; null means an empty object on the wire.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName"/> is uninitialized.</exception>
    public McpToolsCallRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        ToolCallId toolCallId,
        string toolName,
        JsonDocument? arguments = null)
        : base(id, operation, toolCallId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName, nameof(toolName));
        ToolName = toolName;
        Arguments = arguments;
    }

    /// <summary>Gets the protocol-facing tool name.</summary>
    public string ToolName { get; }

    /// <summary>Gets optional JSON arguments.</summary>
    public JsonDocument? Arguments { get; }
}
