// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

public sealed class BudgetResultContractsTests
{
    [Fact]
    public void BudgetStarted_WhenReservationIdIsDefault_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetStarted(default, false));

        exception.ParamName.ShouldBe("reservationId");
    }

    [Fact]
    public void BudgetCorrectionResult_WhenReservationIdIsDefault_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetCorrectionResult(default, 0m, 0m, 1));

        exception.ParamName.ShouldBe("reservationId");
    }

    [Theory]
    [InlineData(-1, 0, 1, "previousActual")]
    [InlineData(0, -1, 1, "correctedActual")]
    [InlineData(0, 0, 0, "revision")]
    public void BudgetCorrectionResult_WhenNumericConstraintIsInvalid_ThrowsWithExactParameterName(
        int previousActual,
        int correctedActual,
        long revision,
        string expectedParameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetCorrectionResult(
                new BudgetReservationId(Guid.NewGuid()),
                previousActual,
                correctedActual,
                revision));

        exception.ParamName.ShouldBe(expectedParameterName);
    }

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

    [Theory]
    [InlineData(typeof(BudgetStarted), nameof(BudgetStarted.ReservationId))]
    [InlineData(typeof(BudgetCorrectionResult), nameof(BudgetCorrectionResult.Revision))]
    [InlineData(typeof(BudgetBatchReserved), nameof(BudgetBatchReserved.Reservations))]
    public void ResultProperty_WhenConstructorValidated_HasNoSetter(Type type, string propertyName) => type.GetProperty(propertyName)!.SetMethod.ShouldBeNull();
}
