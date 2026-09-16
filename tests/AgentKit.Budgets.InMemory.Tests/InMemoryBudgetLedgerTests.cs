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

    /// <summary>Verifies scope creation rejects a nonexistent parent scope reference.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenParentScopeIsUnavailable_ThrowsReferenceUnavailable()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var missingParent = new BudgetScopeId(Guid.NewGuid());
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(missingParent, Address(), [], new IdempotencyKey("missing-parent")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a child scope exceeding the captured maximum depth is rejected without persisting.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenDepthExceedsCapturedMaximum_ReturnsMaximumDepthRejection()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var shallowAdmission = new BudgetScopeAdmission(1, 32, TimeSpan.FromMinutes(5));
        var root = (await ledger.CreateScopeAsync(ScopeRequest("depth-root", admission: shallowAdmission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var childRequest = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(root.Id, Address(), [], new IdempotencyKey("depth-child")), shallowAdmission);
        var rejected = (await ledger.CreateScopeAsync(childRequest, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreateRejected>();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.MaximumDepthExceeded);
    }

    /// <summary>Verifies a scope limit for an unregistered dimension is rejected without persisting.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenLimitDimensionIsUnregistered_ReturnsInvalidLimitRejection()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, Address(), [new BudgetLimit(new BudgetDimension("test.unregistered"), 10, new BudgetUnit("count"), BudgetLimitKind.Hard)], new IdempotencyKey("invalid-limit-dimension")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var rejected = (await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreateRejected>();
        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
    }

    /// <summary>Verifies concurrent atomic batches bound to disjoint idempotency keys cannot be replayed as one batch.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenItemKeysBelongToDifferentBatches_ThrowsMutationConflict()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "batch-conflict-scope");
        var firstItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("batch-conflict-a"));
        var secondItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("batch-conflict-b"));
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [firstItem]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [secondItem]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        var mixedItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("batch-conflict-a"));
        var otherMixedItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("batch-conflict-b"));
        _ = await Should.ThrowAsync<BudgetLedgerMutationConflictException>(async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [mixedItem, otherMixedItem]), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a dimension descriptor with an undefined aggregation kind is rejected before accounting.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenDescriptorAggregationIsUndefined_ThrowsBudgetLedgerStateException()
    {
        var catalog = new UndefinedAggregationCatalog();
        var ledger = new InMemoryBudgetLedger(TimeProvider.System, new SequentialIdGenerator<BudgetScopeId>(id => new(id)), new SequentialIdGenerator<BudgetReservationId>(id => new(id)), catalog);
        var scope = (await ledger.CreateScopeAsync(new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(null, Address(), [], new IdempotencyKey("undefined-aggregation-scope")), new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5))), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.undefined"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("undefined-aggregation-item"));
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("undefined aggregation");
    }

    /// <summary>Verifies a reservation unit that conflicts with a captured scope limit's unit is rejected.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenReservationUnitConflictsWithScopeLimit_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, Address(), [new BudgetLimit(new BudgetDimension("test.multi-unit"), 100, new BudgetUnit("count"), BudgetLimitKind.Hard)], new IdempotencyKey("limit-unit-conflict-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var scope = (await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.multi-unit"), 1, new BudgetUnit("bytes"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("limit-unit-conflict-item"));
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("captured scope limit");
    }

    /// <summary>Verifies a reservation identity source that produces a duplicate value is rejected.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenReservationIdentitySourceProducesDuplicate_ThrowsBudgetLedgerStateException()
    {
        var duplicateId = new BudgetReservationId(Guid.Parse("70000000-0000-0000-0000-000000000001"));
        var ledger = new InMemoryBudgetLedger(TimeProvider.System, new SequentialIdGenerator<BudgetScopeId>(id => new(id)), new ConstantIdGenerator<BudgetReservationId>(duplicateId), new InMemoryBudgetLedgerConformanceFixtureCatalog());
        var scope = (await ledger.CreateScopeAsync(new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(null, Address(), [], new IdempotencyKey("duplicate-reservation-scope")), new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5))), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var firstItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("duplicate-reservation-first"));
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [firstItem]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        var secondItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.sum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("duplicate-reservation-second"));
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [secondItem]), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("duplicate value");
    }

    /// <summary>Verifies unstarted expired reservations are cleaned up while admitting a fresh atomic batch.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenPriorReservationExpired_CleansUpExpiredCapacityWhileAdmittingNewBatch()
    {
        var fixture = new InMemoryBudgetLedgerConformanceFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "expire-cleanup-scope");
        _ = await ReserveAsync(ledger, scope, "expire-cleanup-first");
        fixture.Advance(TimeSpan.FromMinutes(10));
        var second = await ReserveAsync(ledger, scope, "expire-cleanup-second");
        var snapshot = await ledger.GetSnapshotAsync(scope, TestContext.Current.CancellationToken);
        snapshot.Usages.Single(usage => usage.Dimension == new BudgetDimension("test.sum")).Reserved.ShouldBe(BudgetQuantity.FromDecimal(1));
        second.Id.ShouldNotBe(default);
    }

    /// <summary>Verifies starting an already-started reservation replays truthfully instead of double-counting.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenAlreadyStarted_ReturnsAlreadyStartedReplay()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "already-started-scope");
        var reservation = await ReserveAsync(ledger, scope, "already-started-reservation");
        _ = (await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetStarted>();
        var second = (await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetStarted>();
        second.WasAlreadyStarted.ShouldBeTrue();
    }

    /// <summary>Verifies releasing a settled reservation reports the terminal settlement instead of releasing capacity.</summary>
    [Fact]
    public async Task ReleaseUnstartedAsync_WhenReservationAlreadySettled_ReturnsAlreadySettled()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "already-settled-scope");
        var reservation = await ReserveAsync(ledger, scope, "already-settled-reservation");
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), TestContext.Current.CancellationToken);
        var release = (await ledger.ReleaseUnstartedAsync(reservation, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerAlreadySettled>();
        release.Commit.ShouldBe(commit);
    }

    /// <summary>Verifies releasing an already-released reservation is an idempotent no-op.</summary>
    [Fact]
    public async Task ReleaseUnstartedAsync_WhenCalledTwice_IsIdempotent()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "double-release-scope");
        var reservation = await ReserveAsync(ledger, scope, "double-release-reservation");
        _ = (await ledger.ReleaseUnstartedAsync(reservation, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerReleased>();
        _ = (await ledger.ReleaseUnstartedAsync(reservation, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerReleased>();
    }

    /// <summary>Verifies settlement of a reservation that never started is rejected.</summary>
    [Fact]
    public async Task SettleAsync_WhenReservationNeverStarted_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "unstarted-settle-scope");
        var reservation = await ReserveAsync(ledger, scope, "unstarted-settle-reservation");
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("not started or is released");
    }

    /// <summary>Verifies correction before settlement is rejected.</summary>
    [Fact]
    public async Task CorrectAsync_WhenReservationNotYetSettled_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "unsettled-correct-scope");
        var reservation = await ReserveAsync(ledger, scope, "unsettled-correct-reservation");
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Only settled accounting");
    }

    /// <summary>Verifies a correction revision that does not increase monotonically is rejected.</summary>
    [Fact]
    public async Task CorrectAsync_WhenRevisionDoesNotIncreaseMonotonically_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "non-monotonic-scope");
        var reservation = await ReserveAsync(ledger, scope, "non-monotonic-reservation");
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), TestContext.Current.CancellationToken);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 5), TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 3), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("monotonically");
    }

    /// <summary>Verifies a page size above the captured finite ledger bound is rejected.</summary>
    [Fact]
    public async Task ReadUnresolvedStartedAsync_WhenPageSizeExceedsCapturedBound_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "page-size-scope");
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 33, null), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("page size");
    }

    /// <summary>Verifies reconciliation of an unstarted reservation is rejected.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenReservationNeverStarted_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "unstarted-reconcile-scope");
        var reservation = await ReserveAsync(ledger, scope, "unstarted-reconcile-reservation");
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReconcileAsync(new BudgetLedgerReconciliationRequest(reservation, new BudgetStillUnknown(), new IdempotencyKey("unstarted-reconcile-key")), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("started reservation");
    }

    /// <summary>Verifies estimated reconciliation evidence settles the reservation using the estimated actual.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenEvidenceIsEstimated_SettlesUsingEstimatedActual()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "estimated-reconcile-scope");
        var reservation = await ReserveAsync(ledger, scope, "estimated-reconcile-reservation");
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var result = (await ledger.ReconcileAsync(new BudgetLedgerReconciliationRequest(reservation, new BudgetActualEstimated(1), new IdempotencyKey("estimated-reconcile-key")), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerReconciliationSettled>();
        result.Commit.Actual.ShouldBe(1);
    }

    /// <summary>Verifies an overrun hold reference whose boundary is not part of the reservation's real lineage is rejected.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenBoundaryIsNotInReservationLineage_ThrowsReferenceUnavailable()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scopeA = await CreateScopeAsync(ledger, "lineage-scope-a");
        var scopeB = await CreateScopeAsync(ledger, "lineage-scope-b");
        var reservation = await ReserveAsync(ledger, scopeA, "lineage-reservation");
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), TestContext.Current.CancellationToken);
        var realHold = commit.CreatedOverrunHolds.Single().Reference;
        var unrelatedHold = new BudgetOverrunHoldReference(scopeB, reservation, realHold.TriggeringRevision);
        var request = ResolutionRequest(unrelatedHold);
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.ResolveOverrunHoldAsync(request, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies operator resolution is rejected for a hold owned by a boundary that did not capture the authorized-resolution policy.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenPolicyDoesNotAcceptOperatorResolution_ThrowsBudgetLedgerStateException()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "wrong-policy-scope");
        var reservation = await ReserveAsync(ledger, scope, "wrong-policy-reservation");
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), TestContext.Current.CancellationToken);
        var request = ResolutionRequest(commit.CreatedOverrunHolds.Single().Reference);
        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ResolveOverrunHoldAsync(request, TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("cannot accept operator resolution");
    }

    /// <summary>Verifies overrun resolution finds no hard-limit blockers for a dimension with no captured hard limit.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenDimensionHasNoHardLimit_FindsNoHardFailures()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, Address(), [], new IdempotencyKey("no-hard-limit-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution));
        var scope = (await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.unlimited"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("no-hard-limit-item"));
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), TestContext.Current.CancellationToken);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1), TestContext.Current.CancellationToken);
        var resolutionRequest = ResolutionRequest(commit.CreatedOverrunHolds.Single().Reference);
        _ = (await ledger.ResolveOverrunHoldAsync(resolutionRequest, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetOverrunHoldResolved>();
    }

    /// <summary>Verifies overrun resolution correctly aggregates a maximum-aggregation dimension against its hard limit.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenDimensionUsesMaximumAggregation_EvaluatesHardFailureWithMaximum()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, Address(), [new BudgetLimit(new BudgetDimension("test.maximum"), 1, new BudgetUnit("count"), BudgetLimitKind.Hard)], new IdempotencyKey("maximum-agg-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution));
        var scope = (await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.maximum"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("maximum-agg-item"));
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), TestContext.Current.CancellationToken);
        var resolutionRequest = ResolutionRequest(commit.CreatedOverrunHolds.Single().Reference);
        var result = (await ledger.ResolveOverrunHoldAsync(resolutionRequest, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetOverrunHoldResolutionBlocked>();
        result.HardLimitFailures.ShouldNotBeEmpty();
    }

    /// <summary>Verifies a correction eligible for automatic clearing with no captured hard limit clears the overrun hold.</summary>
    [Fact]
    public async Task CorrectAsync_WhenDimensionHasNoHardLimitAndCorrectionReturnsWithinReserved_ClearsOverrunHold()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, Address(), [], new IdempotencyKey("no-hard-limit-clear-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var scope = (await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.unlimited"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("no-hard-limit-clear-item"));
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), TestContext.Current.CancellationToken);
        var corrected = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1), TestContext.Current.CancellationToken);
        corrected.ClearedOverrunHolds.ShouldNotBeEmpty();
    }

    /// <summary>Verifies a correction eligible for automatic clearing on a concurrent-gauge dimension with a hard limit clears the overrun hold.</summary>
    [Fact]
    public async Task CorrectAsync_WhenDimensionUsesConcurrentGaugeAggregationAndCorrectionClears_ClearsOverrunHold()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, Address(), [new BudgetLimit(new BudgetDimension("test.gauge"), 10, new BudgetUnit("count"), BudgetLimitKind.Hard)], new IdempotencyKey("gauge-clear-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var scope = (await ledger.CreateScopeAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var operationId = new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.gauge"), 1, new BudgetUnit("count"), operationId, null, new IdempotencyKey("gauge-clear-item"));
        var concurrentItem = new BudgetReservationRequest(scope.Id, new BudgetDimension("test.gauge"), 1, new BudgetUnit("count"), operationId, null, new IdempotencyKey("gauge-clear-concurrent-item"));
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [concurrentItem]), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2), TestContext.Current.CancellationToken);
        var corrected = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1), TestContext.Current.CancellationToken);
        corrected.ClearedOverrunHolds.ShouldNotBeEmpty();
    }

    private sealed class UndefinedAggregationCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            if (dimension == new BudgetDimension("test.undefined"))
            {
                descriptor = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("count")]) with
                {
                    Aggregation = (BudgetAggregationKind) 99
                };
                return true;
            }
            descriptor = null;
            return false;
        }
    }

    private sealed class InMemoryBudgetLedgerConformanceFixtureCatalog: IBudgetDimensionCatalog
    {
        private static readonly ImmutableArray<BudgetDimensionDescriptor> Descriptors =
        [
            new(new BudgetDimension("test.sum"), BudgetAggregationKind.Sum, [new BudgetUnit("count")]),
        ];

        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = Descriptors.FirstOrDefault(item => item.Dimension == dimension);
            return descriptor is not null;
        }
    }

    private sealed class SequentialIdGenerator<T>(Func<Guid, T> factory): IIdentifierGenerator<T>
        where T : struct
    {
        private int _value;
        public T Create() => factory(Guid.Parse($"80000000-0000-0000-0000-{Interlocked.Increment(ref _value):000000000000}"));
    }

    private sealed class ConstantIdGenerator<T>(T value): IIdentifierGenerator<T>
        where T : struct
    {
        public T Create() => value;
    }

    /// <summary>Verifies a scope limit whose unit conflicts with a non-immediate ancestor's captured limit is rejected.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenLimitUnitConflictsWithNonImmediateAncestorLimit_ReturnsInvalidLimitRejection()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var admission = new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5));
        var root = (await ledger.CreateScopeAsync(new(new(null, Address(), [new(new("test.multi-unit"), 100, new("count"), BudgetLimitKind.Hard)], new("ancestor-unit-root")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var child = (await ledger.CreateScopeAsync(new(new(root.Id, Address(), [], new("ancestor-unit-child")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;

        var rejected = (await ledger.CreateScopeAsync(new(new(child.Id, Address(), [new(new("test.multi-unit"), 10, new("bytes"), BudgetLimitKind.Hard)], new("ancestor-unit-grandchild")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreateRejected>();

        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.InvalidLimit);
    }

    /// <summary>Verifies a scope limit that widens a non-immediate ancestor's captured hard limit is rejected.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenLimitWidensNonImmediateAncestorHardLimit_ReturnsLimitWiderThanAncestorRejection()
    {
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger();
        var admission = new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5));
        var root = (await ledger.CreateScopeAsync(new(new(null, Address(), [new(new("test.sum"), 10, new("count"), BudgetLimitKind.Hard)], new("ancestor-widen-root")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var child = (await ledger.CreateScopeAsync(new(new(root.Id, Address(), [], new("ancestor-widen-child")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;

        var rejected = (await ledger.CreateScopeAsync(new(new(child.Id, Address(), [new(new("test.sum"), 20, new("count"), BudgetLimitKind.Hard)], new("ancestor-widen-grandchild")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreateRejected>();

        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.LimitWiderThanAncestor);
    }

    /// <summary>Verifies a scope identity source that produces a value already bound to an existing scope is rejected.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenScopeIdentitySourceProducesDuplicate_ThrowsBudgetLedgerStateException()
    {
        var fixedId = new BudgetScopeId(Guid.NewGuid());
        var ledger = new InMemoryBudgetLedger(TimeProvider.System, new ConstantIdGenerator<BudgetScopeId>(fixedId), new SequentialIdGenerator<BudgetReservationId>(id => new(id)), new InMemoryBudgetLedgerConformanceFixtureCatalog());
        var admission = new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5));
        _ = (await ledger.CreateScopeAsync(new(new(null, Address(), [], new("duplicate-scope-first")), admission), TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>();

        var exception = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.CreateScopeAsync(new(new(null, Address(), [], new("duplicate-scope-second")), admission), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("duplicate value");
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
    /// <summary>Verifies the generated log-state accessors work through the classic non-generic enumeration surface
    /// that some third-party logging providers use instead of the generic key/value interface.</summary>
    [Fact]
    public async Task Operations_WhenLoggerEnumeratesStateViaLegacyEnumerable_ExercisesGeneratedStateAccessors()
    {
        var logger = new LegacyEnumeratingLogger();
        var ledger = new InMemoryBudgetLedgerConformanceFixture().CreateLedger(logger);
        var scope = await CreateScopeAsync(ledger, "legacy-enumerable-scope");
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.GetSnapshotAsync(new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Address()), TestContext.Current.CancellationToken));
        logger.CompletedCounts.ShouldNotBeEmpty();
        logger.FailedCounts.ShouldNotBeEmpty();
        logger.CompletedCounts.ShouldAllBe(count => count > 0);
        logger.FailedCounts.ShouldAllBe(count => count > 0);
    }

    private sealed class LegacyEnumeratingLogger: ILogger<InMemoryBudgetLedger>
    {
        internal List<int> CompletedCounts { get; } = [];
        internal List<int> FailedCounts { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not System.Collections.IEnumerable legacy)
            {
                return;
            }

            var count = 0;
            foreach (var _ in legacy)
            {
                count++;
            }

            if (state is IReadOnlyList<KeyValuePair<string, object?>> indexed && indexed.Count > 0)
            {
                for (var index = 0; index < indexed.Count; index++)
                {
                    _ = indexed[index];
                }

                try
                {
                    _ = indexed[indexed.Count];
                }
                catch (IndexOutOfRangeException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            if (eventId.Id == 7050)
            {
                CompletedCounts.Add(count);
            }
            else if (eventId.Id == 7051)
            {
                FailedCounts.Add(count);
            }
        }
    }

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
