// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The positive version of one canonicalization profile.</summary>
/// <remarks>
/// This type is an immutable value object. A default instance can exist in memory but is never a valid version.
/// </remarks>
public readonly record struct CanonicalizationProfileVersion
{
    /// <summary>Initializes a validated <see cref="CanonicalizationProfileVersion"/> value.</summary>
    /// <param name="value">The positive version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public CanonicalizationProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive version number.</summary>
    public long Value { get; }

    /// <summary>Returns the version number.</summary>
    /// <returns>The invariant-culture version text.</returns>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
