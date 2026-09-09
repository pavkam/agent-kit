// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable endpoint-profile publication revision.</summary>
/// <remarks>This revision is captured evidence only; it neither resolves a profile nor proves that it remains available.</remarks>
public readonly record struct ProviderEndpointProfileVersion
{
    /// <summary>Initializes a positive published revision.</summary>
    /// <param name="value">The positive exact revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ProviderEndpointProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the exact published revision.</summary>
    /// <value>A positive number, or zero only for the CLR default value.</value>
    public long Value { get; }

    /// <summary>Formats the revision with invariant-culture decimal digits.</summary>
    /// <returns>The invariant revision text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
