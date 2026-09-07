// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one MCP tool by its protocol-facing name.</summary>
public readonly record struct McpToolName
{
    /// <summary>Initializes a validated tool name.</summary>
    /// <param name="value">The non-empty protocol-facing name.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public McpToolName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the protocol-facing name.</summary>
    public string Value { get; }

    /// <summary>Returns the protocol-facing name.</summary>
    public override string ToString() => Value;
}
