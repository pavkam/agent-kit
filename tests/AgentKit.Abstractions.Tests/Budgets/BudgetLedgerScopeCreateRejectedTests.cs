// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerScopeCreateRejected behavior and contracts.</summary>
public sealed class BudgetLedgerScopeCreateRejectedTests
{
    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsNull_ThrowArgumentNullException() => Should.Throw<ArgumentNullException>(() => new BudgetLedgerScopeCreateRejected(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsPresent_PreserveAllFields()
    {
        var scopeFailure = new BudgetScopeCreationFailed(BudgetScopeCreationFailureKind.InvalidLimit, "safe");
        new BudgetLedgerScopeCreateRejected(scopeFailure).Failure.ShouldBe(scopeFailure);
    }
}
