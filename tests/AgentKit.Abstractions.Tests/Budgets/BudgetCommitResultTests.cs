// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;
/// <summary>Verifies BudgetCommitResult behavior and contracts.</summary>
public sealed class BudgetCommitResultTests
{
    [Fact]
    public void BudgetCommitResult_WhenPresentAccountingRevisionIsDefault_ThrowsExactParameterName() => Should.Throw<ArgumentOutOfRangeException>(() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, default(BudgetAccountingRevision), [])).ParamName.ShouldBe("accountingRevision");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BudgetCommitResult_WhenHoldArrayIsDefault_ThrowsExactParameterName(bool presentRevision)
    {
        BudgetAccountingRevision? revision = presentRevision ? new BudgetAccountingRevision(1) : null;
        Should.Throw<ArgumentException>(() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, revision, default)).ParamName.ShouldBe("createdOverrunHolds");
    }

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
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

    private static (BudgetOverrunHold First, BudgetOverrunHold Second) Holds()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("20000000-0000-0000-0000-000000000002")), address);
        var reservation = new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.Parse("30000000-0000-0000-0000-000000000003")));
        var first = new BudgetOverrunHold(new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1)), new BudgetDimension("test.sum"), new BudgetUnit("count"), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var second = new BudgetOverrunHold(new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(2)), new BudgetDimension("test.sum"), new BudgetUnit("count"), 1, 3, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        return (first, second);
    }

    [Fact]
    public void OverrunResultArrays_WhenDefaultOrContainingNull_ThrowExactParameterName() => Should.Throw<ArgumentException>(() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, null, [null!])).ParamName.ShouldBe("createdOverrunHolds");

    [Fact]
    public void OverrunResultArrays_WhenEmptyIsAllowed_PreserveEmptyEvidence() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, null, []).CreatedOverrunHolds.ShouldBeEmpty();
}
