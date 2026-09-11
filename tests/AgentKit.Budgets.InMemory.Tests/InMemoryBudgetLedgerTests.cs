// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;



/// <summary>Verifies InMemoryBudgetLedger behavior and contracts.</summary>
public sealed class InMemoryBudgetLedgerTests: BudgetLedgerConformanceTests<InMemoryBudgetLedgerConformanceFixture>
{
    /// <summary>Verifies every required constructor collaborator reports its exact parameter name before assignment.</summary>
    [Fact]
    public void Constructor_WhenRequiredArgumentIsNull_ThrowsWithExactParameterName()
    {
        var clock = TimeProvider.System;
        var scopeIds = new ScopeIdGenerator();
        var reservationIds = new ReservationIdGenerator();
        var dimensions = new EmptyDimensionCatalog();
        Should.Throw<ArgumentNullException>(() => new InMemoryBudgetLedger(null!, scopeIds, reservationIds, dimensions)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new InMemoryBudgetLedger(clock, null!, reservationIds, dimensions)).ParamName.ShouldBe("scopeIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryBudgetLedger(clock, scopeIds, null!, dimensions)).ParamName.ShouldBe("reservationIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryBudgetLedger(clock, scopeIds, reservationIds, null!)).ParamName.ShouldBe("dimensions");
    }

    /// <summary>Verifies null logging is the documented disabled-observer selection.</summary>
    [Fact]
    public void Constructor_WhenLoggerIsNull_AcceptsDisabledLogging()
    {
        var ledger = new InMemoryBudgetLedger(TimeProvider.System, new ScopeIdGenerator(), new ReservationIdGenerator(), new EmptyDimensionCatalog(), null);
        _ = ledger.ShouldNotBeNull();
    }

    /// <summary>Verifies the leaf truthfully declares stable process-local ephemeral coordination.</summary>
    [Fact]
    public void Descriptor_WhenRead_DeclaresStableProcessLocalEphemeralCapabilities()
    {
        var ledger = new InMemoryBudgetLedger(TimeProvider.System, new ScopeIdGenerator(), new ReservationIdGenerator(), new EmptyDimensionCatalog());
        var first = ledger.Descriptor;
        var second = ledger.Descriptor;
        first.ShouldBe(new BudgetLedgerDescriptor(false, BudgetLedgerConcurrencyDomain.ProcessLocal));
        second.ShouldBe(first);
    }

    private sealed class ScopeIdGenerator: IIdentifierGenerator<BudgetScopeId>
    {
        public BudgetScopeId Create() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    }

    private sealed class ReservationIdGenerator: IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    }

    private sealed class EmptyDimensionCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }
    }

    /// <summary>Verifies every public ledger operation emits one successful activity, log, count, and duration.</summary>
    [Fact]
    public async Task Operations_WhenSuccessful_EmitBoundedTerminalSignals()
    {
        var activities = new List<Activity>();
        var measurements = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var parent = StartParent("budget-ledger-success");
        using var activityListener = ListenToActivities(activities, parent.TraceId);
        using var meterListener = ListenToMetrics(measurements, parent.TraceId);
        var logger = new RecordingLogger();
        var fixture = new InMemoryBudgetLedgerConformanceFixture();
        var ledger = fixture.CreateLedger(logger);
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = await CreateScopeAsync(ledger, "diagnostics-scope");
        var settled = await ReserveAsync(ledger, scope, "diagnostics-settled");
        _ = await ledger.MarkStartedAsync(settled, cancellationToken);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(settled, 1), cancellationToken);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(settled, 1, 1), cancellationToken);
        var released = await ReserveAsync(ledger, scope, "diagnostics-released");
        _ = await ledger.ReleaseUnstartedAsync(released, cancellationToken);
        _ = await ledger.GetSnapshotAsync(scope, cancellationToken);
        _ = await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 1, null), cancellationToken);
        var reconciled = await ReserveAsync(ledger, scope, "diagnostics-reconciled");
        _ = await ledger.MarkStartedAsync(reconciled, cancellationToken);
        _ = await ledger.ReconcileAsync(new BudgetLedgerReconciliationRequest(reconciled, new BudgetNoUsageProven(), new IdempotencyKey("diagnostics-proof")), cancellationToken);
        var operations = activities.Where(activity => activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation).ToArray();
        operations.Length.ShouldBe(12);
        operations.ShouldAllBe(activity => activity.Status == ActivityStatusCode.Ok);
        operations.ShouldAllBe(activity => activity.ParentSpanId == parent.SpanId);
        operations.Select(activity => activity.GetTagItem(AgentKitTagNames.BudgetOperation)?.ToString()).ShouldAllBe(value => !string.IsNullOrWhiteSpace(value));
        var create = operations.Single(activity => Equals(activity.GetTagItem(AgentKitTagNames.BudgetOperation), "create_scope"));
        create.GetTagItem(AgentKitTagNames.BudgetScopeId).ShouldBe(scope.Id.ToString());
        create.GetTagItem(AgentKitTagNames.TenantId).ShouldBe("tenant");
        create.GetTagItem(AgentKitTagNames.AgentId).ShouldBe("10000000-0000-0000-0000-000000000001");
        create.GetTagItem(AgentKitTagNames.SessionId).ShouldBeNull();
        _ = operations.First(activity => Equals(activity.GetTagItem(AgentKitTagNames.BudgetOperation), "reserve_batch")).GetTagItem(AgentKitTagNames.OperationId).ShouldNotBeNull();
        _ = operations.Single(activity => Equals(activity.GetTagItem(AgentKitTagNames.BudgetOperation), "settle")).GetTagItem(AgentKitTagNames.BudgetReservationId).ShouldNotBeNull();
        logger.Entries.Count(entry => entry.EventId.Id == 7050).ShouldBe(12);
        var createLog = logger.Entries.Single(entry => entry.Tags.TryGetValue("BudgetOperation", out var value) && Equals(value, "create_scope"));
        createLog.Tags["BudgetScopeId"].ShouldBe(scope.Id.ToString());
        createLog.Tags["TenantId"].ShouldBe("tenant");
        createLog.Tags["SessionId"].ShouldBeNull();
        measurements.Count(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationCount).ShouldBe(12);
        measurements.Count(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationDuration).ShouldBe(12);
        measurements.SelectMany(item => item.Tags.Keys).Distinct().Order().ShouldBe(new[] { AgentKitTagNames.BudgetOperation, AgentKitTagNames.Outcome }.Order());
        logger.Entries.ShouldAllBe(entry => !entry.Message.Contains("diagnostics-scope", StringComparison.Ordinal));
    }

    /// <summary>Verifies successful creation correlates every hierarchy identity available in its validated address.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenAddressHasSessionAndRun_EmitsApplicableCorrelation()
    {
        var activities = new List<Activity>();
        using var parent = StartParent("budget-ledger-hierarchy-correlation");
        using var listener = ListenToActivities(activities, parent.TraceId);
        var logger = new RecordingLogger();
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger(logger);
        var sessionId = new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), sessionId, runId, null);
        var created = (await ledger.CreateScopeAsync(ScopeRequest("hierarchy-correlation", address), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>();
        var activity = activities.Single(item => item.OperationName == AgentKitActivityNames.BudgetLedgerOperation);
        activity.GetTagItem(AgentKitTagNames.BudgetScopeId).ShouldBe(created.Scope.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(sessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(runId.ToString());
        var entry = logger.Entries.Single();
        entry.Tags["BudgetScopeId"].ShouldBe(created.Scope.Id.ToString());
        entry.Tags["SessionId"].ShouldBe(sessionId.ToString());
        entry.Tags["RunId"].ShouldBe(runId.ToString());
    }

    /// <summary>Verifies invalid input emits nothing while cancellation and faults emit truthful error terminals.</summary>
    [Fact]
    public async Task Operations_WhenInvalidCancelledOrFaulted_RespectDiagnosticBoundary()
    {
        var activities = new List<Activity>();
        using var parent = StartParent("budget-ledger-errors");
        using var listener = ListenToActivities(activities, parent.TraceId);
        var logger = new RecordingLogger();
        var fixture = new InMemoryBudgetLedgerConformanceFixture();
        var ledger = fixture.CreateLedger(logger);
        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await ledger.GetSnapshotAsync(null!, TestContext.Current.CancellationToken));
        activities.ShouldBeEmpty();
        logger.Entries.ShouldBeEmpty();
        using var source = new CancellationTokenSource();
        source.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.GetSnapshotAsync(new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address()), source.Token));
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.GetSnapshotAsync(new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address()), TestContext.Current.CancellationToken));
        activities.Count.ShouldBe(2);
        activities.ShouldAllBe(activity => activity.Status == ActivityStatusCode.Error);
        activities.Select(activity => activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString()).ShouldBe(["cancelled", "faulted"]);
        activities.ShouldAllBe(activity => HasTag(activity, AgentKitTagNames.ErrorType));
        activities.ShouldAllBe(activity => HasTag(activity, AgentKitTagNames.BudgetScopeId));
        logger.Entries.Select(entry => entry.EventId.Id).ShouldBe([7050, 7051]);
    }

    /// <summary>Verifies reservation expiry is a typed terminal outcome without fabricated limit-failure diagnostics.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenReservationExpired_EmitsExpiredOutcomeWithoutLimitFailure()
    {
        var activities = new List<Activity>();
        using var parent = StartParent("budget-ledger-expired");
        using var listener = ListenToActivities(activities, parent.TraceId);
        var logger = new RecordingLogger();
        var fixture = new InMemoryBudgetLedgerConformanceFixture();
        var ledger = fixture.CreateLedger(logger);
        var scope = await CreateScopeAsync(ledger, "diagnostics-expired");
        var reservation = await ReserveAsync(ledger, scope, "diagnostics-expired-reservation");
        fixture.Advance(TimeSpan.FromMinutes(10));
        _ = (await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetStartExpired>();
        var activity = activities.Single(item => item.OperationName == AgentKitActivityNames.BudgetLedgerOperation && Equals(item.GetTagItem(AgentKitTagNames.BudgetOperation), "mark_started"));
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("expired");
        activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBeNull();
        var entry = logger.Entries.Single(item => item.Tags.TryGetValue("BudgetOperation", out var operation) && Equals(operation, "mark_started"));
        entry.EventId.Id.ShouldBe(7050);
        entry.Tags["Outcome"].ShouldBe("expired");
        entry.Tags.ContainsKey("ErrorType").ShouldBeFalse();
    }

    /// <summary>Verifies audited overrun resolution emits one truthful correlated terminal across every diagnostic surface.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenSuccessful_EmitsResolvedTerminalSignals()
    {
        var activities = new List<Activity>();
        var measurements = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var parent = StartParent("budget-ledger-overrun-resolution");
        using var activityListener = ListenToActivities(activities, parent.TraceId);
        using var meterListener = ListenToMetrics(measurements, parent.TraceId);
        var logger = new RecordingLogger();
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger(logger);
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = (await ledger.CreateScopeAsync(ScopeRequest("diagnostics-overrun", admission: new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution)), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var reservation = await ReserveAsync(ledger, scope, "diagnostics-overrun-row");
        _ = await ledger.MarkStartedAsync(reservation, cancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), cancellationToken);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1), cancellationToken);
        var request = ResolutionRequest(commit.CreatedOverrunHolds.Single().Reference);
        _ = (await ledger.ResolveOverrunHoldAsync(request, cancellationToken)).ShouldBeOfType<BudgetOverrunHoldResolved>();
        var activity = activities.Single(item => item.OperationName == AgentKitActivityNames.BudgetLedgerOperation && Equals(item.GetTagItem(AgentKitTagNames.BudgetOperation), "resolve_overrun_hold"));
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("resolved");
        activity.GetTagItem(AgentKitTagNames.BudgetScopeId).ShouldBe(scope.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.BudgetReservationId).ShouldBe(reservation.Id.ToString());
        var entry = logger.Entries.Single(item => item.Tags.TryGetValue("BudgetOperation", out var value) && Equals(value, "resolve_overrun_hold"));
        entry.EventId.Id.ShouldBe(7050);
        entry.Tags["Outcome"].ShouldBe("resolved");
        measurements.Count(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationCount && Equals(item.Tags[AgentKitTagNames.BudgetOperation], "resolve_overrun_hold")).ShouldBe(1);
        measurements.Count(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationDuration && Equals(item.Tags[AgentKitTagNames.BudgetOperation], "resolve_overrun_hold")).ShouldBe(1);
    }

    /// <summary>Verifies held, invalid-binding, and cancellation resolution paths emit truthful content-free terminals.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenNotResolved_EmitsTruthfulSafeTerminalSignals()
    {
        var activities = new List<Activity>();
        var measurements = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var parent = StartParent("budget-ledger-overrun-not-resolved");
        using var activityListener = ListenToActivities(activities, parent.TraceId);
        using var meterListener = ListenToMetrics(measurements, parent.TraceId);
        var logger = new RecordingLogger();
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger(logger);
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = (await ledger.CreateScopeAsync(ScopeRequest("diagnostics-overrun-held", admission: new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution)), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var reservation = await ReserveAsync(ledger, scope, "diagnostics-overrun-held-row");
        _ = await ledger.MarkStartedAsync(reservation, cancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), cancellationToken);
        var valid = ResolutionRequest(commit.CreatedOverrunHolds.Single().Reference);
        _ = (await ledger.ResolveOverrunHoldAsync(valid, cancellationToken)).ShouldBeOfType<BudgetOverrunHoldResolutionBlocked>();
        var invalid = CorruptResolutionRequest(valid);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ResolveOverrunHoldAsync(invalid, cancellationToken));
        using var source = new CancellationTokenSource();
        source.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.ResolveOverrunHoldAsync(new BudgetOverrunHoldResolutionRequest(valid.Hold, valid.EnforcementReceipt, new IdempotencyKey("diagnostics-overrun-cancelled")), source.Token));
        var resolutionActivities = activities.Where(item => item.OperationName == AgentKitActivityNames.BudgetLedgerOperation && Equals(item.GetTagItem(AgentKitTagNames.BudgetOperation), "resolve_overrun_hold")).ToArray();
        resolutionActivities.Select(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString()).ShouldBe(["held", "faulted", "cancelled"]);
        resolutionActivities.ShouldAllBe(item => item.Status == ActivityStatusCode.Error);
        resolutionActivities[0].GetTagItem(AgentKitTagNames.ErrorType).ShouldBeNull();
        resolutionActivities[1].GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(typeof(BudgetLedgerStateException).FullName);
        resolutionActivities[2].GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(OperationCanceledException));
        logger.Entries.Where(item => item.Tags.TryGetValue("BudgetOperation", out var value) && Equals(value, "resolve_overrun_hold")).Select(item => item.EventId.Id).ShouldBe([7050, 7051, 7050]);
        measurements.Where(item => Equals(item.Tags[AgentKitTagNames.BudgetOperation], "resolve_overrun_hold")).SelectMany(item => item.Tags.Keys).Distinct().Order().ShouldBe(new[] { AgentKitTagNames.BudgetOperation, AgentKitTagNames.Outcome }.Order());
        logger.Entries.ShouldAllBe(item => !item.Message.Contains("budget-overrun:", StringComparison.Ordinal) && !item.Message.Contains("sha256:", StringComparison.Ordinal));
        foreach (var activity in resolutionActivities)
        {
            foreach (var tag in activity.Tags)
            {
                (tag.Value?.Contains("sha256:", StringComparison.Ordinal) ?? false).ShouldBeFalse();
            }
        }
    }

    /// <summary>Verifies missing injected timing evidence omits duration rather than fabricating zero while preserving the count.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenTimestampFails_RecordsCountWithoutDuration()
    {
        var measurements = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var parent = StartParent("budget-ledger-missing-timing");
        using var listener = ListenToMetrics(measurements, parent.TraceId);
        var fixture = new InMemoryBudgetLedgerConformanceFixture();
        var ledger = fixture.CreateLedger();
        fixture.ArmTimestampFailure();
        _ = await ledger.CreateScopeAsync(ScopeRequest("missing-timing"), TestContext.Current.CancellationToken);
        measurements.Count(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationCount).ShouldBe(1);
        measurements.ShouldNotContain(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationDuration);
    }

    /// <summary>Verifies a backward injected timestamp omits duration while preserving the semantic result and terminal count.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenElapsedTimeIsNegative_RecordsCountWithoutDuration()
    {
        var measurements = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var parent = StartParent("budget-ledger-backward-timing");
        using var listener = ListenToMetrics(measurements, parent.TraceId);
        var fixture = new InMemoryBudgetLedgerConformanceFixture();
        var ledger = fixture.CreateLedger();
        fixture.ArmNegativeElapsedTime();
        var result = await ledger.CreateScopeAsync(ScopeRequest("backward-timing"), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        measurements.Count(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationCount).ShouldBe(1);
        measurements.ShouldNotContain(item => item.Name == AgentKitMetricNames.BudgetLedgerOperationDuration);
    }

    /// <summary>Verifies throwing logging, activity, and metric observers cannot change a committed semantic result.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenObserversThrow_StillCommitsAndReplays()
    {
        using var parent = StartParent("budget-ledger-throwing-observers");
        var traceId = parent.TraceId;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == traceId)
                {
                    throw new InvalidOperationException("Injected activity observer failure.");
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (IsLedgerInstrument(instrument))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => ThrowForTrace(traceId));
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => ThrowForTrace(traceId));
        meterListener.Start();
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger(new ThrowingLogger());
        var request = ScopeRequest("observer-failure");
        var created = await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken);
        var replay = await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken);
        replay.ShouldBe(created);
    }

    private static ActivityListener ListenToActivities(List<Activity> activities, ActivityTraceId traceId)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == traceId)
                {
                    activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener ListenToMetrics(List<(string Name, IReadOnlyDictionary<string, object?> Tags)> measurements, ActivityTraceId traceId)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (IsLedgerInstrument(instrument))
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => RecordForTrace(traceId, measurements, instrument, tags));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => RecordForTrace(traceId, measurements, instrument, tags));
        listener.Start();
        return listener;
    }

    private static Activity StartParent(string name) => new Activity(name).SetIdFormat(ActivityIdFormat.W3C).Start();
    private static bool IsLedgerInstrument(Instrument instrument) => instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.BudgetLedgerOperationCount or AgentKitMetricNames.BudgetLedgerOperationDuration;
    private static void RecordForTrace(ActivityTraceId traceId, List<(string Name, IReadOnlyDictionary<string, object?> Tags)> measurements, Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (Activity.Current?.TraceId == traceId)
        {
            measurements.Add((instrument.Name, tags.ToArray().ToDictionary(item => item.Key, item => item.Value)));
        }
    }

    private static void ThrowForTrace(ActivityTraceId traceId)
    {
        if (Activity.Current?.TraceId == traceId)
        {
            throw new InvalidOperationException("Injected metric observer failure.");
        }
    }

    private static async Task<BudgetLedgerScopeReference> CreateScopeAsync(IBudgetLedger ledger, string key) => (await ledger.CreateScopeAsync(ScopeRequest(key), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
    private static BudgetLedgerScopeCreateRequest ScopeRequest(string key, BudgetScopeAddress? address = null, BudgetScopeAdmission? admission = null) => new(new BudgetScopeRequest(null, address ?? Address(), [new BudgetLimit(new BudgetDimension("test.sum"), 100, new BudgetUnit("count"), BudgetLimitKind.Hard)], new IdempotencyKey(key)), admission ?? new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
    private static async Task<BudgetLedgerReservationReference> ReserveAsync(IBudgetLedger ledger, BudgetLedgerScopeReference scope, string key) => (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey(key))]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
    private static BudgetScopeAddress Address() => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
    private static BudgetOverrunHoldResolutionRequest ResolutionRequest(BudgetOverrunHoldReference hold)
    {
        var enforcement = new SecurityEnforcementRequest(new SecurityAuthorizationScope(Address().AgentId, null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null)), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("operator"), ExecutionSubjectKind.Human), new ComponentId("budget-operator"), SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [BudgetOverrunSecurityBinding.Resource(hold)], BudgetOverrunSecurityBinding.Fingerprint(hold), new SecurityRevocationVersion(1));
        var receipt = new SecurityEnforcementIntentReceipt(new SecurityEnforcementIntentId(Guid.Parse("40000000-0000-0000-0000-000000000004")), new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")), enforcement, null, new ContentHash("sha256:overrun-resolution"), DateTimeOffset.UnixEpoch);
        return new BudgetOverrunHoldResolutionRequest(hold, receipt, new IdempotencyKey("diagnostics-overrun-resolution"));
    }

    private static BudgetOverrunHoldResolutionRequest CorruptResolutionRequest(BudgetOverrunHoldResolutionRequest request)
    {
        var receipt = request.EnforcementReceipt;
        var enforcement = receipt.Enforcement with
        {
            InputFingerprint = new InputFingerprint("sha256:wrong-target")
        };
        var corrupted = new SecurityEnforcementIntentReceipt(receipt.IntentId, receipt.GrantId, receipt.RequestId, enforcement, receipt.RequiredFence, receipt.EffectFingerprint, receipt.ConsumedAt);
        return new BudgetOverrunHoldResolutionRequest(request.Hold, corrupted, new IdempotencyKey("diagnostics-overrun-invalid"));
    }

    private static bool HasTag(Activity activity, string name) => !string.IsNullOrWhiteSpace(activity.GetTagItem(name)?.ToString());
    private sealed class RecordingLogger: ILogger<InMemoryBudgetLedger>
    {
        internal List<(EventId EventId, string Message, IReadOnlyDictionary<string, object?> Tags)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var tags = state is IEnumerable<KeyValuePair<string, object?>> values ? values.Where(item => item.Key != "{OriginalFormat}").ToDictionary(item => item.Key, item => item.Value) : [];
            Entries.Add((eventId, formatter(state, exception), tags));
        }
    }

    private sealed class ThrowingLogger: ILogger<InMemoryBudgetLedger>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => throw new InvalidOperationException();
        public bool IsEnabled(LogLevel logLevel) => throw new InvalidOperationException("Injected logger failure.");
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException();
    }
}
