// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetUnresolvedReservationPage behavior and contracts.</summary>
public sealed class BudgetUnresolvedReservationPageTests
{
    [Fact]
    public void BudgetUnresolvedReservationPage_WhenScopeOrWatermarkDiffers_IsNotEqual()
    {
        var watermark = new BudgetLedgerWatermark(1);
        var first = new BudgetUnresolvedReservationPage(Scope(), watermark, [], null);
        var second = new BudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(2), [], null);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void BudgetUnresolvedReservationPage_WhenContinuationIsInvalid_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, ReservationId(), "first")];
        var next = new BudgetReservationCursor(scope, watermark, ReservationId());
        var exception = Should.Throw<ArgumentException>(() => new BudgetUnresolvedReservationPage(scope, watermark, items, next));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void BudgetUnresolvedReservationPage_WhenEveryEqualityFieldMatches_HasEqualHashCode()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationId();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, watermark, id);
        var first = new BudgetUnresolvedReservationPage(scope, watermark, items, next);
        var second = new BudgetUnresolvedReservationPage(scope, watermark, items, next);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void BudgetUnresolvedReservationPage_WhenAnyEqualityFieldDiffers_IsNotEqual()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationId();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var first = new BudgetUnresolvedReservationPage(scope, watermark, items, null);
        var differentScope = new BudgetUnresolvedReservationPage(Scope(), watermark, [], null);
        var differentWatermark = new BudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(2), items, null);
        ImmutableArray<BudgetUnresolvedReservation> differentItems = [Unresolved(scope, ReservationId(), "second")];
        var differentItemPage = new BudgetUnresolvedReservationPage(scope, watermark, differentItems, null);
        var differentNext = new BudgetUnresolvedReservationPage(scope, watermark, items, new BudgetReservationCursor(scope, watermark, id));
        first.ShouldNotBe(differentScope);
        first.ShouldNotBe(differentWatermark);
        first.ShouldNotBe(differentItemPage);
        first.ShouldNotBe(differentNext);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
    private static BudgetReservationRequest Request(BudgetScopeId scopeId, decimal amount = 1m, string dimension = "tests.requests", string unit = "requests", string idempotencyKey = "key", OperationId? operationId = null) => new(scopeId, new BudgetDimension(dimension), amount, new BudgetUnit(unit), operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")), null, new IdempotencyKey(idempotencyKey));
    private static BudgetLedgerReservationReceipt Receipt(BudgetLedgerScopeReference scope, BudgetReservationId reservationId, string idempotencyKey, string dimension = "tests.requests", string unit = "requests", OperationId? operationId = null) => new(new BudgetLedgerReservationReference(scope, reservationId), Request(scope.Id, dimension: dimension, unit: unit, idempotencyKey: idempotencyKey, operationId: operationId), new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, ReservationId(), "first")];
        var original = new BudgetUnresolvedReservationPage(scope, watermark, items, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetUnresolvedReservation Unresolved(BudgetLedgerScopeReference scope, BudgetReservationId reservationId, string idempotencyKey) => new(Receipt(scope, reservationId, idempotencyKey), DateTimeOffset.UnixEpoch.AddTicks(-1));
}
