// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>Verifies BudgetLimitFailure behavior and contracts.</summary>
public sealed class BudgetLimitFailureTests
{
    [Theory]
    [InlineData(-1, 0, 0, "configuredValue")]
    [InlineData(10, -1, 0, "observedValue")]
    [InlineData(10, 0, -1, "requestedAmount")]
    public void BudgetLimitFailure_WhenCompatibilityQuantityIsNegative_ThrowsBeforeConstruction(decimal configuredValue, decimal observedValue, decimal requestedAmount, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLimitFailure(new BudgetScopeId(Guid.NewGuid()), new BudgetDimension("tests.requests"), BudgetLimitKind.Hard, configuredValue, observedValue, requestedAmount, new BudgetUnit("requests"), "limit exceeded"));
        exception.ParamName.ShouldBe(parameterName);
    }
}
