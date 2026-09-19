// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies one immutable publication of an MCP capability profile.</summary>
/// <remarks>
/// A run captures this revision together with the selected endpoint revision.
/// It is not an <see cref="McpCatalogVersion"/> and does not change when a
/// remote server publishes a new tool list.
/// </remarks>
public readonly record struct McpCapabilityProfileRevision
{
    /// <summary>Initializes a positive profile generation.</summary>
    /// <param name="value">The positive generation number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than one.</exception>
    public McpCapabilityProfileRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Value = value;
    }

    /// <summary>Gets the positive generation number.</summary>
    public long Value { get; }

    /// <summary>Returns the invariant generation text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
