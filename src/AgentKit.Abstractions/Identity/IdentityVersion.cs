// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies the version of trusted issuer mapping and normalization used for an identity.</summary>
public readonly record struct IdentityVersion
{
    /// <summary>Initializes an identity-policy version.</summary>
    /// <param name="value">The positive version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public IdentityVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        Value = value;
    }

    /// <summary>Gets the version number.</summary>
    public long Value { get; }

    /// <summary>Returns the invariant version number.</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
