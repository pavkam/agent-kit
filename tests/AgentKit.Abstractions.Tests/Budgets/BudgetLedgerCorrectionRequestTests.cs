// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerCorrectionRequest behavior and contracts.</summary>
public sealed class BudgetLedgerCorrectionRequestTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BudgetLedgerCorrectionRequest_WhenRevisionIsNotPositive_ThrowsExactParameterName(long revision)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerCorrectionRequest(Reservation(), 0m, revision));
        exception.ParamName.ShouldBe("revision");
    }

    [Fact]
    public void BudgetLedgerCorrectionRequest_WhenReservationIsNullOrValuesAreInvalid_ThrowsExactExceptionAndParameterName()
    {
        var nullReservation = Should.Throw<ArgumentNullException>(() => new BudgetLedgerCorrectionRequest(null!, 0m, 1));
        var negativeActual = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerCorrectionRequest(Reservation(), -1m, 1));
        var zeroRevision = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerCorrectionRequest(Reservation(), 0m, 0));
        var negativeRevision = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerCorrectionRequest(Reservation(), 0m, -1));
        nullReservation.ParamName.ShouldBe("reservation");
        negativeActual.ParamName.ShouldBe("correctedActual");
        zeroRevision.ParamName.ShouldBe("revision");
        negativeRevision.ParamName.ShouldBe("revision");
    }

    [Fact]
    public void BudgetLedgerCorrectionRequest_WhenBoundaryValuesAreValid_PreservesAllFields()
    {
        var reservation = Reservation();
        var request = new BudgetLedgerCorrectionRequest(reservation, 0m, 1);
        request.Reservation.ShouldBe(reservation);
        request.CorrectedActual.ShouldBe(0m);
        request.Revision.ShouldBe(1);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerCorrectionRequest(Reservation(), 0m, 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetLedgerReservationReference Reservation() => new(Scope(), ReservationId());
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
