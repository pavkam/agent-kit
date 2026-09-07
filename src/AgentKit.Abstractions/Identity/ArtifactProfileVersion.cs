// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable artifact profile revision.</summary>
public readonly record struct ArtifactProfileVersion
{
    /// <summary>Initializes a positive revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ArtifactProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
    /// <summary>Gets the positive revision number.</summary>
    public long Value { get; }
    /// <summary>Returns the invariant revision text.</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
