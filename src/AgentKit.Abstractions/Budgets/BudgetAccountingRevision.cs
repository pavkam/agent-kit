// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive ledger-assigned replacement of a reservation's accounting truth.</summary>
public readonly record struct BudgetAccountingRevision
{
    /// <summary>Creates a revision.</summary><param name="value">The positive monotonic value.</param><exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public BudgetAccountingRevision(long value) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value); Value = value; }
    /// <summary>Gets the positive value.</summary><value>A ledger-assigned monotonic value.</value>
    public long Value { get; }
    /// <summary>Formats the invariant numeric value.</summary><returns>The invariant decimal representation.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
