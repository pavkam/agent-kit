// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies exact sortable indexing for decimal maximum projections.</summary>
public sealed class SqliteMaximumKeyTests
{
    /// <summary>Proves fixed keys preserve numeric ordering across scale and decimal boundaries.</summary>
    [Fact]
    public void MaximumKey_WhenValuesDiffer_PreservesNumericOrdering()
    {
        decimal[] values = [0m, 0.0000000000000000000000000001m, 0.1m, 1m, 1.00m, decimal.MaxValue];

        var ordered = values.Select(value => (Value: value, Key: SqliteBudgetLedger.MaximumKey(value)))
            .OrderBy(static item => item.Key, ByteArrayComparer.Instance).Select(static item => item.Value).ToArray();

        ordered.ShouldBe(values);
        SqliteBudgetLedger.MaximumKey(1m).ShouldBe(SqliteBudgetLedger.MaximumKey(1.00m));
    }

    private sealed class ByteArrayComparer: IComparer<byte[]>
    {
        internal static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right) => left.AsSpan().SequenceCompareTo(right);
    }
}
