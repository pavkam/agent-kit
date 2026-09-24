// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Requests one remote MCP prompt with arguments.</summary>
public sealed record McpPromptsGetRequest: McpRequest
{
    /// <summary>Initializes a prompt get request.</summary>
    /// <param name="id">The non-default MCP request identity.</param>
    /// <param name="operation">The bound protected operation context.</param>
    /// <param name="name">The protocol-facing prompt name.</param>
    /// <param name="arguments">Optional prompt arguments; null means none were supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is uninitialized.</exception>
    public McpPromptsGetRequest(
        McpRequestId id,
        ProtectedSemanticOperationContext operation,
        string name,
        IReadOnlyDictionary<string, string>? arguments = null)
        : base(id, operation, toolCallId: null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        Name = name;
        Arguments = arguments is null ? null : ImmutableDictionary.CreateRange(arguments);
    }

    /// <summary>Gets the protocol-facing prompt name.</summary>
    public string Name { get; }

    /// <summary>Gets optional prompt arguments.</summary>
    public ImmutableDictionary<string, string>? Arguments { get; }
}
