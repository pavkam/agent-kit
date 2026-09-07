// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable effective-configuration revision.</summary>
/// <remarks>This shared composition identity lets captured evidence name the configuration it observed. It conveys neither freshness nor authority; consumers must revalidate it at their boundary.</remarks>
public readonly record struct ConfigurationVersion
{
    /// <summary>Initializes an effective-configuration revision, including a captured run snapshot when applicable.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ConfigurationVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive revision number.</summary>
    /// <value>The revision number, or zero for a default instance.</value>
    public long Value { get; }

    /// <summary>Returns the invariant-culture revision text.</summary>
    /// <returns>The numeric revision formatted with invariant culture.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
