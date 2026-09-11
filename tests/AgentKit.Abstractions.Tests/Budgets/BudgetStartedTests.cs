// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetStarted behavior and contracts.</summary>
public sealed class BudgetStartedTests
{
    [Fact]
    public void BudgetStarted_WhenReservationIdIsDefault_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetStarted(default, false));
        exception.ParamName.ShouldBe("reservationId");
    }

    [Fact]
    public void ReservationId_WhenApiShapeIsInspected_HasNoSetter() => typeof(BudgetStarted).GetProperty(nameof(BudgetStarted.ReservationId))!.SetMethod.ShouldBeNull();

}
