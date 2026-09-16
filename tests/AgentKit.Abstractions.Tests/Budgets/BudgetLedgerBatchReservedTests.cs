// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerBatchReserved behavior and contracts.</summary>
public sealed class BudgetLedgerBatchReservedTests
{
    [Fact]
    public void BudgetLedgerBatchReserved_WhenReceiptsAreMixed_RejectsBeforePropertyAssignment()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(Scope(), ReservationId(), "first"), Receipt(Scope(), ReservationId(), "second")];
        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserved(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void BudgetLedgerBatchReserved_WhenReceiptOriginalOperationsDiffer_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(scope, ReservationId(), "first", operationId: OperationId()), Receipt(scope, ReservationId(), "second", operationId: OperationId())];
        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserved(receipts));
        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void BudgetLedgerBatchReserved_WhenReceiptSequenceMatches_HasEqualHashCode()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(scope, ReservationId(), "first"), Receipt(scope, ReservationId(), "second")];
        var first = new BudgetLedgerBatchReserved(receipts);
        var second = new BudgetLedgerBatchReserved(receipts);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void BudgetLedgerBatchReserved_WhenReceiptOrderDiffers_IsNotEqual()
    {
        var scope = Scope();
        var firstReceipt = Receipt(scope, ReservationId(), "first");
        var secondReceipt = Receipt(scope, ReservationId(), "second");
        var first = new BudgetLedgerBatchReserved([firstReceipt, secondReceipt]);
        var second = new BudgetLedgerBatchReserved([secondReceipt, firstReceipt]);
        first.ShouldNotBe(second);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
    private static OperationId OperationId() => new(Guid.NewGuid());
    private static BudgetReservationRequest Request(BudgetScopeId scopeId, decimal amount = 1m, string dimension = "tests.requests", string unit = "requests", string idempotencyKey = "key", OperationId? operationId = null) => new(scopeId, new BudgetDimension(dimension), amount, new BudgetUnit(unit), operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")), null, new IdempotencyKey(idempotencyKey));
    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(scope, ReservationId(), "first")];
        var original = new BudgetLedgerBatchReserved(receipts);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerReservationReceipt Receipt(BudgetLedgerScopeReference scope, BudgetReservationId reservationId, string idempotencyKey, string dimension = "tests.requests", string unit = "requests", OperationId? operationId = null) => new(new BudgetLedgerReservationReference(scope, reservationId), Request(scope.Id, dimension: dimension, unit: unit, idempotencyKey: idempotencyKey, operationId: operationId), new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
}
