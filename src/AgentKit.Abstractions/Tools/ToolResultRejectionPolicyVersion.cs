// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable rejection-policy revision.</summary>
public readonly record struct ToolResultRejectionPolicyVersion
{
    /// <summary>Creates an exact positive revision without inferring freshness or ordering policy.</summary>
    /// <param name="value">The positive published revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ToolResultRejectionPolicyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
    /// <summary>Gets the positive revision.</summary><value>A positive value, or zero only for default.</value>
    public long Value { get; }
    /// <summary>Formats the revision invariantly.</summary><returns>Invariant decimal text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
