// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable security-profile revision.</summary>
/// <remarks>The default value is not a published revision and must be rejected by consumers.</remarks>
public readonly record struct SecurityProfileVersion
{
    /// <summary>Initializes a security-profile revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public SecurityProfileVersion(long value)
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
