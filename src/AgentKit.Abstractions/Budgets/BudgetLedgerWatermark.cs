// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one monotonic ledger revision that bounds a recovery scan.</summary>
/// <remarks>The value is opaque to consumers. The ledger assigns it once, then excludes reservations which start after that revision.</remarks>
public readonly record struct BudgetLedgerWatermark
{
    /// <summary>Initializes a positive ledger revision.</summary>
    /// <param name="value">The positive revision assigned by the ledger.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public BudgetLedgerWatermark(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
    /// <summary>Gets the opaque revision value.</summary>
    /// <value>A positive value meaningful only to the owning ledger.</value>
    public long Value { get; }

    /// <summary>Returns the invariant-culture text form suitable for diagnostic correlation.</summary>
    /// <returns>The positive revision in invariant-culture decimal form.</returns>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
