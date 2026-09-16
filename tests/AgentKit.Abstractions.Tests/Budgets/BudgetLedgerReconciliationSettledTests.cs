// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerReconciliationSettled behavior and contracts.</summary>
public sealed class BudgetLedgerReconciliationSettledTests
{
    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsNull_ThrowArgumentNullException() => Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationSettled(null!)).ParamName.ShouldBe("commit");

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsPresent_PreserveAllFields()
    {
        var scope = Scope();
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var commit = Commit(reservation.Id);
        new BudgetLedgerReconciliationSettled(commit).Commit.ShouldBe(commit);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerReconciliationSettled(Commit(ReservationId()));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
    private static BudgetCommitResult Commit(BudgetReservationId reservationId) => new(reservationId, 1m, 1m, 0m, 0m);
}
