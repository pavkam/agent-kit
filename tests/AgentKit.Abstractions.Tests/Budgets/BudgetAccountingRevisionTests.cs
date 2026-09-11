// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetAccountingRevision behavior and contracts.</summary>
public sealed class BudgetAccountingRevisionTests: Conformance.LongIdentityConformanceTests<BudgetAccountingRevision>
{
    [Fact]
    public void BudgetAccountingRevision_WhenValueIsNotPositive_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetAccountingRevision(0));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override BudgetAccountingRevision Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(BudgetAccountingRevision subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
