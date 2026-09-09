// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;

using System.Globalization;

/// <summary>Identifies a positive immutable toolset publication revision.</summary>
/// <remarks>The number identifies exact published content; comparison alone does not authorize a selection or imply availability.</remarks>
public readonly record struct ToolsetVersion
{
    /// <summary>Creates an exact positive publication revision.</summary>
    /// <param name="value">The positive revision retained with published membership.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ToolsetVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
    /// <summary>Gets the exact publication revision.</summary>
    /// <value>A positive number, or zero only on the CLR default value.</value>
    public long Value { get; }

    /// <summary>Formats the revision independently of the current culture.</summary>
    /// <returns>Invariant decimal revision text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
