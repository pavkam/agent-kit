// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one MCP request inside a session independently of the JSON-RPC id.</summary>
/// <remarks>
/// JSON-RPC ids remain external correlation values. This identity is the
/// AgentKit request identity used for grants, audit, and cancellation, and it
/// never replaces <see cref="OperationId"/> or <see cref="ToolCallId"/>.
/// </remarks>
public readonly record struct McpRequestId
{
    /// <summary>Initializes a non-empty request identity.</summary>
    /// <param name="value">The stable non-empty identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public McpRequestId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the stable identity value.</summary>
    public Guid Value { get; }

    /// <summary>Returns canonical identity text.</summary>
    public override string ToString() => Value.ToString("D");
}
