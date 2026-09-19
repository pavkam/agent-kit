// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one MCP client connection independently of its catalog generation.</summary>
/// <remarks>
/// A session identity is minted when a connection opens and stays stable until
/// that connection is disposed. It is not an MCP JSON-RPC id, an
/// <see cref="OperationId"/>, or a <see cref="ToolCallId"/>.
/// </remarks>
public readonly record struct McpSessionId
{
    /// <summary>Initializes a non-empty session identity.</summary>
    /// <param name="value">The stable non-empty identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public McpSessionId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the stable identity value.</summary>
    public Guid Value { get; }

    /// <summary>Returns canonical identity text.</summary>
    public override string ToString() => Value.ToString("D");
}
