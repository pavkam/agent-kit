// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Requests the client's currently advertised filesystem roots.</summary>
public sealed record McpRootsListRequest: McpRequest
{
    /// <summary>Initializes a roots list request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    public McpRootsListRequest(McpRequestId id, ProtectedSemanticOperationContext operation)
        : base(id, operation, toolCallId: null)
    {
    }
}
