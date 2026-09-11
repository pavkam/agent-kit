// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;



/// <summary>Verifies ReconciliationState behavior and contracts.</summary>
public sealed class ReconciliationStateTests
{
    /// <summary>Verifies reconciliation replay state reports the exact invalid constructor parameter.</summary>
    [Fact]
    public void ReconciliationState_WhenArgumentIsNull_ThrowsWithExactParameterName()
    {
        var evidenceException = Should.Throw<ArgumentNullException>(() => new ReconciliationState(null!, null!));
        evidenceException.ParamName.ShouldBe("evidence");
        var resultException = Should.Throw<ArgumentNullException>(() => new ReconciliationState(new BudgetStillUnknown(), null!));
        resultException.ParamName.ShouldBe("result");
    }
}
