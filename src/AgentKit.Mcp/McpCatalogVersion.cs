// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one immutable publication of an MCP tool catalog.</summary>
/// <remarks>
/// A catalog version is a local monotonic generation, not an MCP protocol
/// revision or a version of any individual tool contract.
/// </remarks>
public readonly record struct McpCatalogVersion
{
    /// <summary>Initializes a catalog generation.</summary>
    /// <param name="value">The positive generation number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than one.</exception>
    public McpCatalogVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Value = value;
    }

    /// <summary>Gets the positive generation number.</summary>
    public long Value { get; }

    /// <summary>Returns the invariant generation text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
