// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies the deterministic algorithm used to normalize one terminal result.</summary>
public readonly record struct ToolResultNormalizationAlgorithmVersion
{
    /// <summary>Initializes a positive algorithm revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ToolResultNormalizationAlgorithmVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
    /// <summary>Gets the positive revision number.</summary>
    /// <value>A positive value, or zero only for default.</value>
    public long Value { get; }
    /// <summary>Formats the revision invariantly.</summary>
    /// <returns>The invariant decimal revision.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
