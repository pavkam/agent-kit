// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;



/// <summary>Verifies BudgetBatchHeld behavior and contracts.</summary>
public sealed class BudgetBatchHeldTests
{
    [Fact]
    public void HeldResultConstructors_WhenHoldsInvalid_ThrowWithExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetBatchHeld(default)).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetBatchHeld([])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetBatchHeld([null!])).ParamName.ShouldBe("holds");
    }
}
