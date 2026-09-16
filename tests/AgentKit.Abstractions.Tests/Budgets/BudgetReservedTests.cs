// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetReserved behavior and contracts.</summary>
public sealed class BudgetReservedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var reservation = new TestBudgetReservation();
        var reserved = new BudgetReserved(reservation);
        reserved.Reservation.ShouldBeSameAs(reservation);
    }

    [Fact]
    public void Constructor_WhenReservationIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetReserved(null!));
        exception.ParamName.ShouldBe("reservation");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var reservation = new TestBudgetReservation();
        var original = new BudgetReserved(reservation);
        var copy = original with { };
        copy.Reservation.ShouldBeSameAs(reservation);
    }
}
