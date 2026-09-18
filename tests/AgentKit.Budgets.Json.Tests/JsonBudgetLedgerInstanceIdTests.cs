// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the identity that pins one initialized JSON budget-ledger root to its expected deployment.</summary>
public sealed class JsonBudgetLedgerInstanceIdTests
{
    /// <summary>Verifies an empty GUID is refused, because a store must never bind to an unnamed deployment.</summary>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonBudgetLedgerInstanceId(Guid.Empty))
            .ParamName.ShouldBe("value");

    /// <summary>Verifies a nondefault GUID is captured exactly.</summary>
    [Fact]
    public void Constructor_WhenValueIsNondefault_ExposesExactCapturedValue()
    {
        var value = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");

        var id = new JsonBudgetLedgerInstanceId(value);

        id.Value.ShouldBe(value);
    }

    /// <summary>Verifies the diagnostic text is the canonical lowercase hyphenated identity.</summary>
    [Fact]
    public void ToString_WhenCalled_ReturnsCanonicalLowercaseText()
    {
        var value = Guid.Parse("A1A1A1A1-A1A1-A1A1-A1A1-A1A1A1A1A1A1");
        var id = new JsonBudgetLedgerInstanceId(value);

        id.ToString().ShouldBe(value.ToString("D"));
    }

    /// <summary>Verifies two identities wrapping the same value compare equal and hash alike.</summary>
    [Fact]
    public void Equals_WhenValuesMatch_ReportsValueEquality()
    {
        var value = Guid.NewGuid();
        var first = new JsonBudgetLedgerInstanceId(value);
        var second = new JsonBudgetLedgerInstanceId(value);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    /// <summary>Verifies two identities wrapping different values compare unequal.</summary>
    [Fact]
    public void Equals_WhenValuesDiffer_ReportsInequality()
    {
        var first = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        var second = new JsonBudgetLedgerInstanceId(Guid.NewGuid());

        second.ShouldNotBe(first);
    }
}
