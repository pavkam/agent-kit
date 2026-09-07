// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the revocation epoch against which a grant was issued.</summary>
public readonly record struct SecurityRevocationVersion
{
    /// <summary>Initializes a validated revocation version.</summary>
    /// <param name="value">The positive epoch number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public SecurityRevocationVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the epoch number.</summary>
    public long Value { get; }

    /// <summary>Returns the invariant-culture epoch text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
