// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Requests the contents of one remote MCP resource.</summary>
public sealed record McpResourcesReadRequest: McpRequest
{
    /// <summary>Initializes a resource read request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="uri">The resource URI to read.</param>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is uninitialized.</exception>
    public McpResourcesReadRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        string uri)
        : base(id, operation, toolCallId: null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri, nameof(uri));
        Uri = uri;
    }

    /// <summary>Gets the resource URI.</summary>
    public string Uri { get; }
}
