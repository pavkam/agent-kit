// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerReconciliationRequest behavior and contracts.</summary>
public sealed class BudgetLedgerReconciliationRequestTests
{
    [Fact]
    public void BudgetLedgerReconciliationRequest_WhenRequiredValuesAreInvalid_ThrowsExactExceptionAndParameterName()
    {
        var nullReservation = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationRequest(null!, new BudgetNoUsageProven(), new IdempotencyKey("reconcile")));
        var nullEvidence = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationRequest(Reservation(), null!, new IdempotencyKey("reconcile")));
        var defaultKey = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationRequest(Reservation(), new BudgetNoUsageProven(), default));
        nullReservation.ParamName.ShouldBe("reservation");
        nullEvidence.ParamName.ShouldBe("evidence");
        defaultKey.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void BudgetLedgerReconciliationRequest_WhenInputsAreValid_PreservesAllFields()
    {
        var reservation = Reservation();
        var evidence = new BudgetActualEstimated(0m);
        var idempotencyKey = new IdempotencyKey("reconcile");
        var request = new BudgetLedgerReconciliationRequest(reservation, evidence, idempotencyKey);
        request.Reservation.ShouldBe(reservation);
        request.Evidence.ShouldBe(evidence);
        request.IdempotencyKey.ShouldBe(idempotencyKey);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerReconciliationRequest(Reservation(), new BudgetActualEstimated(0m), new IdempotencyKey("reconcile"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerReservationReference Reservation() => new(Scope(), ReservationId());
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
