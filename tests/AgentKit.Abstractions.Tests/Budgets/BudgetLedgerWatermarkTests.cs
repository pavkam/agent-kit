// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerWatermark behavior and contracts.</summary>
public sealed class BudgetLedgerWatermarkTests: Conformance.LongIdentityConformanceTests<BudgetLedgerWatermark>
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetLedgerWatermark_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerWatermark(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void BudgetLedgerWatermark_WhenValueIsPositive_PreservesValueAndInvariantText()
    {
        var watermark = new BudgetLedgerWatermark(1);
        watermark.Value.ShouldBe(1);
        watermark.ToString().ShouldBe("1");
    }

    /// <inheritdoc/>
    protected override BudgetLedgerWatermark Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(BudgetLedgerWatermark subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
