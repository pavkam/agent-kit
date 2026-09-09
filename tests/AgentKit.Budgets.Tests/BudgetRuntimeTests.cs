// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class BudgetRuntimeTests
{
    [Fact]
    public void HeldResultConstructors_WhenHoldsInvalid_ThrowWithExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetHeld(default)).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetHeld([])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetHeld([null!])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetBatchHeld(default)).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetBatchHeld([])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetBatchHeld([null!])).ParamName.ShouldBe("holds");
    }

    [Fact]
    public void BudgetHeld_WhenArraysHaveEqualOrderedContent_HasStructuralEquality()
    {
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scope.Id);
        var reservation = new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid()));
        var first = new BudgetOverrunHold(
            new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1)),
            request.Dimension, request.Unit, 1m, 2m, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var second = first with { };

        var singleLeft = new BudgetHeld([first]);
        var singleRight = new BudgetHeld([second]);
        var batchLeft = new BudgetBatchHeld([first]);
        var batchRight = new BudgetBatchHeld([second]);

        singleLeft.ShouldBe(singleRight);
        singleLeft.GetHashCode().ShouldBe(singleRight.GetHashCode());
        batchLeft.ShouldBe(batchRight);
        batchLeft.GetHashCode().ShouldBe(batchRight.GetHashCode());
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenLedgerCreatesScope_ReturnsHandleAndCapturedPolicy()
    {
        var ledger = new RecordingLedger();
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions(BudgetOverrunBehavior.RequireOperatorReconciliation));
        var request = TestFactory.ScopeRequest();
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), request.Address);
        ledger.CreateResult = new BudgetLedgerScopeCreated(reference);

        var result = await authority.CreateChildScopeAsync(request, TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<BudgetScopeCreated>();
        created.Scope.Id.ShouldBe(reference.Id);
        created.Scope.Address.ShouldBe(reference.Address);
        ledger.CreateRequest!.Admission.OverrunHoldPolicy.ShouldBe(BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        ledger.CreateRequest.OriginalRequest.ShouldBe(request);
        created.Scope.ShouldNotBeAssignableTo<IRunBudget>();
    }

    [Fact]
    public async Task RuntimeWithExplicitInMemoryLedger_WhenReplayAndStartedHandleDisposed_PreservesLedgerTruth()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging().AddAgentBudgets().AddInMemoryBudgetLedger();
        using var provider = services.BuildServiceProvider();
        var authority = provider.GetRequiredService<IBudgetAuthority>();
        var scope = ((BudgetScopeCreated) await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(), TestContext.Current.CancellationToken)).Scope;
        var request = new BudgetReservationRequest(
            scope.Id,
            BudgetDimensions.InputTokens,
            3m,
            new BudgetUnit("tokens"),
            new OperationId(Guid.NewGuid()),
            null,
            new IdempotencyKey(Guid.NewGuid().ToString()));

        var first = ((BudgetReserved) await scope.ReserveAsync(
            request, TestContext.Current.CancellationToken)).Reservation;
        var replay = ((BudgetReserved) await scope.ReserveAsync(
            request, TestContext.Current.CancellationToken)).Reservation;
        _ = await first.MarkStartedAsync(TestContext.Current.CancellationToken);
        await first.DisposeAsync();
        var snapshot = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);

        replay.Id.ShouldBe(first.Id);
        snapshot.Usages.Single(usage => usage.Dimension == request.Dimension).Reserved.ShouldBe(
            BudgetQuantity.FromDecimal(request.Amount));
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerHolds_ReturnsTruthfulOrderedHolds()
    {
        var ledger = new RecordingLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        var reservationReference = new BudgetLedgerReservationReference(scopeReference, new BudgetReservationId(Guid.NewGuid()));
        var hold = new BudgetOverrunHold(
            new BudgetOverrunHoldReference(scopeReference, reservationReference, new BudgetAccountingRevision(1)),
            request.Dimension, request.Unit, request.Amount, request.Amount + 1, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        ledger.ReserveResult = new BudgetLedgerBatchReserveHeld([hold]);
        var scope = new BudgetScope(ledger, scopeReference);

        var result = await scope.ReserveBatchAsync([request], TestContext.Current.CancellationToken);

        result.ShouldBe(new BudgetBatchHeld([hold]));
        ledger.ReserveRequest!.OriginalRequests.ShouldBe([request]);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenLedgerRejects_ReturnsExactFailure()
    {
        var failure = new BudgetScopeCreationFailed(BudgetScopeCreationFailureKind.MaximumDepthExceeded, "depth exceeded");
        var ledger = new RecordingLedger { CreateResult = new BudgetLedgerScopeCreateRejected(failure) };
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());

        var result = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenLoggerFactoryThrows_ReturnsCommittedLedgerResult()
    {
        var request = TestFactory.ScopeRequest();
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), request.Address);
        var ledger = new RecordingLedger { CreateResult = new BudgetLedgerScopeCreated(reference) };
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions(), new ThrowingLoggerFactory());

        var result = await authority.CreateChildScopeAsync(request, TestContext.Current.CancellationToken);

        ((BudgetScopeCreated) result).Scope.Id.ShouldBe(reference.Id);
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerReserves_ReturnsHandlesInReceiptOrder()
    {
        var ledger = new RecordingLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var operationId = new OperationId(Guid.NewGuid());
        var firstRequest = TestFactory.ReservationRequest(scopeReference.Id, 1m, operationId);
        var secondRequest = TestFactory.ReservationRequest(scopeReference.Id, 2m, operationId);
        var firstReceipt = TestFactory.Receipt(scopeReference, firstRequest);
        var secondReceipt = TestFactory.Receipt(scopeReference, secondRequest);
        ledger.ReserveResult = new BudgetLedgerBatchReserved([firstReceipt, secondReceipt]);
        var scope = new BudgetScope(ledger, scopeReference);

        var result = await scope.ReserveBatchAsync(
            [firstRequest, secondRequest], TestContext.Current.CancellationToken);

        var reserved = result.ShouldBeOfType<BudgetBatchReserved>();
        reserved.Reservations.Select(static reservation => reservation.Id).ShouldBe(
            [firstReceipt.Reservation.Id, secondReceipt.Reservation.Id]);
    }

    [Fact]
    public async Task ReserveAsync_WhenLedgerRejectsOrHolds_MapsSingleOutcomes()
    {
        var ledger = new RecordingLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        var failure = new BudgetLimitFailure(
            scopeReference.Id, request.Dimension, BudgetLimitKind.Hard, 1m, 1m, 1m, request.Unit, "capacity exceeded");
        var scope = new BudgetScope(ledger, scopeReference);
        ledger.ReserveResult = new BudgetLedgerBatchReserveRejected(failure);

        var rejected = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);

        rejected.ShouldBe(new BudgetRejected(failure));
        var reservationReference = new BudgetLedgerReservationReference(scopeReference, new BudgetReservationId(Guid.NewGuid()));
        var hold = new BudgetOverrunHold(
            new BudgetOverrunHoldReference(scopeReference, reservationReference, new BudgetAccountingRevision(1)),
            request.Dimension, request.Unit, 1m, 2m, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        ledger.ReserveResult = new BudgetLedgerBatchReserveHeld([hold]);

        var held = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);

        held.ShouldBe(new BudgetHeld([hold]));
    }

    [Fact]
    public async Task Reservation_WhenUsed_DelegatesExactReceiptAndDisposalRetainsLedgerSemantics()
    {
        var ledger = new RecordingLedger();
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scope.Id, 3m);
        var receipt = TestFactory.Receipt(scope, request);
        var reservation = new BudgetReservation(ledger, receipt);
        ledger.StartResult = new BudgetStarted(receipt.Reservation.Id, false);
        ledger.CommitResult = new BudgetCommitResult(receipt.Reservation.Id, 3m, 2m, 1m, 0m);
        ledger.CorrectionResult = new BudgetCorrectionResult(receipt.Reservation.Id, 2m, 1m, 1);
        ledger.ReleaseResult = new BudgetLedgerRetainedStarted(receipt.Reservation);

        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        var commit = await reservation.CommitAsync(2m, TestContext.Current.CancellationToken);
        var correction = await reservation.CorrectAsync(1m, 1, TestContext.Current.CancellationToken);
        await reservation.DisposeAsync();

        reservation.Id.ShouldBe(receipt.Reservation.Id);
        reservation.ScopeId.ShouldBe(scope.Id);
        reservation.Dimension.ShouldBe(request.Dimension);
        reservation.Reserved.ShouldBe(3m);
        ledger.StartReference.ShouldBe(receipt.Reservation);
        ledger.SettlementRequest!.Actual.ShouldBe(2m);
        ledger.CorrectionRequest!.CorrectedActual.ShouldBe(1m);
        ledger.ReleaseReference.ShouldBe(receipt.Reservation);
        commit.ShouldBeSameAs(ledger.CommitResult);
        correction.ShouldBeSameAs(ledger.CorrectionResult);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DisposeAsync_WhenLedgerPersistenceFails_RetryForwardsExactReferenceWithoutCancellation(
        bool acknowledgementUnknown,
        bool retainStarted)
    {
        var failure = new BudgetLedgerPersistenceUnavailableException("unavailable", acknowledgementUnknown);
        var ledger = new RecordingLedger { ReleaseException = failure };
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var receipt = TestFactory.Receipt(scope, TestFactory.ReservationRequest(scope.Id));
        var reservation = new BudgetReservation(ledger, receipt);

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(DisposeAsync);
        ledger.ReleaseException = null;
        ledger.ReleaseResult = retainStarted
            ? new BudgetLedgerRetainedStarted(ledger.ReleaseReferences.Single())
            : new BudgetLedgerReleased(ledger.ReleaseReferences.Single());
        await reservation.DisposeAsync();

        exception.ShouldBeSameAs(failure);
        ledger.ReleaseReferences.Count.ShouldBe(2);
        ledger.ReleaseReferences.ShouldAllBe(reference => reference == receipt.Reservation);
        ledger.ReleaseTokens.ShouldAllBe(static token => token == CancellationToken.None);

        async Task DisposeAsync() => await reservation.DisposeAsync();
    }

    [Fact]
    public async Task SnapshotAndDispose_WhenCustomLedgerSelected_EmitRuntimeActivities()
    {
        using var parent = new Activity("budget-runtime-test").Start();
        var activities = new System.Collections.Concurrent.ConcurrentQueue<
            (string Name, ActivityStatusCode Status, string? ScopeId)>();
        var parentSpanId = parent.SpanId;
        var traceId = parent.TraceId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) =>
                options.Parent.TraceId == traceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parentSpanId
                    && activity.TraceId == traceId
                    && activity.OperationName is AgentKitActivityNames.BudgetSnapshot or AgentKitActivityNames.BudgetRelease)
                {
                    activities.Enqueue((
                        activity.OperationName,
                        activity.Status,
                        activity.GetTagItem(AgentKitTagNames.BudgetScopeId)?.ToString()));
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var measurements = new System.Collections.Concurrent.ConcurrentQueue<
            (string Outcome, bool HasDimension, bool HasScope)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, enabledListener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.BudgetSnapshotCount)
                {
                    enabledListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var current = Activity.Current;
            if (current?.OperationName != AgentKitActivityNames.BudgetSnapshot
                || current.ParentSpanId != parentSpanId
                || current.TraceId != traceId)
            {
                return;
            }

            var values = tags.ToArray();
            measurements.Enqueue((
                (string) values.Single(tag => tag.Key == AgentKitTagNames.Outcome).Value!,
                values.Any(tag => tag.Key == AgentKitTagNames.BudgetDimension),
                values.Any(tag => tag.Key == AgentKitTagNames.BudgetScopeId)));
        });
        meterListener.Start();
        var ledger = new RecordingLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        ledger.SnapshotResult = new BudgetSnapshot(scopeReference.Id, DateTimeOffset.UnixEpoch, [], []);
        var scopeLogger = new CapturingLogger<BudgetScope>();
        var reservationLogger = new CapturingLogger<BudgetReservation>();
        var scope = new BudgetScope(ledger, scopeReference, scopeLogger, reservationLogger);
        var reservation = new BudgetReservation(ledger, TestFactory.Receipt(scopeReference, request), reservationLogger);
        _ = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        ledger.ReleaseResult = new BudgetLedgerRetainedStarted(
            new BudgetLedgerReservationReference(scopeReference, reservation.Id));
        await reservation.DisposeAsync();

        activities.ShouldContain((AgentKitActivityNames.BudgetSnapshot, ActivityStatusCode.Ok, scopeReference.Id.ToString()));
        activities.ShouldContain((AgentKitActivityNames.BudgetRelease, ActivityStatusCode.Ok, scopeReference.Id.ToString()));
        measurements.ShouldContain(("read", false, false));
        scopeLogger.Events.ShouldContain(entry => entry.EventId == 7060 && entry.Level == LogLevel.Debug);
        scopeLogger.Events.Where(static entry => entry.EventId == 7060)
            .SelectMany(static entry => entry.Keys)
            .ShouldNotContain(AgentKitTagNames.BudgetDimension);
        reservationLogger.Events.ShouldContain(entry => entry.EventId == 7020);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenActivityListenerThrows_RestoresParentAndReturnsLedgerResult()
    {
        using var parent = new Activity("budget-runtime-parent").Start();
        var parentSpanId = parent.SpanId;
        var traceId = parent.TraceId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) =>
                options.Name == AgentKitActivityNames.BudgetSnapshot
                && options.Parent.SpanId == parentSpanId
                && options.Parent.TraceId == traceId
                    ? ActivitySamplingResult.AllData
                    : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetSnapshot
                    && activity.ParentSpanId == parentSpanId
                    && activity.TraceId == traceId)
                {
                    throw new InvalidOperationException("observer failed");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new RecordingLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        ledger.SnapshotResult = new BudgetSnapshot(scopeReference.Id, DateTimeOffset.UnixEpoch, [], []);
        var scope = new BudgetScope(ledger, scopeReference);

        var result = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(ledger.SnapshotResult);
        Activity.Current.ShouldBeSameAs(parent);
    }

    internal sealed class RecordingLedger: IBudgetLedger
    {
        public BudgetLedgerScopeCreateResult CreateResult { get; set; } = null!;
        public BudgetLedgerBatchReserveResult ReserveResult { get; set; } = null!;
        public BudgetStartResult StartResult { get; set; } = null!;
        public BudgetCommitResult CommitResult { get; set; } = null!;
        public BudgetCorrectionResult CorrectionResult { get; set; } = null!;
        public BudgetLedgerReleaseResult ReleaseResult { get; set; } = null!;
        public BudgetSnapshot SnapshotResult { get; set; } = null!;
        public Exception? ReleaseException { get; set; }
        public BudgetLedgerScopeCreateRequest? CreateRequest { get; private set; }
        public BudgetLedgerBatchReserveRequest? ReserveRequest { get; private set; }
        public BudgetLedgerReservationReference? StartReference { get; private set; }
        public BudgetLedgerSettlementRequest? SettlementRequest { get; private set; }
        public BudgetLedgerCorrectionRequest? CorrectionRequest { get; private set; }
        public BudgetLedgerReservationReference? ReleaseReference { get; private set; }
        public List<BudgetLedgerReservationReference> ReleaseReferences { get; } = [];
        public List<CancellationToken> ReleaseTokens { get; } = [];

        public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
        { CreateRequest = request; return ValueTask.FromResult(CreateResult); }
        public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
        { ReserveRequest = request; return ValueTask.FromResult(ReserveResult); }
        public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
        { StartReference = reservation; return ValueTask.FromResult(StartResult); }
        public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
        { SettlementRequest = request; return ValueTask.FromResult(CommitResult); }
        public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
        {
            ReleaseReference = reservation;
            ReleaseReferences.Add(reservation);
            ReleaseTokens.Add(cancellationToken);
            return ReleaseException is null
                ? ValueTask.FromResult(ReleaseResult)
                : ValueTask.FromException<BudgetLedgerReleaseResult>(ReleaseException);
        }
        public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
        { CorrectionRequest = request; return ValueTask.FromResult(CorrectionResult); }
        public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default) => ValueTask.FromResult(SnapshotResult);
        public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingLoggerFactory: ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) => throw new InvalidOperationException("observer failed");
        public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException("observer failed");
        public void Dispose() { }
    }

    private sealed class CapturingLogger<T>: ILogger<T>
    {
        public List<(int EventId, LogLevel Level, string[] Keys)> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var keys = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.Select(static value => value.Key).ToArray()
                : [];
            Events.Add((eventId.Id, logLevel, keys));
        }
    }
}
