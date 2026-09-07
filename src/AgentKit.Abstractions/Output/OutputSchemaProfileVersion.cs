// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies a positive revision of an output-schema engine profile.</summary>
public readonly record struct OutputSchemaProfileVersion
{
    /// <summary>Initializes a profile revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public OutputSchemaProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive profile revision number.</summary>
    public long Value { get; }

    /// <summary>Returns the profile revision using invariant culture.</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
