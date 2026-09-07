// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one monotonically published security-policy version.</summary>
public readonly record struct SecurityPolicyVersion
{
    /// <summary>Initializes a validated policy version.</summary>
    /// <param name="value">The positive version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public SecurityPolicyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the version number.</summary>
    public long Value { get; }

    /// <summary>Returns the invariant-culture version text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
