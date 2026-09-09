// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive portable compatibility-profile publication revision.</summary>
/// <remarks>The revision is retained immutable evidence; it does not prove availability or activate a wire profile.</remarks>
public readonly record struct CompatibilityProfileVersion
{
    /// <summary>Initializes a positive profile publication revision.</summary>
    /// <param name="value">The positive exact revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public CompatibilityProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the exact profile publication revision.</summary>
    /// <value>A positive number, or zero only for the CLR default value.</value>
    public long Value { get; }

    /// <summary>Formats the revision using invariant decimal digits.</summary>
    /// <returns>The invariant revision text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
