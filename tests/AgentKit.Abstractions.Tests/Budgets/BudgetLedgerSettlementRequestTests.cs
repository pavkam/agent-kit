// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerSettlementRequest behavior and contracts.</summary>
public sealed class BudgetLedgerSettlementRequestTests
{
    [Fact]
    public void BudgetLedgerSettlementRequest_WhenActualIsNegative_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerSettlementRequest(Reservation(), -1m));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void BudgetLedgerSettlementRequest_WhenReservationIsNullOrActualIsNegative_ThrowsExactExceptionAndParameterName()
    {
        var nullReservation = Should.Throw<ArgumentNullException>(() => new BudgetLedgerSettlementRequest(null!, 0m));
        var negativeActual = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerSettlementRequest(Reservation(), -1m));
        nullReservation.ParamName.ShouldBe("reservation");
        negativeActual.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void BudgetLedgerSettlementRequest_WhenActualIsZero_PreservesAllFields()
    {
        var reservation = Reservation();
        var request = new BudgetLedgerSettlementRequest(reservation, 0m);
        request.Reservation.ShouldBe(reservation);
        request.Actual.ShouldBe(0m);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerSettlementRequest(Reservation(), 0m);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetLedgerReservationReference Reservation() => new(Scope(), ReservationId());
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
