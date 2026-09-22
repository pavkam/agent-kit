// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A monotonic version stamp for one registered network profile's immutable
/// options snapshot.
/// </summary>
public readonly record struct NetworkProfileVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkProfileVersion"/>
    /// struct.
    /// </summary>
    /// <param name="value">The non-negative version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public NetworkProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the version number.</summary>
    public long Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
