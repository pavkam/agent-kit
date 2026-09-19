// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one configured MCP endpoint.</summary>
/// <remarks>
/// Endpoint keys come from configuration. They are not generated session or
/// request identities, and agent definitions never carry them directly.
/// </remarks>
public readonly record struct McpEndpointKey
{
    /// <summary>Initializes a non-empty endpoint key.</summary>
    /// <param name="value">The non-empty configuration key.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public McpEndpointKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the configuration key.</summary>
    public string Value { get; }

    /// <summary>Returns the configuration key.</summary>
    public override string ToString() => Value;
}
