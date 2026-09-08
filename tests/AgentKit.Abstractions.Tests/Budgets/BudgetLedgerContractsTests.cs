// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

public sealed class BudgetLedgerContractsTests
{
    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenOriginalBatchIsValid_PreservesOnlyOriginalEvidence()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);

        var request = new BudgetLedgerBatchReserveRequest(scope, requests);

        request.Scope.ShouldBe(scope);
        request.OriginalRequests.ShouldBe(requests);
        typeof(BudgetLedgerBatchReserveRequest).GetProperty(nameof(BudgetLedgerBatchReserveRequest.OriginalRequests))!.SetMethod.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetScopeAdmission_WhenDefaultLifetimeIsNotPositive_ThrowsExactParameterName(int ticks)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetScopeAdmission(1, 1, TimeSpan.FromTicks(ticks)));

        exception.ParamName.ShouldBe("defaultReservationLifetime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetUnresolvedReservationQuery_WhenPageSizeIsOutsideBound_ThrowsExactParameterName(int pageSize)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetUnresolvedReservationQuery(Scope(), pageSize, null));

        exception.ParamName.ShouldBe("pageSize");
    }

    [Fact]
    public void BudgetUnresolvedReservationQuery_WhenCursorBelongsToAnotherScope_ThrowsExactParameterName()
    {
        var firstScope = Scope();
        var otherScope = Scope();
        var cursor = new BudgetReservationCursor(firstScope, new BudgetLedgerWatermark(1), ReservationId());

        var exception = Should.Throw<ArgumentException>(
            () => new BudgetUnresolvedReservationQuery(otherScope, 1, cursor));

        exception.ParamName.ShouldBe("after");
    }

    [Fact]
    public void BudgetLedgerSettlementRequest_WhenActualIsNegative_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerSettlementRequest(Reservation(), -1m));

        exception.ParamName.ShouldBe("actual");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BudgetLedgerCorrectionRequest_WhenRevisionIsNotPositive_ThrowsExactParameterName(long revision)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerCorrectionRequest(Reservation(), 0m, revision));

        exception.ParamName.ShouldBe("revision");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BudgetActualMeasured_WhenActualIsNegative_ThrowsExactParameterName(int actual)
    {
        if (actual == 0)
        {
            _ = Should.NotThrow(() => new BudgetActualMeasured(actual));
            return;
        }

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetActualMeasured(actual));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeDoesNotMatchOriginalRequest_ThrowsExactParameterName()
    {
        var scope = Scope();
        var otherScope = Scope();
        var original = Requests(otherScope.Id)[0];
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());

        var exception = Should.Throw<ArgumentException>(
            () => new BudgetLedgerReservationReceipt(reservation, original, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch)));

        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerScopeReference_WhenIdIsDefault_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerScopeReference(default, Address()));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void BudgetUnresolvedReservationPage_WhenScopeOrWatermarkDiffers_IsNotEqual()
    {
        var watermark = new BudgetLedgerWatermark(1);
        var first = new BudgetUnresolvedReservationPage(Scope(), watermark, [], null);
        var second = new BudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(2), [], null);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenRequestIsValid_DoesNotThrow()
    {
        var request = Request(Scope().Id, amount: decimal.One);

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(request));
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenRequestIsNull_UsesInferredParameterName()
    {
        BudgetReservationRequest request = null!;

        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(request));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenRequestIsNull_UsesExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(null!, "candidate"));

        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenScopeIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var request = Request(Scope().Id) with { ScopeId = default };

        AssertInvalidReservationRequest<ArgumentOutOfRangeException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenDimensionIsDefault_ThrowsArgumentNullException()
    {
        var request = Request(Scope().Id) with { Dimension = default };

        AssertInvalidReservationRequest<ArgumentNullException>(request);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenAmountIsNotPositive_ThrowsArgumentOutOfRangeException(int amount)
    {
        var request = Request(Scope().Id) with { Amount = amount };

        AssertInvalidReservationRequest<ArgumentOutOfRangeException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenUnitIsDefault_ThrowsArgumentNullException()
    {
        var request = Request(Scope().Id) with { Unit = default };

        AssertInvalidReservationRequest<ArgumentNullException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenOperationIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var request = Request(Scope().Id) with { OperationId = default };

        AssertInvalidReservationRequest<ArgumentOutOfRangeException>(request);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerReservationRequest_WhenIdempotencyKeyIsDefault_ThrowsArgumentNullException()
    {
        var request = Request(Scope().Id) with { IdempotencyKey = default };

        AssertInvalidReservationRequest<ArgumentNullException>(request);
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenCopiedRequestIsInvalid_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        var originalRequest = Request(scope.Id) with { Amount = 0m };
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerReservationReceipt(reservation, originalRequest, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch)));

        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenCopiedRequestIsInvalid_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        requests = requests.SetItem(0, requests[0] with { Amount = 0m });

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerBatchReserveRequest(scope, requests));

        exception.ParamName.ShouldBe("originalRequests");
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenScopeBindsAnotherOperation_RejectsBeforePropertyAssignment()
    {
        var scopeOperation = OperationId();
        var scope = Scope(scopeOperation);
        var requestOperation = OperationId();
        ImmutableArray<BudgetReservationRequest> requests =
        [
            Request(scope.Id, operationId: requestOperation),
            Request(scope.Id, amount: 2m, idempotencyKey: "second", operationId: requestOperation)
        ];

        var exception = Should.Throw<ArgumentException>(
            () => new BudgetLedgerBatchReserveRequest(scope, requests));

        exception.ParamName.ShouldBe("originalRequests");
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenScopeBindsBatchOperation_AcceptsOriginalEvidence()
    {
        var operationId = OperationId();
        var scope = Scope(operationId);
        ImmutableArray<BudgetReservationRequest> requests =
        [
            Request(scope.Id, operationId: operationId),
            Request(scope.Id, amount: 2m, idempotencyKey: "second", operationId: operationId)
        ];

        var request = Should.NotThrow(() => new BudgetLedgerBatchReserveRequest(scope, requests));

        request.OriginalRequests.ShouldBe(requests);
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsAreValid_DoesNotThrow()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(scope, ReservationId(), "first"), Receipt(scope, ReservationId(), "second")];

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsAreDefault_UsesInferredParameterName()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = default;

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsAreEmpty_UsesExplicitParameterName()
    {
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts([], "candidate"));

        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReceiptsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [null!];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenScopesDiffer_ThrowsArgumentException()
    {
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(Scope(), ReservationId(), "first"), Receipt(Scope(), ReservationId(), "second")];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenReservationIdentityRepeats_ThrowsArgumentException()
    {
        var scope = Scope();
        var reservationId = ReservationId();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(scope, reservationId, "first"), Receipt(scope, reservationId, "second")];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenItemKeyRepeats_ThrowsArgumentException()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts = [Receipt(scope, ReservationId(), "same"), Receipt(scope, ReservationId(), "same")];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenOriginalOperationsDiffer_ThrowsArgumentException()
    {
        var scope = Scope();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts =
        [
            Receipt(scope, ReservationId(), "first", operationId: OperationId()),
            Receipt(scope, ReservationId(), "second", operationId: OperationId())
        ];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetLedgerBatchReceipts_WhenDimensionUnitsDiffer_ThrowsArgumentException()
    {
        var scope = Scope();
        var operationId = OperationId();
        ImmutableArray<BudgetLedgerReservationReceipt> receipts =
        [
            Receipt(scope, ReservationId(), "first", dimension: "shared", unit: "requests", operationId: operationId),
            Receipt(scope, ReservationId(), "second", dimension: "shared", unit: "tokens", operationId: operationId)
        ];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

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
        ImmutableArray<BudgetLedgerReservationReceipt> receipts =
        [
            Receipt(scope, ReservationId(), "first", operationId: OperationId()),
            Receipt(scope, ReservationId(), "second", operationId: OperationId())
        ];

        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserved(receipts));

        exception.ParamName.ShouldBe("receipts");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenPageIsValidWithoutContinuation_DoesNotThrow()
        => Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(1), [], null));

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenPageIsValidWithContinuation_DoesNotThrow()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationId("00000000-0000-0000-0000-000000000001");
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, watermark, id);

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, watermark, items, next));
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        BudgetLedgerScopeReference scope = null!;

        var exception = Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), [], null));

        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenWatermarkIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), default, [], null));

        exception.ParamName.ShouldBe("watermark");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsAreDefault_UsesInferredParameterName()
    {
        ImmutableArray<BudgetUnresolvedReservation> items = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(1), items, null));

        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<BudgetUnresolvedReservation> items = [null!];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(Scope(), new BudgetLedgerWatermark(1), items, null));

        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemScopeDiffers_UsesExplicitParameterName()
    {
        var scope = Scope();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(Scope(), ReservationId(), "first")];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), items, null, "candidate"));

        exception.ParamName.ShouldBe("candidate");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsAreDescending_ThrowsArgumentException()
    {
        var scope = Scope();
        ImmutableArray<BudgetUnresolvedReservation> items =
        [
            Unresolved(scope, ReservationId("00000000-0000-0000-0000-000000000002"), "second"),
            Unresolved(scope, ReservationId("00000000-0000-0000-0000-000000000001"), "first")
        ];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), items, null));

        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenItemsRepeatIdentity_ThrowsArgumentException()
    {
        var scope = Scope();
        var id = ReservationId("00000000-0000-0000-0000-000000000001");
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first"), Unresolved(scope, id, "second")];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, new BudgetLedgerWatermark(1), items, null));

        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenContinuationScopeDiffers_ThrowsArgumentException()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationId();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(Scope(), watermark, id);

        AssertInvalidPage(scope, watermark, items, next);
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenContinuationWatermarkDiffers_ThrowsArgumentException()
    {
        var scope = Scope();
        var id = ReservationId();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, new BudgetLedgerWatermark(2), id);

        AssertInvalidPage(scope, new BudgetLedgerWatermark(1), items, next);
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenEmptyPageHasContinuation_ThrowsArgumentException()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var next = new BudgetReservationCursor(scope, watermark, ReservationId());

        AssertInvalidPage(scope, watermark, [], next);
    }

    [Fact]
    public void ThrowIfInvalidBudgetUnresolvedReservationPage_WhenContinuationDoesNotAnchorLastItem_ThrowsArgumentException()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var id = ReservationId();
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, id, "first")];
        var next = new BudgetReservationCursor(scope, watermark, ReservationId());

        AssertInvalidPage(scope, watermark, items, next);
    }

    [Fact]
    public void BudgetUnresolvedReservationPage_WhenContinuationIsInvalid_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        ImmutableArray<BudgetUnresolvedReservation> items = [Unresolved(scope, ReservationId(), "first")];
        var next = new BudgetReservationCursor(scope, watermark, ReservationId());

        var exception = Should.Throw<ArgumentException>(
            () => new BudgetUnresolvedReservationPage(scope, watermark, items, next));

        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void BudgetLedgerScopeCreateRequest_WhenOriginalIdempotencyKeyIsDefault_RejectsBeforePropertyAssignment()
    {
        var originalRequest = ScopeRequest() with { IdempotencyKey = default };

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerScopeCreateRequest(originalRequest, Admission()));

        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerScopeCreateRequest_WhenOriginalParentScopeIdIsPresentAndDefault_RejectsBeforePropertyAssignment()
    {
        var originalRequest = ScopeRequest() with
        {
            ParentScopeId = (BudgetScopeId?) default(BudgetScopeId)
        };

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerScopeCreateRequest(originalRequest, Admission()));

        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerScopeCreateRequest_WhenOriginalReplayCoordinatesAreValid_PreservesEvidence()
    {
        var originalRequest = ScopeRequest();
        var admission = Admission();

        var request = Should.NotThrow(() => new BudgetLedgerScopeCreateRequest(originalRequest, admission));

        request.OriginalRequest.ShouldBe(originalRequest);
        request.Admission.ShouldBe(admission);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, -1)]
    public void BudgetScopeAdmission_WhenAnyCapturedBoundIsNotPositive_ThrowsArgumentOutOfRangeException(
        int maximumScopeDepth,
        int maximumOpenReservations,
        int lifetimeTicks)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetScopeAdmission(maximumScopeDepth, maximumOpenReservations, TimeSpan.FromTicks(lifetimeTicks)));

        exception.ParamName.ShouldBe(
            maximumScopeDepth <= 0
                ? "maximumScopeDepth"
                : maximumOpenReservations <= 0
                    ? "maximumOpenReservationsPerScope"
                    : "defaultReservationLifetime");
    }

    [Fact]
    public void BudgetScopeAdmission_WhenCapturedBoundsAreAtPositiveBoundary_PreservesAllFields()
    {
        var admission = new BudgetScopeAdmission(1, 1, TimeSpan.FromTicks(1));

        admission.MaximumScopeDepth.ShouldBe(1);
        admission.MaximumOpenReservationsPerScope.ShouldBe(1);
        admission.DefaultReservationLifetime.ShouldBe(TimeSpan.FromTicks(1));
    }

    [Fact]
    public void BudgetEffectiveReservation_WhenExpiryIsSupplied_PreservesAbsoluteInstant()
    {
        var expiresAt = DateTimeOffset.UnixEpoch.AddTicks(1);

        var effectiveReservation = new BudgetEffectiveReservation(expiresAt);

        effectiveReservation.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void BudgetLedgerScopeReference_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), null!));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void BudgetLedgerScopeReference_WhenInputsAreValid_PreservesAllFields()
    {
        var id = new BudgetScopeId(Guid.NewGuid());
        var address = Address();

        var reference = new BudgetLedgerScopeReference(id, address);

        reference.Id.ShouldBe(id);
        reference.Address.ShouldBe(address);
    }

    [Fact]
    public void BudgetLedgerReservationReference_WhenScopeIsNullOrIdIsDefault_ThrowsExactExceptionAndParameterName()
    {
        var nullScope = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReservationReference(null!, ReservationId()));
        var defaultId = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerReservationReference(Scope(), default));

        nullScope.ParamName.ShouldBe("scope");
        defaultId.ParamName.ShouldBe("id");
    }

    [Fact]
    public void BudgetLedgerReservationReference_WhenInputsAreValid_PreservesAllFields()
    {
        var scope = Scope();
        var id = ReservationId();

        var reference = new BudgetLedgerReservationReference(scope, id);

        reference.Scope.ShouldBe(scope);
        reference.Id.ShouldBe(id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetLedgerWatermark_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerWatermark(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void BudgetLedgerWatermark_WhenValueIsPositive_PreservesValueAndInvariantText()
    {
        var watermark = new BudgetLedgerWatermark(1);

        watermark.Value.ShouldBe(1);
        watermark.ToString().ShouldBe("1");
    }

    [Fact]
    public void BudgetReservationCursor_WhenScopeOrCoordinatesAreInvalid_ThrowsExactExceptionAndParameterName()
    {
        var nullScope = Should.Throw<ArgumentNullException>(() => new BudgetReservationCursor(null!, new BudgetLedgerWatermark(1), ReservationId()));
        var defaultWatermark = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetReservationCursor(Scope(), default, ReservationId()));
        var defaultReservation = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetReservationCursor(Scope(), new BudgetLedgerWatermark(1), default));

        nullScope.ParamName.ShouldBe("scope");
        defaultWatermark.ParamName.ShouldBe("watermark");
        defaultReservation.ParamName.ShouldBe("afterReservationId");
    }

    [Fact]
    public void BudgetReservationCursor_WhenCoordinatesAreValid_PreservesAllFields()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var reservationId = ReservationId();

        var cursor = new BudgetReservationCursor(scope, watermark, reservationId);

        cursor.Scope.ShouldBe(scope);
        cursor.Watermark.ShouldBe(watermark);
        cursor.AfterReservationId.ShouldBe(reservationId);
    }

    [Fact]
    public void BudgetUnresolvedReservationQuery_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetUnresolvedReservationQuery(null!, 1, null));

        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void BudgetUnresolvedReservationQuery_WhenInputsAreValid_PreservesAllFields()
    {
        var scope = Scope();
        var cursor = new BudgetReservationCursor(scope, new BudgetLedgerWatermark(1), ReservationId());

        var query = new BudgetUnresolvedReservationQuery(scope, 1, cursor);

        query.Scope.ShouldBe(scope);
        query.PageSize.ShouldBe(1);
        query.After.ShouldBe(cursor);
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
        var request = Request(scope.Id) with { ExpiresAt = DateTimeOffset.UnixEpoch };

        var exception = Should.Throw<ArgumentException>(
            () => new BudgetLedgerReservationReceipt(
                new BudgetLedgerReservationReference(scope, ReservationId()),
                request,
                new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch.AddTicks(1))));

        exception.ParamName.ShouldBe("effectiveReservation");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeBindsDifferentOperation_ThrowsArgumentException()
    {
        var scope = Scope(OperationId());
        var request = Request(scope.Id, operationId: OperationId());

        var exception = Should.Throw<ArgumentException>(
            () => new BudgetLedgerReservationReceipt(
                new BudgetLedgerReservationReference(scope, ReservationId()),
                request,
                new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch)));

        exception.ParamName.ShouldBe("originalRequest");
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeDoesNotBindOperation_AcceptsRequestOperation()
    {
        var scope = Scope();
        var request = Request(scope.Id, operationId: OperationId());

        var receipt = new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(scope, ReservationId()),
            request,
            new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));

        receipt.OriginalRequest.ShouldBe(request);
    }

    [Fact]
    public void BudgetLedgerReservationReceipt_WhenScopeBindsMatchingOperationAndExplicitExpiry_PreservesAllFields()
    {
        var operationId = OperationId();
        var scope = Scope(operationId);
        var expiresAt = DateTimeOffset.UnixEpoch;
        var request = Request(scope.Id, operationId: operationId) with { ExpiresAt = expiresAt };
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var effectiveReservation = new BudgetEffectiveReservation(expiresAt);

        var receipt = new BudgetLedgerReservationReceipt(reservation, request, effectiveReservation);

        receipt.Reservation.ShouldBe(reservation);
        receipt.OriginalRequest.ShouldBe(request);
        receipt.EffectiveReservation.ShouldBe(effectiveReservation);
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

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BudgetActualEstimated_WhenActualIsNegativeOrZero_EnforcesTheDocumentedBoundary(int actual)
    {
        if (actual == 0)
        {
            new BudgetActualEstimated(actual).Actual.ShouldBe(0m);
            return;
        }

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetActualEstimated(actual));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void BudgetUnresolvedReservation_WhenStartedAtIsAtOrAfterExpiry_ThrowsArgumentOutOfRangeException()
    {
        var scope = Scope();
        var expiresAt = DateTimeOffset.UnixEpoch;
        var receipt = new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(scope, ReservationId()),
            Request(scope.Id),
            new BudgetEffectiveReservation(expiresAt));

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
        var receipt = new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(scope, ReservationId()),
            Request(scope.Id),
            new BudgetEffectiveReservation(expiresAt));
        var startedAt = expiresAt.AddTicks(-1);

        var unresolved = new BudgetUnresolvedReservation(receipt, startedAt);

        unresolved.Receipt.ShouldBe(receipt);
        unresolved.StartedAt.ShouldBe(startedAt);
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenEveryEqualityFieldMatches_HasEqualHashCode()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        var first = new BudgetLedgerBatchReserveRequest(scope, requests);
        var second = new BudgetLedgerBatchReserveRequest(scope, requests);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenScopeOrOrderedOriginalRequestsDiffer_IsNotEqual()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        var first = new BudgetLedgerBatchReserveRequest(scope, requests);
        var otherScope = Scope();
        var differentScope = new BudgetLedgerBatchReserveRequest(otherScope, Requests(otherScope.Id));
        var differentRequests = new BudgetLedgerBatchReserveRequest(scope, [.. requests.Reverse()]);

        first.ShouldNotBe(differentScope);
        first.ShouldNotBe(differentRequests);
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

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerScopeCreated(null!)).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerScopeCreateRejected(null!)).ParamName.ShouldBe("failure");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerBatchReserveRejected(null!)).ParamName.ShouldBe("failure");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerReleased(null!)).ParamName.ShouldBe("reservation");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerRetainedStarted(null!)).ParamName.ShouldBe("reservation");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerAlreadySettled(null!)).ParamName.ShouldBe("commit");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationReleased(null!)).ParamName.ShouldBe("reservation");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationRetainedUnknown(null!)).ParamName.ShouldBe("reservation");
        Should.Throw<ArgumentNullException>(() => new BudgetLedgerReconciliationSettled(null!)).ParamName.ShouldBe("commit");
    }

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsPresent_PreserveAllFields()
    {
        var scope = Scope();
        var reservation = new BudgetLedgerReservationReference(scope, ReservationId());
        var commit = Commit(reservation.Id);
        var scopeFailure = new BudgetScopeCreationFailed(BudgetScopeCreationFailureKind.InvalidLimit, "safe");
        var limitFailure = LimitFailure(scope.Id);

        new BudgetLedgerScopeCreated(scope).Scope.ShouldBe(scope);
        new BudgetLedgerScopeCreateRejected(scopeFailure).Failure.ShouldBe(scopeFailure);
        new BudgetLedgerBatchReserveRejected(limitFailure).Failure.ShouldBe(limitFailure);
        new BudgetLedgerReleased(reservation).Reservation.ShouldBe(reservation);
        new BudgetLedgerRetainedStarted(reservation).Reservation.ShouldBe(reservation);
        new BudgetLedgerAlreadySettled(commit).Commit.ShouldBe(commit);
        new BudgetLedgerReconciliationReleased(reservation).Reservation.ShouldBe(reservation);
        new BudgetLedgerReconciliationRetainedUnknown(reservation).Reservation.ShouldBe(reservation);
        new BudgetLedgerReconciliationSettled(commit).Commit.ShouldBe(commit);
    }

    [Fact]
    public void BudgetLedgerReferenceUnavailableException_WhenSafeMessageIsInvalid_ThrowsArgumentException() => AssertSafeMessageValidation(message => new BudgetLedgerReferenceUnavailableException(message));

    [Fact]
    public void BudgetLedgerMutationConflictException_WhenSafeMessageIsInvalid_ThrowsArgumentException() => AssertSafeMessageValidation(message => new BudgetLedgerMutationConflictException(message));

    [Fact]
    public void BudgetLedgerStateException_WhenSafeMessageIsInvalid_ThrowsArgumentException() => AssertSafeMessageValidation(message => new BudgetLedgerStateException(message));

    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenSafeMessageIsInvalid_ThrowsArgumentException() => AssertSafeMessageValidation(message => new BudgetLedgerPersistenceUnavailableException(message, false));

    [Fact]
    public void BudgetLedgerSafeExceptions_WhenSafeMessageIsValid_PreserveSafeMessage()
    {
        const string message = "safe";

        new BudgetLedgerReferenceUnavailableException(message).SafeMessage.ShouldBe(message);
        new BudgetLedgerMutationConflictException(message).SafeMessage.ShouldBe(message);
        new BudgetLedgerStateException(message).SafeMessage.ShouldBe(message);
    }

    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenAcknowledgementIsKnownOrUnknown_PreservesState()
    {
        new BudgetLedgerPersistenceUnavailableException("safe", false).AcknowledgementUnknown.ShouldBeFalse();
        new BudgetLedgerPersistenceUnavailableException("safe", true).AcknowledgementUnknown.ShouldBeTrue();
    }

    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenInnerExceptionIsSupplied_PreservesCausalFailure()
    {
        var cause = new InvalidOperationException("adapter");

        var exception = new BudgetLedgerPersistenceUnavailableException("safe", true, cause);

        exception.SafeMessage.ShouldBe("safe");
        exception.AcknowledgementUnknown.ShouldBeTrue();
        exception.InnerException.ShouldBeSameAs(cause);
    }

    [Fact]
    public void BudgetLedgerPersistenceUnavailableException_WhenInnerExceptionIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new BudgetLedgerPersistenceUnavailableException("safe", false, null!));

        exception.ParamName.ShouldBe("innerException");
    }

    private static void AssertInvalidReservationRequest<TException>(BudgetReservationRequest request)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(() => ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(request));
        exception.ParamName.ShouldBe("request");
    }

    private static void AssertInvalidPage(
        BudgetLedgerScopeReference scope,
        BudgetLedgerWatermark watermark,
        ImmutableArray<BudgetUnresolvedReservation> items,
        BudgetReservationCursor next)
    {
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, watermark, items, next));

        exception.ParamName.ShouldBe("items");
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        new AgentId(Guid.NewGuid()),
        null,
        null,
        operationId);

    private static BudgetLedgerReservationReference Reservation() => new(Scope(), ReservationId());

    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());

    private static BudgetReservationId ReservationId(string value) => new(Guid.Parse(value));

    private static OperationId OperationId() => new(Guid.NewGuid());

    private static BudgetReservationRequest Request(
        BudgetScopeId scopeId,
        decimal amount = 1m,
        string dimension = "tests.requests",
        string unit = "requests",
        string idempotencyKey = "key",
        OperationId? operationId = null) => new(
            scopeId,
            new BudgetDimension(dimension),
            amount,
            new BudgetUnit(unit),
            operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")),
            null,
            new IdempotencyKey(idempotencyKey));

    private static BudgetLedgerReservationReceipt Receipt(
        BudgetLedgerScopeReference scope,
        BudgetReservationId reservationId,
        string idempotencyKey,
        string dimension = "tests.requests",
        string unit = "requests",
        OperationId? operationId = null) => new(
            new BudgetLedgerReservationReference(scope, reservationId),
            Request(scope.Id, dimension: dimension, unit: unit, idempotencyKey: idempotencyKey, operationId: operationId),
            new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));

    private static BudgetUnresolvedReservation Unresolved(
        BudgetLedgerScopeReference scope,
        BudgetReservationId reservationId,
        string idempotencyKey) => new(
            Receipt(scope, reservationId, idempotencyKey),
            DateTimeOffset.UnixEpoch.AddTicks(-1));

    private static ImmutableArray<BudgetReservationRequest> Requests(BudgetScopeId scopeId) =>
    [
        Request(scopeId, idempotencyKey: "one"),
        Request(scopeId, amount: 2m, dimension: "tests.tokens", unit: "tokens", idempotencyKey: "two")
    ];

    private static BudgetScopeRequest ScopeRequest() => new(null, Address(), [], new IdempotencyKey("scope"));

    private static BudgetScopeAdmission Admission() => new(1, 1, TimeSpan.FromMinutes(1));

    private static BudgetCommitResult Commit(BudgetReservationId reservationId) => new(reservationId, 1m, 1m, 0m, 0m);

    private static BudgetLimitFailure LimitFailure(BudgetScopeId scopeId) => new(
        scopeId,
        new BudgetDimension("tests.requests"),
        BudgetLimitKind.Hard,
        1m,
        0m,
        1m,
        new BudgetUnit("requests"),
        "safe");

    private static void AssertSafeMessageValidation<TException>(Func<string, TException> create)
        where TException : InvalidOperationException
    {
        var nullMessage = Should.Throw<ArgumentNullException>(() => create(null!));
        var emptyMessage = Should.Throw<ArgumentException>(() => create(string.Empty));
        var whitespaceMessage = Should.Throw<ArgumentException>(() => create(" "));

        nullMessage.ParamName.ShouldBe("safeMessage");
        emptyMessage.ParamName.ShouldBe("safeMessage");
        whitespaceMessage.ParamName.ShouldBe("safeMessage");
    }
}
