// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Requests one page of remote MCP prompts.</summary>
public sealed record McpPromptsListRequest: McpRequest
{
    /// <summary>Initializes a prompts list request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="cursor">Optional pagination cursor from a prior list response.</param>
    public McpPromptsListRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        string? cursor = null)
        : base(id, operation, toolCallId: null) =>
        Cursor = cursor;

    /// <summary>Gets the optional pagination cursor.</summary>
    public string? Cursor { get; }
}
