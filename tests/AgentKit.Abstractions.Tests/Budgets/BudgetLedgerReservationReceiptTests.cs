// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerReservationReceipt behavior and contracts.</summary>
public sealed class BudgetLedgerReservationReceiptTests
{
    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeDoesNotMatchOriginalRequest_ThrowsExactParameterName()
    {
        var scope = Scope();
        var otherScope = Scope();
        var original = Requests(otherScope.Id)[0];
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerReservationReceipt(reservation, original, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch)));
        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenCopiedRequestIsInvalid_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        var originalRequest = Request(scope.Id) with
        {
            Amount = 0m
        };
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerReservationReceipt(reservation, originalRequest, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch)));
        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenRequiredValuesAreNull_ThrowsArgumentNullException()
    {
        var scope = Scope();
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var request = Request(scope.Id);
        var effectiveReservation = new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch);
        var nullReservation = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReservationReceipt(null!, request, effectiveReservation));
        var nullRequest = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReservationReceipt(reservation, null!, effectiveReservation));
        var nullEffectiveReservation = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReservationReceipt(reservation, request, null!));
        nullReservation.ParamName.ShouldBe("reservation");
        nullRequest.ParamName.ShouldBe("originalRequest");
        nullEffectiveReservation.ParamName.ShouldBe("effectiveReservation");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenExplicitExpiryDoesNotMatch_ThrowsArgumentException()
    {
        var scope = Scope();
        var request = Request(scope.Id) with
        {
            ExpiresAt = DateTimeOffset.UnixEpoch
        };
        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerReservationReceipt(new BudgetLedgerReservationReference(scope, ReservationId()), request, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch.AddTicks(1))));
        exception.ParamName.ShouldBe("effectiveReservation");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeBindsDifferentOperation_ThrowsArgumentException()
    {
        var scope = Scope(OperationId());
        var request = Request(scope.Id, operationId: OperationId());
        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerReservationReceipt(new BudgetLedgerReservationReference(scope, ReservationId()), request, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch)));
        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeDoesNotBindOperation_AcceptsRequestOperation()
    {
        var scope = Scope();
        var request = Request(scope.Id, operationId: OperationId());
        var receipt = new BudgetLedgerReservationReceipt(new BudgetLedgerReservationReference(scope, ReservationId()), request, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
        receipt.OriginalRequest.ShouldBe(request);
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeBindsMatchingOperationAndExplicitExpiry_PreservesAllFields()
    {
        var operationId = OperationId();
        var scope = Scope(operationId);
        var expiresAt = DateTimeOffset.UnixEpoch;
        var request = Request(scope.Id, operationId: operationId) with
        {
            ExpiresAt = expiresAt
        };
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var effectiveReservation = new BudgetEffectiveReservation(expiresAt);
        var receipt = new BudgetLedgerReservationReceipt(reservation, request, effectiveReservation);
        receipt.Reservation.ShouldBe(reservation);
        receipt.OriginalRequest.ShouldBe(request);
        receipt.EffectiveReservation.ShouldBe(effectiveReservation);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
    private static OperationId OperationId() => new(Guid.NewGuid());
    private static BudgetReservationRequest Request(BudgetScopeId scopeId, decimal amount = 1m, string dimension = "tests.requests", string unit = "requests", string idempotencyKey = "key", OperationId? operationId = null) => new(scopeId, new BudgetDimension(dimension), amount, new BudgetUnit(unit), operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")), null, new IdempotencyKey(idempotencyKey));
    private static ImmutableArray<BudgetReservationRequest> Requests(BudgetScopeId scopeId) => [Request(scopeId, idempotencyKey: "one"), Request(scopeId, amount: 2m, dimension: "tests.tokens", unit: "tokens", idempotencyKey: "two")];
}
