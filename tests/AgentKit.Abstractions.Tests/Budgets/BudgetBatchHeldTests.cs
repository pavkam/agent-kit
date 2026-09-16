// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetBatchHeld behavior and contracts.</summary>
public sealed class BudgetBatchHeldTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var hold = Hold();
        var held = new BudgetBatchHeld([hold]);
        _ = held.Holds.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_WhenHoldsIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetBatchHeld(default));
        exception.ParamName.ShouldBe("holds");
    }

    [Fact]
    public void Constructor_WhenHoldsIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetBatchHeld([]));
        exception.ParamName.ShouldBe("holds");
    }

    [Fact]
    public void Constructor_WhenHoldsContainsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetBatchHeld([null!]));
        exception.ParamName.ShouldBe("holds");
    }

    [Fact]
    public void Equals_WhenHoldsMatch_InstancesAreEqual()
    {
        var hold = Hold();
        var first = new BudgetBatchHeld([hold]);
        var second = new BudgetBatchHeld([hold]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetBatchHeld([Hold()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetOverrunHoldReference HoldReference()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        return new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.Parse("00000000-0000-0000-0000-000000000003"))), new BudgetAccountingRevision(1));
    }

    private static BudgetOverrunHold Hold() => new(HoldReference(), new BudgetDimension("tests.overrun"), new BudgetUnit("count"), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
}
