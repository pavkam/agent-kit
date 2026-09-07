// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable budget-profile revision captured for an execution.</summary>
/// <remarks>A default instance represents no captured profile and must be rejected at an execution boundary.</remarks>
public readonly record struct BudgetProfileVersion
{
    /// <summary>Initializes a positive budget-profile revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public BudgetProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive immutable revision number.</summary>
    /// <value>Zero only for the CLR default representation.</value>
    public long Value { get; }

    /// <summary>Returns the invariant decimal profile revision.</summary>
    /// <returns>The invariant decimal representation of <see cref="Value"/>.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
