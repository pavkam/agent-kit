// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one configured MCP server registration.</summary>
/// <remarks>
/// Server keys come from configuration and select a server composition. They
/// are not session identities and do not grant authority to expose a primitive.
/// </remarks>
public readonly record struct McpServerKey
{
    /// <summary>Initializes a non-empty server key.</summary>
    /// <param name="value">The non-empty configuration key.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public McpServerKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the configuration key.</summary>
    public string Value { get; }

    /// <summary>Returns the configuration key.</summary>
    public override string ToString() => Value;
}
