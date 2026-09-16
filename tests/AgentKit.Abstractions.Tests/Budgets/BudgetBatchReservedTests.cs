// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetBatchReserved behavior and contracts.</summary>
public sealed class BudgetBatchReservedTests
{
    [Fact]
    public void BudgetBatchReserved_WhenReservationsAreDefault_ThrowsWithExactParameterName()
    {
        ImmutableArray<IBudgetReservation> reservations = default;
        var exception = Should.Throw<ArgumentException>(() => new BudgetBatchReserved(reservations));
        exception.ParamName.ShouldBe("reservations");
    }

    [Fact]
    public void BudgetBatchReserved_WhenReservationsAreEmpty_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetBatchReserved([]));
        exception.ParamName.ShouldBe("reservations");
    }

    [Fact]
    public void BudgetBatchReserved_WhenReservationsContainNull_ThrowsWithExactParameterName()
    {
        ImmutableArray<IBudgetReservation> reservations = [null!];
        var exception = Should.Throw<ArgumentException>(() => new BudgetBatchReserved(reservations));
        exception.ParamName.ShouldBe("reservations");
    }

    [Fact]
    public void Reservations_WhenApiShapeIsInspected_HasNoSetter() => typeof(BudgetBatchReserved).GetProperty(nameof(BudgetBatchReserved.Reservations))!.SetMethod.ShouldBeNull();

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        ImmutableArray<IBudgetReservation> reservations = [new TestBudgetReservation()];
        var batch = new BudgetBatchReserved(reservations);
        batch.Reservations.ShouldBe(reservations);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        ImmutableArray<IBudgetReservation> reservations = [new TestBudgetReservation()];
        var original = new BudgetBatchReserved(reservations);
        var copy = original with { };
        copy.Reservations.ShouldBe(reservations);
    }
}
