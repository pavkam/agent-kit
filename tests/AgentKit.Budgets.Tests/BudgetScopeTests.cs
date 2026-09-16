// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;



/// <summary>Verifies BudgetScope behavior and contracts.</summary>
public sealed class BudgetScopeTests
{
    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerHolds_ReturnsTruthfulOrderedHolds()
    {
        var ledger = new RecordingBudgetLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        var reservationReference = new BudgetLedgerReservationReference(scopeReference, new BudgetReservationId(Guid.NewGuid()));
        var hold = new BudgetOverrunHold(new BudgetOverrunHoldReference(scopeReference, reservationReference, new BudgetAccountingRevision(1)), request.Dimension, request.Unit, request.Amount, request.Amount + 1, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        ledger.ReserveResult = new BudgetLedgerBatchReserveHeld([hold]);
        var scope = new BudgetScope(ledger, scopeReference);
        var result = await scope.ReserveBatchAsync([request], TestContext.Current.CancellationToken);
        result.ShouldBe(new BudgetBatchHeld([hold]));
        ledger.ReserveRequest!.OriginalRequests.ShouldBe([request]);
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerReserves_ReturnsHandlesInReceiptOrder()
    {
        var ledger = new RecordingBudgetLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var operationId = new OperationId(Guid.NewGuid());
        var firstRequest = TestFactory.ReservationRequest(scopeReference.Id, 1m, operationId);
        var secondRequest = TestFactory.ReservationRequest(scopeReference.Id, 2m, operationId);
        var firstReceipt = TestFactory.Receipt(scopeReference, firstRequest);
        var secondReceipt = TestFactory.Receipt(scopeReference, secondRequest);
        ledger.ReserveResult = new BudgetLedgerBatchReserved([firstReceipt, secondReceipt]);
        var scope = new BudgetScope(ledger, scopeReference);
        var result = await scope.ReserveBatchAsync([firstRequest, secondRequest], TestContext.Current.CancellationToken);
        var reserved = result.ShouldBeOfType<BudgetBatchReserved>();
        reserved.Reservations.Select(static reservation => reservation.Id).ShouldBe([firstReceipt.Reservation.Id, secondReceipt.Reservation.Id]);
    }

    [Fact]
    public async Task ReserveAsync_WhenLedgerRejectsOrHolds_MapsSingleOutcomes()
    {
        var ledger = new RecordingBudgetLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        var failure = new BudgetLimitFailure(scopeReference.Id, request.Dimension, BudgetLimitKind.Hard, 1m, 1m, 1m, request.Unit, "capacity exceeded");
        var scope = new BudgetScope(ledger, scopeReference);
        ledger.ReserveResult = new BudgetLedgerBatchReserveRejected(failure);
        var rejected = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);
        rejected.ShouldBe(new BudgetRejected(failure));
        var reservationReference = new BudgetLedgerReservationReference(scopeReference, new BudgetReservationId(Guid.NewGuid()));
        var hold = new BudgetOverrunHold(new BudgetOverrunHoldReference(scopeReference, reservationReference, new BudgetAccountingRevision(1)), request.Dimension, request.Unit, 1m, 2m, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        ledger.ReserveResult = new BudgetLedgerBatchReserveHeld([hold]);
        var held = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);
        held.ShouldBe(new BudgetHeld([hold]));
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
            Sample = (ref options) => options.Name == AgentKitActivityNames.BudgetSnapshot && options.Parent.SpanId == parentSpanId && options.Parent.TraceId == traceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetSnapshot && activity.ParentSpanId == parentSpanId && activity.TraceId == traceId)
                {
                    throw new InvalidOperationException("observer failed");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new RecordingBudgetLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        ledger.SnapshotResult = new BudgetSnapshot(scopeReference.Id, DateTimeOffset.UnixEpoch, [], []);
        var scope = new BudgetScope(ledger, scopeReference);
        var result = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        result.ShouldBeSameAs(ledger.SnapshotResult);
        Activity.Current.ShouldBeSameAs(parent);
    }



    [Fact]
    public void Constructors_WhenRequiredDependencyIsNull_ThrowExactParameterName()
    {
        var ledger = new RecordingLedgerBudgetRuntimeBoundary();
        var scope = Scope();
        Should.Throw<ArgumentNullException>(() => new BudgetScope(null!, scope)).ParamName.ShouldBe("ledger");
        Should.Throw<ArgumentNullException>(() => new BudgetScope(ledger, null!)).ParamName.ShouldBe("reference");
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerThrowsNonCancellationException_LogsFailureAndRethrows()
    {
        var failure = new InvalidOperationException("ledger unavailable");
        var ledger = new RecordingBudgetLedger
        {
            ReserveException = failure
        };
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        var logger = new CapturingLogger<BudgetScope>();
        var scope = new BudgetScope(ledger, scopeReference, logger);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await scope.ReserveBatchAsync([request], TestContext.Current.CancellationToken));
        exception.ShouldBeSameAs(failure);
        logger.Events.ShouldContain(entry => entry.EventId == 7012);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenLedgerThrowsNonCancellationException_LogsFailureAndRethrows()
    {
        var failure = new InvalidOperationException("ledger unavailable");
        var ledger = new RecordingBudgetLedger
        {
            SnapshotException = failure
        };
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var logger = new CapturingLogger<BudgetScope>();
        var scope = new BudgetScope(ledger, scopeReference, logger);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await scope.GetSnapshotAsync(TestContext.Current.CancellationToken));
        exception.ShouldBeSameAs(failure);
        logger.Events.ShouldContain(entry => entry.EventId == 7061);
    }

    private sealed class CapturingLogger<T>: ILogger<T>
    {
        public List<(int EventId, LogLevel Level)> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Events.Add((eventId.Id, logLevel));
    }

    private static BudgetLedgerScopeReference Scope() => new(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
    private sealed class RecordingLedgerBudgetRuntimeBoundary: IBudgetLedger
    {
        public BudgetLedgerDescriptor? DescriptorValue { get; set; } = new(false, BudgetLedgerConcurrencyDomain.ProcessLocal);
        public int DescriptorReads { get; private set; }

        public BudgetLedgerDescriptor Descriptor
        {
            get
            {
                DescriptorReads++;
                return DescriptorValue!;
            }
        }

        public int Calls { get; private set; }
        public bool Cancel { get; init; }
        public BudgetLedgerScopeCreateResult CreateResult { get; set; } = null!;
        public BudgetLedgerBatchReserveResult ReserveResult { get; set; } = null!;
        public BudgetStartResult StartResult { get; set; } = null!;
        public BudgetCommitResult CommitResult { get; set; } = null!;
        public BudgetCorrectionResult CorrectionResult { get; set; } = null!;
        public BudgetSnapshot SnapshotResult { get; set; } = null!;
        public BudgetLedgerReleaseResult ReleaseResult { get; set; } = null!;
        public CancellationToken CreateToken { get; private set; }
        public CancellationToken ReserveToken { get; private set; }
        public CancellationToken StartToken { get; private set; }
        public CancellationToken SettleToken { get; private set; }
        public CancellationToken CorrectToken { get; private set; }
        public CancellationToken SnapshotToken { get; private set; }
        public CancellationToken ReleaseToken { get; private set; }

        public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CreateToken = cancellationToken;
            return Result(CreateResult, cancellationToken);
        }

        public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            ReserveToken = cancellationToken;
            return Result(ReserveResult, cancellationToken);
        }

        public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
        {
            Calls++;
            StartToken = cancellationToken;
            return Result(StartResult, cancellationToken);
        }

        public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            SettleToken = cancellationToken;
            return Result(CommitResult, cancellationToken);
        }

        public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CorrectToken = cancellationToken;
            return Result(CorrectionResult, cancellationToken);
        }

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default)
        {
            Calls++;
            SnapshotToken = cancellationToken;
            return Result(SnapshotResult, cancellationToken);
        }

        public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
        {
            Calls++;
            ReleaseToken = cancellationToken;
            return Result(ReleaseResult, cancellationToken);
        }

        public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private ValueTask<T> Result<T>(T value, CancellationToken token) => Cancel ? ValueTask.FromCanceled<T>(token) : ValueTask.FromResult(value);
    }
}
