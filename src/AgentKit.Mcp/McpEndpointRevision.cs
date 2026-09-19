// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one immutable publication of an MCP endpoint.</summary>
/// <remarks>
/// Opening a session captures this revision and never silently substitutes a
/// newer registration. It is a local configuration generation, not an MCP
/// protocol version.
/// </remarks>
public readonly record struct McpEndpointRevision
{
    /// <summary>Initializes a positive endpoint generation.</summary>
    /// <param name="value">The positive generation number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than one.</exception>
    public McpEndpointRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Value = value;
    }

    /// <summary>Gets the positive generation number.</summary>
    public long Value { get; }

    /// <summary>Returns the invariant generation text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
