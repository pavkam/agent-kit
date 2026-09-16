// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies SqliteBudgetLedgerInstanceId behavior and contracts.</summary>
public sealed class SqliteBudgetLedgerInstanceIdTests
{
    /// <summary>Verifies an empty GUID is rejected.</summary>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteBudgetLedgerInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    /// <summary>Verifies the canonical diagnostic text is the lowercase hyphenated GUID.</summary>
    [Fact]
    public void ToString_WhenCalled_ReturnsCanonicalLowercaseText()
    {
        var guid = Guid.NewGuid();
        var id = new SqliteBudgetLedgerInstanceId(guid);
        id.ToString().ShouldBe(guid.ToString("D"));
    }
}
