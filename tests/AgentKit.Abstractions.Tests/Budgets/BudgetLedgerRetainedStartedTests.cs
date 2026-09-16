// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerRetainedStarted behavior and contracts.</summary>
public sealed class BudgetLedgerRetainedStartedTests
{
    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsNull_ThrowArgumentNullException() => Should.Throw<ArgumentNullException>(() => new BudgetLedgerRetainedStarted(null!)).ParamName.ShouldBe("reservation");

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsPresent_PreserveAllFields()
    {
        var scope = Scope();
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        new BudgetLedgerRetainedStarted(reservation).Reservation.ShouldBe(reservation);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerRetainedStarted(new BudgetLedgerReservationReference(Scope(), ReservationId()));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
