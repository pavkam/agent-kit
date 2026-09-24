// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Replaces the client's advertised filesystem roots.</summary>
public sealed record McpRootsSetRequest: McpRequest
{
    /// <summary>Initializes a roots update request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="roots">The complete root URI set to publish.</param>
    /// <exception cref="ArgumentException"><paramref name="roots"/> is default or contains an uninitialized URI.</exception>
    public McpRootsSetRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        ImmutableArray<string> roots)
        : base(id, operation, toolCallId: null)
    {
        ArgumentException.ThrowIfDefault(roots);
        foreach (var root in roots)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root, nameof(roots));
        }

        Roots = roots;
    }

    /// <summary>Gets the complete root URI set.</summary>
    public ImmutableArray<string> Roots { get; }
}
