// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

using System.Globalization;

/// <summary>Identifies one date-versioned revision of the Model Context Protocol.</summary>
/// <remarks>
/// This identity is deliberately distinct from an AgentKit package version, a
/// tool contract version, and a catalog generation. Ordering compares the
/// underlying protocol revision date.
/// </remarks>
public readonly record struct McpProtocolVersion: IComparable<McpProtocolVersion>
{
    /// <summary>Initializes a validated protocol revision.</summary>
    /// <param name="value">The non-default date published for the protocol revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is the default date.</exception>
    public McpProtocolVersion(DateOnly value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, default);
        Value = value;
    }

    /// <summary>Gets the date that identifies this protocol revision.</summary>
    public DateOnly Value { get; }

    /// <summary>Gets the lifecycle family selected by this protocol revision.</summary>
    public McpProtocolEra Era => Value >= McpProtocolVersions.July2026.Value
        ? McpProtocolEra.Modern
        : McpProtocolEra.Legacy;

    /// <summary>Parses an MCP revision in its canonical <c>yyyy-MM-dd</c> representation.</summary>
    /// <param name="value">The canonical revision text.</param>
    /// <returns>The parsed protocol revision.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="value"/> is not a canonical date-versioned revision.</exception>
    public static McpProtocolVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var date = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
        return new McpProtocolVersion(date);
    }

    /// <summary>Compares this revision with another revision by publication date.</summary>
    /// <param name="other">The protocol revision to compare with this revision.</param>
    /// <returns>
    /// A negative value when this revision predates <paramref name="other"/>, zero when they are equal,
    /// or a positive value when this revision follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(McpProtocolVersion other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether one protocol revision predates another.</summary>
    /// <param name="left">The revision on the left side of the comparison.</param>
    /// <param name="right">The revision on the right side of the comparison.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> predates <paramref name="right"/>.</returns>
    public static bool operator <(McpProtocolVersion left, McpProtocolVersion right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether one protocol revision does not follow another.</summary>
    /// <param name="left">The revision on the left side of the comparison.</param>
    /// <param name="right">The revision on the right side of the comparison.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> predates or equals <paramref name="right"/>.</returns>
    public static bool operator <=(McpProtocolVersion left, McpProtocolVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether one protocol revision follows another.</summary>
    /// <param name="left">The revision on the left side of the comparison.</param>
    /// <param name="right">The revision on the right side of the comparison.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> follows <paramref name="right"/>.</returns>
    public static bool operator >(McpProtocolVersion left, McpProtocolVersion right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether one protocol revision does not predate another.</summary>
    /// <param name="left">The revision on the left side of the comparison.</param>
    /// <param name="right">The revision on the right side of the comparison.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> follows or equals <paramref name="right"/>.</returns>
    public static bool operator >=(McpProtocolVersion left, McpProtocolVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Returns the canonical wire representation of this revision.</summary>
    public override string ToString() => Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
