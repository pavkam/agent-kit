// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;
/// <summary>Verifies BudgetCorrectionResult behavior and contracts.</summary>
public sealed class BudgetCorrectionResultTests
{
    [Fact]
    public void BudgetCorrectionResult_WhenPresentAccountingRevisionIsDefault_ThrowsExactParameterName() => Should.Throw<ArgumentOutOfRangeException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, default(BudgetAccountingRevision), [], [])).ParamName.ShouldBe("accountingRevision");

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    [Fact]
    public void BudgetCorrectionResult_WhenReservationIdIsDefault_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetCorrectionResult(default, 0m, 0m, 1));
        exception.ParamName.ShouldBe("reservationId");
    }

    [Theory]
    [InlineData(-1, 0, 1, "previousActual")]
    [InlineData(0, -1, 1, "correctedActual")]
    [InlineData(0, 0, 0, "revision")]
    public void BudgetCorrectionResult_WhenNumericConstraintIsInvalid_ThrowsWithExactParameterName(int previousActual, int correctedActual, long revision, string expectedParameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetCorrectionResult(new BudgetReservationId(Guid.NewGuid()), previousActual, correctedActual, revision));
        exception.ParamName.ShouldBe(expectedParameterName);
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
    public void OverrunResultArrays_WhenDefaultOrContainingNull_ThrowExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, default, [])).ParamName.ShouldBe("createdOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [null!], [])).ParamName.ShouldBe("createdOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [], default)).ParamName.ShouldBe("clearedOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [], [null!])).ParamName.ShouldBe("clearedOverrunHolds");
    }

    [Fact]
    public void OverrunResultArrays_WhenEmptyIsAllowed_PreserveEmptyEvidence()
    {
        var result = new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [], []);
        result.CreatedOverrunHolds.ShouldBeEmpty();
        result.ClearedOverrunHolds.ShouldBeEmpty();
    }

    [Fact]
    public void Revision_WhenApiShapeIsInspected_HasNoSetter() => typeof(BudgetCorrectionResult).GetProperty(nameof(BudgetCorrectionResult.Revision))!.SetMethod.ShouldBeNull();

}
