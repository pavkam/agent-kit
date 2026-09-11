// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;
/// <summary>Verifies BudgetLedgerBatchReserveHeld behavior and contracts.</summary>
public sealed class BudgetLedgerBatchReserveHeldTests
{
    [Fact]
    public void BudgetLedgerBatchReserveHeld_WhenArrayIsInvalid_ThrowsAndValidEvidenceIsRetained()
    {
        Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveHeld(default)).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveHeld([])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveHeld([null!])).ParamName.ShouldBe("holds");
        _ = new BudgetLedgerBatchReserveHeld([Hold()]).Holds.ShouldHaveSingleItem();
    }

    private static BudgetOverrunHoldReference HoldReference()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        return new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, ReservationId()), new BudgetAccountingRevision(1));
    }

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static BudgetDimension Dimension() => new("tests.overrun");
    private static BudgetUnit Unit() => new("count");
    private static BudgetOverrunHold Hold() => new(HoldReference(), Dimension(), Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
    [Fact]
    public void BudgetLedgerBatchReserveHeld_WhenArraysHaveEqualContents_UsesOrderedContentEquality()
    {
        var (first, second) = Holds();
        var left = new BudgetLedgerBatchReserveHeld([first, second]);
        var equal = new BudgetLedgerBatchReserveHeld([first, second]);
        var reordered = new BudgetLedgerBatchReserveHeld([second, first]);
        equal.ShouldBe(left);
        equal.GetHashCode().ShouldBe(left.GetHashCode());
        reordered.ShouldNotBe(left);
    }

    private static (BudgetOverrunHold First, BudgetOverrunHold Second) Holds()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("20000000-0000-0000-0000-000000000002")), address);
        var reservation = new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.Parse("30000000-0000-0000-0000-000000000003")));
        var first = new BudgetOverrunHold(new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1)), new BudgetDimension("test.sum"), new BudgetUnit("count"), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var second = new BudgetOverrunHold(new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(2)), new BudgetDimension("test.sum"), new BudgetUnit("count"), 1, 3, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        return (first, second);
    }
}
