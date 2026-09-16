// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;
/// <summary>Verifies BudgetOverrunHold behavior and contracts.</summary>
public sealed class BudgetOverrunHoldTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var reference = HoldReference();
        var hold = new BudgetOverrunHold(reference, Dimension(), Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        hold.Reference.ShouldBe(reference);
        hold.Dimension.ShouldBe(Dimension());
        hold.Unit.ShouldBe(Unit());
        hold.Reserved.ShouldBe(1);
        hold.CurrentActual.ShouldBe(2);
        hold.Policy.ShouldBe(BudgetOverrunHoldPolicy.ClearWhenReconciled);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetOverrunHold(HoldReference(), Dimension(), Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void BudgetOverrunHold_WhenRequiredReferenceIsNull_ThrowsExactParameterName() => Should.Throw<ArgumentNullException>(() => new BudgetOverrunHold(null!, Dimension(), Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe("reference");

    [Fact]
    public void BudgetOverrunHold_WhenDimensionOrUnitIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHold(HoldReference(), default, Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe("dimension");
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHold(HoldReference(), Dimension(), default, 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe("unit");
    }

    [Theory]
    [InlineData(-1, 1, "reserved")]
    [InlineData(1, -1, "currentActual")]
    public void BudgetOverrunHold_WhenAmountIsNegative_ThrowsExactParameterName(decimal reserved, decimal actual, string name) => Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHold(HoldReference(), Dimension(), Unit(), reserved, actual, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe(name);

    [Fact]
    public void BudgetOverrunHold_WhenPolicyIsUndefined_ThrowsExactParameterName() => Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHold(HoldReference(), Dimension(), Unit(), 1, 2, (BudgetOverrunHoldPolicy) int.MaxValue)).ParamName.ShouldBe("policy");

    private static BudgetOverrunHoldReference HoldReference()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        return new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, ReservationId()), new BudgetAccountingRevision(1));
    }

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static BudgetDimension Dimension() => new("tests.overrun");
    private static BudgetUnit Unit() => new("count");
}
