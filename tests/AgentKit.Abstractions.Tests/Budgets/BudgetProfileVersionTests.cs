// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetProfileVersion behavior and contracts.</summary>
public sealed class BudgetProfileVersionTests: Conformance.LongIdentityConformanceTests<BudgetProfileVersion>
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetProfileVersion_WhenNotPositive_ThrowsExactValueParameter(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetProfileVersion(value));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override BudgetProfileVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(BudgetProfileVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
