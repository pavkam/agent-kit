// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

public sealed class BudgetOverrunEqualityTests
{
    [Fact]
    public void BudgetCommitResult_WhenArraysHaveEqualContents_UsesOrderedContentEquality()
    {
        var (first, second) = Holds();
        var left = new BudgetCommitResult(first.Reference.Reservation.Id, 1, 2, 0, 1, new BudgetAccountingRevision(3), [first, second]);
        var equal = new BudgetCommitResult(first.Reference.Reservation.Id, 1, 2, 0, 1, new BudgetAccountingRevision(3), [first, second]);
        var reordered = new BudgetCommitResult(first.Reference.Reservation.Id, 1, 2, 0, 1, new BudgetAccountingRevision(3), [second, first]);

        equal.ShouldBe(left);
        equal.GetHashCode().ShouldBe(left.GetHashCode());
        reordered.ShouldNotBe(left);
    }

    [Fact]
    public void BudgetCorrectionResult_WhenArraysHaveEqualContents_UsesOrderedContentEquality()
    {
        var (first, second) = Holds();
        var left = new BudgetCorrectionResult(first.Reference.Reservation.Id, 2, 1, 1, new BudgetAccountingRevision(3), [first, second], [first.Reference, second.Reference]);
        var equal = new BudgetCorrectionResult(first.Reference.Reservation.Id, 2, 1, 1, new BudgetAccountingRevision(3), [first, second], [first.Reference, second.Reference]);
        var reordered = new BudgetCorrectionResult(first.Reference.Reservation.Id, 2, 1, 1, new BudgetAccountingRevision(3), [second, first], [second.Reference, first.Reference]);

        equal.ShouldBe(left);
        equal.GetHashCode().ShouldBe(left.GetHashCode());
        reordered.ShouldNotBe(left);
    }

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

    [Fact]
    public void BudgetOverrunHoldResolutionBlocked_WhenArraysHaveEqualContents_UsesOrderedContentEquality()
    {
        var (first, second) = Holds();
        var left = new BudgetOverrunHoldResolutionBlocked(first.Reference, [first, second], []);
        var equal = new BudgetOverrunHoldResolutionBlocked(first.Reference, [first, second], []);
        var reordered = new BudgetOverrunHoldResolutionBlocked(first.Reference, [second, first], []);

        equal.ShouldBe(left);
        equal.GetHashCode().ShouldBe(left.GetHashCode());
        reordered.ShouldNotBe(left);
    }

    private static (BudgetOverrunHold First, BudgetOverrunHold Second) Holds()
    {
        var address = new BudgetScopeAddress(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            null,
            null,
            null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("20000000-0000-0000-0000-000000000002")), address);
        var reservation = new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.Parse("30000000-0000-0000-0000-000000000003")));
        var first = new BudgetOverrunHold(
            new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1)),
            new BudgetDimension("test.sum"),
            new BudgetUnit("count"),
            1,
            2,
            BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var second = new BudgetOverrunHold(
            new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(2)),
            new BudgetDimension("test.sum"),
            new BudgetUnit("count"),
            1,
            3,
            BudgetOverrunHoldPolicy.ClearWhenReconciled);
        return (first, second);
    }
}
