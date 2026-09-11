// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;



/// <summary>Verifies BudgetHeld behavior and contracts.</summary>
public sealed class BudgetHeldTests
{
    [Fact]
    public void BudgetHeld_WhenArraysHaveEqualOrderedContent_HasStructuralEquality()
    {
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scope.Id);
        var reservation = new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid()));
        var first = new BudgetOverrunHold(new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1)), request.Dimension, request.Unit, 1m, 2m, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var second = first with
        {
        };
        var singleLeft = new BudgetHeld([first]);
        var singleRight = new BudgetHeld([second]);
        var batchLeft = new BudgetBatchHeld([first]);
        var batchRight = new BudgetBatchHeld([second]);
        singleLeft.ShouldBe(singleRight);
        singleLeft.GetHashCode().ShouldBe(singleRight.GetHashCode());
        batchLeft.ShouldBe(batchRight);
        batchLeft.GetHashCode().ShouldBe(batchRight.GetHashCode());
    }

    [Fact]
    public void HeldResultConstructors_WhenHoldsInvalid_ThrowWithExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetHeld(default)).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetHeld([])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetHeld([null!])).ParamName.ShouldBe("holds");
    }
}
