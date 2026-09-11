// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetUnresolvedReservation behavior and contracts.</summary>
public sealed class BudgetUnresolvedReservationTests
{
    [Fact]
    public void BudgetUnresolvedReservation_WhenStartedAtIsAtOrAfterExpiry_ThrowsArgumentOutOfRangeException()
    {
        var scope = Scope();
        var expiresAt = DateTimeOffset.UnixEpoch;
        var receipt = new BudgetLedgerReservationReceipt(new BudgetLedgerReservationReference(scope, ReservationId()), Request(scope.Id), new BudgetEffectiveReservation(expiresAt));
        var atExpiry = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetUnresolvedReservation(receipt, expiresAt));
        var afterExpiry = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetUnresolvedReservation(receipt, expiresAt.AddTicks(1)));
        atExpiry.ParamName.ShouldBe("startedAt");
        afterExpiry.ParamName.ShouldBe("startedAt");
    }

    [Fact]
    public void BudgetUnresolvedReservation_WhenStartedBeforeExpiry_PreservesAllFields()
    {
        var scope = Scope();
        var expiresAt = DateTimeOffset.UnixEpoch;
        var receipt = new BudgetLedgerReservationReceipt(new BudgetLedgerReservationReference(scope, ReservationId()), Request(scope.Id), new BudgetEffectiveReservation(expiresAt));
        var startedAt = expiresAt.AddTicks(-1);
        var unresolved = new BudgetUnresolvedReservation(receipt, startedAt);
        unresolved.Receipt.ShouldBe(receipt);
        unresolved.StartedAt.ShouldBe(startedAt);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
    private static BudgetReservationRequest Request(BudgetScopeId scopeId, decimal amount = 1m, string dimension = "tests.requests", string unit = "requests", string idempotencyKey = "key", OperationId? operationId = null) => new(scopeId, new BudgetDimension(dimension), amount, new BudgetUnit(unit), operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")), null, new IdempotencyKey(idempotencyKey));
}
