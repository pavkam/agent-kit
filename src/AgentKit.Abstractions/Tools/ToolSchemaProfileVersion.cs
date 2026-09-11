// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Pins the positive revision of a tool-schema profile's validation and resource-accounting semantics.</summary>
public readonly record struct ToolSchemaProfileVersion
{
    /// <summary>Creates a positive profile revision without reference to an assembly version.</summary>
    /// <param name="value">The positive authored revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ToolSchemaProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
    /// <summary>Gets the exact authored profile revision.</summary>
    /// <value>A positive integer; the default value is invalid at consuming boundaries.</value>
    public long Value { get; }
    /// <summary>Returns the exact positive revision using invariant decimal formatting.</summary>
    /// <returns>The retained revision as culture-independent text.</returns>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
