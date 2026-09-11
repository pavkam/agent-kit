// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies a positive replacement revision within one usage entry.</summary>
/// <remarks>This version is independent of budget-reservation revisions and session ledger ordering. The first report is revision one; each replacement names its preceding revision.</remarks>
public readonly record struct UsageAccountingRevision
{
    /// <summary>Captures a positive accounting revision.</summary>
    /// <param name="value">The positive revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    [System.Text.Json.Serialization.JsonConstructor]
    public UsageAccountingRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the monotonically increasing revision of one contribution.</summary>
    /// <value>A positive number for constructed values; consumers reject the CLR default.</value>
    public long Value { get; }

    /// <summary>Formats the revision without culture-dependent separators.</summary>
    /// <returns>The invariant decimal representation.</returns>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
