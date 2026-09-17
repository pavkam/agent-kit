// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;



/// <summary>Verifies BudgetAuthority behavior and contracts.</summary>
public sealed class BudgetAuthorityTests
{
    [Fact]
    public void BudgetAuthority_WhenLedgerDescriptorNull_ReadsOnceAndThrowsBeforeLedgerOperation()
    {
        var ledger = new RecordingLedger
        {
            DescriptorValue = null
        };
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetAuthority(ledger, TestFactory.DefaultOptions()));
        exception.ParamName.ShouldBe("ledger");
        ledger.DescriptorReads.ShouldBe(1);
        ledger.Calls.ShouldBe(0);
    }

    private sealed class RecordingLedger: IBudgetLedger
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

    [Fact]
    public async Task CreateAndReserve_WhenRequestIsNullOrBatchInvalid_RejectBeforeLedger()
    {
        var ledger = new RecordingLedger();
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        var scope = new BudgetScope(ledger, Scope());
        (await Should.ThrowAsync<ArgumentNullException>(async () => await authority.CreateChildScopeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await scope.ReserveAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentException>(async () => await scope.ReserveBatchAsync([]))).ParamName.ShouldBe("originalRequests");
        (await Should.ThrowAsync<ArgumentException>(async () => await scope.ReserveBatchAsync(default))).ParamName.ShouldBe("originalRequests");
        var copiedInvalid = TestFactory.ReservationRequest(scope.Id) with
        {
            Amount = -1m
        };
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await scope.ReserveAsync(copiedInvalid))).ParamName.ShouldBe("originalRequests");
        ledger.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task RuntimeOperations_WhenSuccessful_ForwardCallerTokensAndDisposeUsesNone()
    {
        var ledger = new RecordingLedger();
        var reference = Scope();
        var request = TestFactory.ReservationRequest(reference.Id);
        var receipt = Receipt(reference, request);
        ledger.CreateResult = new BudgetLedgerScopeCreated(reference);
        ledger.ReserveResult = new BudgetLedgerBatchReserved([receipt]);
        ledger.StartResult = new BudgetStarted(receipt.Reservation.Id, false);
        ledger.CommitResult = new BudgetCommitResult(receipt.Reservation.Id, 1m, 1m, 0m, 0m);
        ledger.CorrectionResult = new BudgetCorrectionResult(receipt.Reservation.Id, 1m, 0m, 1);
        ledger.SnapshotResult = new BudgetSnapshot(reference.Id, DateTimeOffset.UnixEpoch, []);
        ledger.ReleaseResult = new BudgetLedgerReleased(receipt.Reservation);
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        using var create = new CancellationTokenSource();
        using var reserve = new CancellationTokenSource();
        using var start = new CancellationTokenSource();
        using var settle = new CancellationTokenSource();
        using var correct = new CancellationTokenSource();
        using var snapshot = new CancellationTokenSource();
        var scope = ((BudgetScopeCreated) await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), create.Token)).Scope;
        var reservation = ((BudgetReserved) await scope.ReserveAsync(request, reserve.Token)).Reservation;
        _ = await reservation.MarkStartedAsync(start.Token);
        _ = await reservation.CommitAsync(1m, settle.Token);
        _ = await reservation.CorrectAsync(1m, 1, correct.Token);
        _ = await scope.GetSnapshotAsync(snapshot.Token);
        await reservation.DisposeAsync();
        ledger.CreateToken.ShouldBe(create.Token);
        ledger.ReserveToken.ShouldBe(reserve.Token);
        ledger.StartToken.ShouldBe(start.Token);
        ledger.SettleToken.ShouldBe(settle.Token);
        ledger.CorrectToken.ShouldBe(correct.Token);
        ledger.SnapshotToken.ShouldBe(snapshot.Token);
        ledger.ReleaseToken.ShouldBe(CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeOperations_WhenLedgerAsynchronouslyCancels_PropagateCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var ledger = new RecordingLedger
        {
            Cancel = true
        };
        var reference = Scope();
        var receipt = Receipt(reference, TestFactory.ReservationRequest(reference.Id));
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        var scope = new BudgetScope(ledger, reference);
        var reservation = new BudgetReservation(ledger, receipt);
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.ReserveAsync(receipt.OriginalRequest, source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reservation.MarkStartedAsync(source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reservation.CommitAsync(0m, source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reservation.CorrectAsync(0m, 1, source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.GetSnapshotAsync(source.Token));
    }

    private static BudgetLedgerScopeReference Scope() => new(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
    private static BudgetLedgerReservationReceipt Receipt(BudgetLedgerScopeReference scope, BudgetReservationRequest request) => new(new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid())), request, new BudgetEffectiveReservation(DateTimeOffset.UtcNow.AddMinutes(1)));
    [Fact]
    public async Task CreateChildScopeAsync_WhenLedgerCreatesScope_ReturnsHandleAndCapturedPolicy()
    {
        var ledger = new RecordingBudgetLedger();
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
        var scope = ((BudgetScopeCreated) await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken)).Scope;
        var request = new BudgetReservationRequest(scope.Id, BudgetDimensions.InputTokens, 3m, new BudgetUnit("tokens"), new OperationId(Guid.NewGuid()), null, new IdempotencyKey(Guid.NewGuid().ToString()));
        var first = ((BudgetReserved) await scope.ReserveAsync(request, TestContext.Current.CancellationToken)).Reservation;
        var replay = ((BudgetReserved) await scope.ReserveAsync(request, TestContext.Current.CancellationToken)).Reservation;
        _ = await first.MarkStartedAsync(TestContext.Current.CancellationToken);
        await first.DisposeAsync();
        var snapshot = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        replay.Id.ShouldBe(first.Id);
        snapshot.Usages.Single(usage => usage.Dimension == request.Dimension).Reserved.ShouldBe(BudgetQuantity.FromDecimal(request.Amount));
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenLedgerRejects_ReturnsExactFailure()
    {
        var failure = new BudgetScopeCreationFailed(BudgetScopeCreationFailureKind.MaximumDepthExceeded, "depth exceeded");
        var ledger = new RecordingBudgetLedger
        {
            CreateResult = new BudgetLedgerScopeCreateRejected(failure)
        };
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken);
        result.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies the rejected outcome enriches a live sampled activity, not only the no-listener default path.</summary>
    [Fact]
    public async Task CreateChildScopeAsync_WhenLedgerRejectsWithAnActiveListener_MarksTheActivityAsRejected()
    {
        using var parent = new Activity("budget-scope-create-rejected-test").Start();
        var activities = new System.Collections.Concurrent.ConcurrentQueue<(string Name, ActivityStatusCode Status, string? Outcome)>();
        var parentSpanId = parent.SpanId;
        var traceId = parent.TraceId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == traceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parentSpanId && activity.TraceId == traceId && activity.OperationName == AgentKitActivityNames.BudgetScopeCreate)
                {
                    activities.Enqueue((activity.OperationName, activity.Status, activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString()));
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var failure = new BudgetScopeCreationFailed(BudgetScopeCreationFailureKind.MaximumDepthExceeded, "depth exceeded");
        var ledger = new RecordingBudgetLedger
        {
            CreateResult = new BudgetLedgerScopeCreateRejected(failure)
        };
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());

        var result = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(failure);
        activities.ShouldContain(entry => entry.Name == AgentKitActivityNames.BudgetScopeCreate && entry.Status == ActivityStatusCode.Error && entry.Outcome == "rejected");
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenLoggerFactoryThrows_ReturnsCommittedLedgerResult()
    {
        var request = TestFactory.ScopeRequest();
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), request.Address);
        var ledger = new RecordingBudgetLedger
        {
            CreateResult = new BudgetLedgerScopeCreated(reference)
        };
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions(), new ThrowingLoggerFactory());
        var result = await authority.CreateChildScopeAsync(request, TestContext.Current.CancellationToken);
        ((BudgetScopeCreated) result).Scope.Id.ShouldBe(reference.Id);
    }

    [Fact]
    public async Task SnapshotAndDispose_WhenCustomLedgerSelected_EmitRuntimeActivities()
    {
        using var parent = new Activity("budget-runtime-test").Start();
        var activities = new System.Collections.Concurrent.ConcurrentQueue<(string Name, ActivityStatusCode Status, string? ScopeId)>();
        var parentSpanId = parent.SpanId;
        var traceId = parent.TraceId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == traceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parentSpanId && activity.TraceId == traceId && activity.OperationName is AgentKitActivityNames.BudgetSnapshot or AgentKitActivityNames.BudgetRelease)
                {
                    activities.Enqueue((activity.OperationName, activity.Status, activity.GetTagItem(AgentKitTagNames.BudgetScopeId)?.ToString()));
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var measurements = new System.Collections.Concurrent.ConcurrentQueue<(string Outcome, bool HasDimension, bool HasScope)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, enabledListener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == AgentKitMetricNames.BudgetSnapshotCount)
                {
                    enabledListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var current = Activity.Current;
            if (current?.OperationName != AgentKitActivityNames.BudgetSnapshot || current.ParentSpanId != parentSpanId || current.TraceId != traceId)
            {
                return;
            }

            var values = tags.ToArray();
            measurements.Enqueue(((string) values.Single(tag => tag.Key == AgentKitTagNames.Outcome).Value!, values.Any(tag => tag.Key == AgentKitTagNames.BudgetDimension), values.Any(tag => tag.Key == AgentKitTagNames.BudgetScopeId)));
        });
        meterListener.Start();
        var ledger = new RecordingBudgetLedger();
        var scopeReference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scopeReference.Id);
        ledger.SnapshotResult = new BudgetSnapshot(scopeReference.Id, DateTimeOffset.UnixEpoch, [], []);
        var scopeLogger = new CapturingLogger<BudgetScope>();
        var reservationLogger = new CapturingLogger<BudgetReservation>();
        var scope = new BudgetScope(ledger, scopeReference, scopeLogger, reservationLogger);
        var reservation = new BudgetReservation(ledger, TestFactory.Receipt(scopeReference, request), reservationLogger);
        _ = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        ledger.ReleaseResult = new BudgetLedgerRetainedStarted(new BudgetLedgerReservationReference(scopeReference, reservation.Id));
        await reservation.DisposeAsync();
        activities.ShouldContain((AgentKitActivityNames.BudgetSnapshot, ActivityStatusCode.Ok, scopeReference.Id.ToString()));
        activities.ShouldContain((AgentKitActivityNames.BudgetRelease, ActivityStatusCode.Ok, scopeReference.Id.ToString()));
        measurements.ShouldContain(("read", false, false));
        scopeLogger.Events.ShouldContain(entry => entry.EventId == 7060 && entry.Level == LogLevel.Debug);
        scopeLogger.Events.Where(static entry => entry.EventId == 7060).SelectMany(static entry => entry.Keys).ShouldNotContain(AgentKitTagNames.BudgetDimension);
        reservationLogger.Events.ShouldContain(entry => entry.EventId == 7020);
    }



    private sealed class ThrowingLoggerFactory: ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) => throw new InvalidOperationException("observer failed");
        public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException("observer failed");
        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger<T>: ILogger<T>
    {
        public List<(int EventId, LogLevel Level, string[] Keys)> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var keys = state is IEnumerable<KeyValuePair<string, object?>> values ? values.Select(static value => value.Key).ToArray() : [];
            Events.Add((eventId.Id, logLevel, keys));
        }
    }

    [Fact]
    public void Constructors_WhenRequiredDependencyIsNull_ThrowExactParameterName()
    {
        var ledger = new RecordingLedger();
        Should.Throw<ArgumentNullException>(() => new BudgetAuthority(null!, TestFactory.DefaultOptions())).ParamName.ShouldBe("ledger");
        Should.Throw<ArgumentNullException>(() => new BudgetAuthority(ledger, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task CreateChildScopeAsync_WhenLedgerThrowsNonCancellationException_LogsFailureAndRethrows()
    {
        var failure = new InvalidOperationException("ledger unavailable");
        var ledger = new RecordingBudgetLedger
        {
            CreateException = failure
        };
        var logger = new CapturingLogger<BudgetAuthority>();
        var loggerFactory = new CapturingLoggerFactory(logger);
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions(), loggerFactory);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken));
        exception.ShouldBeSameAs(failure);
        logger.Events.ShouldContain(entry => entry.EventId == 7003);
    }

    private sealed class CapturingLoggerFactory(ILogger logger): ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }

    /// <summary>Verifies every successful and cancelled outcome emits its bounded log event when logging is enabled.</summary>
    [Fact]
    public async Task RuntimeOperations_WhenLoggingEnabled_EmitBoundedSuccessAndCancellationLogs()
    {
        var rawLogger = new CapturingRawLogger();
        var loggerFactory = new CapturingLoggerFactory(rawLogger);
        var ledger = new RecordingBudgetLedger();
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions(), loggerFactory);
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(reference.Id);
        var receipt = TestFactory.Receipt(reference, request);
        ledger.CreateResult = new BudgetLedgerScopeCreated(reference);
        var scope = ((BudgetScopeCreated) await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken)).Scope;
        rawLogger.Events.ShouldContain(entry => entry == 7000);
        ledger.CreateResult = new BudgetLedgerScopeCreateRejected(new BudgetScopeCreationFailed(BudgetScopeCreationFailureKind.MaximumDepthExceeded, "depth"));
        _ = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), TestContext.Current.CancellationToken);
        rawLogger.Events.ShouldContain(entry => entry == 7001);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            ledger.CreateException = new OperationCanceledException(cancelled.Token);
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), cancelled.Token));
        }
        rawLogger.Events.ShouldContain(entry => entry == 7002);
        ledger.ReserveResult = new BudgetLedgerBatchReserved([receipt]);
        var reservation = ((BudgetReserved) await scope.ReserveAsync(request, TestContext.Current.CancellationToken)).Reservation;
        rawLogger.Events.ShouldContain(entry => entry == 7010);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            ledger.ReserveException = new OperationCanceledException(cancelled.Token);
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.ReserveAsync(request, cancelled.Token));
        }
        rawLogger.Events.ShouldContain(entry => entry == 7011);
        ledger.StartResult = new BudgetStarted(receipt.Reservation.Id, false);
        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        rawLogger.Events.ShouldContain(entry => entry == 7030);
        ledger.CorrectionResult = new BudgetCorrectionResult(receipt.Reservation.Id, 1m, 1m, 1);
        _ = await reservation.CorrectAsync(1m, 1, TestContext.Current.CancellationToken);
        rawLogger.Events.ShouldContain(entry => entry == 7040);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            ledger.SnapshotException = new OperationCanceledException(cancelled.Token);
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.GetSnapshotAsync(cancelled.Token));
        }
        rawLogger.Events.ShouldContain(entry => entry == 7062);
    }

    private sealed class CapturingRawLogger: ILogger
    {
        public List<int> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Events.Add(eventId.Id);
    }
}
