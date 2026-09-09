// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

/// <summary>Verifies truthful, bounded, and isolated SQLite ledger diagnostics.</summary>
public sealed class SqliteBudgetLedgerDiagnosticsTests
{
    /// <summary>Proves caller argument guards run before activities or storage access.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenArgumentIsNull_EmitsNoActivity()
    {
        using var parent = new Activity("sqlite-invalid-test").Start();
        var started = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Name == AgentKitActivityNames.BudgetLedgerOperation
                    && options.Parent.TraceId == parent.TraceId
                    ? ActivitySamplingResult.AllData
                    : ActivitySamplingResult.None;
            },
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
                {
                    _ = Interlocked.Increment(ref started);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new SqliteBudgetLedgerConformanceFixture().CreateLedger();
        var before = Volatile.Read(ref started);

        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await ledger.GetSnapshotAsync(null!, TestContext.Current.CancellationToken));

        Volatile.Read(ref started).ShouldBe(before);
    }

    /// <summary>Proves cancellation and typed admission rejection use distinct truthful terminal evidence.</summary>
    [Fact]
    public async Task Operations_WhenCancelledOrRejected_EmitTruthfulErrorOutcomes()
    {
        using var parent = new Activity("sqlite-terminal-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateScopedListener(parent, stopped);
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var ledger = new SqliteBudgetLedgerConformanceFixture(logger).CreateLedger();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await ledger.ReadUnresolvedStartedAsync(new(new(new(Guid.NewGuid()),
                new(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null)), 1, null), cancellation.Token));
        var rejected = await ledger.CreateScopeAsync(CreateRequest("diagnostic-rejected", new("missing.dimension")), TestContext.Current.CancellationToken);

        _ = rejected.ShouldBeOfType<BudgetLedgerScopeCreateRejected>();
        var cancelled = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "cancelled");
        cancelled.Status.ShouldBe(ActivityStatusCode.Error);
        _ = cancelled.GetTagItem(AgentKitTagNames.ErrorType).ShouldNotBeNull();
        var rejection = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "rejected");
        rejection.Status.ShouldBe(ActivityStatusCode.Error);
        rejection.GetTagItem(AgentKitTagNames.ErrorType).ShouldBeNull();
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 7070
            && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "cancelled")));
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 7070
            && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "rejected")));
    }

    /// <summary>Proves persistence and collaborator faults retain normalized exception evidence.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenCatalogFails_EmitsFaultWithoutProtectedContent()
    {
        const string sentinel = "protected-key-sentinel";
        const string pathSentinel = "protected-path-sentinel";
        const string resourceSentinel = "protected-resource-sentinel";
        using var parent = new Activity("sqlite-fault-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateScopedListener(parent, stopped);
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name == AgentKitMetricNames.BudgetLedgerOperationCount)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                measurements.Enqueue(tags.ToArray());
            }
        });
        meterListener.Start();
        var fixture = new SqliteBudgetLedgerConformanceFixture(logger, pathSentinel);
        var ledger = fixture.CreateLedger();
        fixture.ArmCatalogFailure(resourceSentinel);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ledger.CreateScopeAsync(CreateRequest(sentinel, new("test.sum")), TestContext.Current.CancellationToken));

        var activity = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "faulted");
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        var errorType = activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldNotBeNull();
        errorType.ToString()!.ShouldContain(nameof(InvalidOperationException));
        activity.TagObjects.Any(item => item.Value is not null
            && (item.Value.ToString()!.Contains(sentinel, StringComparison.Ordinal)
                || item.Value.ToString()!.Contains(pathSentinel, StringComparison.Ordinal)
                || item.Value.ToString()!.Contains(resourceSentinel, StringComparison.Ordinal))).ShouldBeFalse();
        logger.Entries.SelectMany(static entry => entry.State).Any(item => item.Value is not null
            && (item.Value.ToString()!.Contains(sentinel, StringComparison.Ordinal)
                || item.Value.ToString()!.Contains(pathSentinel, StringComparison.Ordinal)
                || item.Value.ToString()!.Contains(resourceSentinel, StringComparison.Ordinal))).ShouldBeFalse();
        measurements.SelectMany(static tags => tags).Any(item => item.Value is not null
            && (item.Value.ToString()!.Contains(sentinel, StringComparison.Ordinal)
                || item.Value.ToString()!.Contains(pathSentinel, StringComparison.Ordinal)
                || item.Value.ToString()!.Contains(resourceSentinel, StringComparison.Ordinal))).ShouldBeFalse();
    }

    /// <summary>Proves failing standard observers cannot change a committed result or ambient parent.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenObserversThrow_PreservesResultAndParent()
    {
        using var parent = new Activity("sqlite-throwing-observer-test").Start();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name is AgentKitMetricNames.BudgetLedgerOperationCount or AgentKitMetricNames.BudgetLedgerOperationDuration)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                throw new InvalidOperationException("meter observer failure");
            }
        });
        meterListener.SetMeasurementEventCallback<double>((_, _, _, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                throw new InvalidOperationException("meter observer failure");
            }
        });
        meterListener.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId
                    ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
                {
                    throw new InvalidOperationException("observer failure");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new SqliteBudgetLedgerConformanceFixture(new ThrowingLogger()).CreateLedger();

        var result = await ledger.CreateScopeAsync(CreateRequest("throwing-observer", new("test.sum")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    /// <summary>Proves disabled diagnostic listeners leave semantic execution unchanged.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenDiagnosticsAreDisabled_Succeeds()
    {
        var ledger = new SqliteBudgetLedgerConformanceFixture().CreateLedger();

        var result = await ledger.CreateScopeAsync(CreateRequest("disabled-observers", new("test.sum")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
    }

    private static BudgetLedgerScopeCreateRequest CreateRequest(string key, BudgetDimension dimension) => new(
        new BudgetScopeRequest(null, new(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null),
            [new(dimension, 10, new("count"), BudgetLimitKind.Hard)], new(key)),
        new(8, 32, TimeSpan.FromMinutes(5)));

    private static ActivityListener CreateScopedListener(Activity parent, ConcurrentQueue<Activity> stopped) => new()
    {
        ShouldListenTo = static source => source.Name == "AgentKit",
        Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
        {
            return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId
                ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
        },
        ActivityStopped = activity =>
        {
            if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
            {
                stopped.Enqueue(activity);
            }
        },
    };

    /// <summary>Proves snapshot correlation and terminal outcome are emitted without affecting the semantic receipt.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenObserved_EmitsCorrelatedSuccessfulActivity()
    {
        using var parent = new Activity("sqlite-budget-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name == AgentKitMetricNames.BudgetLedgerOperationCount)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                measurements.Enqueue(tags.ToArray());
            }
        });
        meterListener.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parent.SpanId && activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation)
                {
                    stopped.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var ledger = new SqliteBudgetLedgerConformanceFixture(logger).CreateLedger();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(null, address,
            [new(new("test.sum"), 10, new("count"), BudgetLimitKind.Hard)], new("diagnostic")),
            new(8, 32, TimeSpan.FromMinutes(5)));
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = (await ledger.CreateScopeAsync(request, cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;

        var snapshot = await ledger.GetSnapshotAsync(scope, cancellationToken);

        snapshot.ScopeId.ShouldBe(scope.Id);
        var activity = stopped.Last(item => item.GetTagItem(AgentKitTagNames.BudgetOperation)?.ToString() == "snapshot");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.BudgetScopeId)?.ToString().ShouldBe(scope.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.TenantId)?.ToString().ShouldBe(scope.Address.TenantId.ToString());
        var metric = measurements.Last(tags => tags.Any(tag => tag.Key == AgentKitTagNames.BudgetOperation && Equals(tag.Value, "snapshot")));
        metric.ShouldContain(tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "succeeded"));
        metric.ShouldNotContain(tag => tag.Key == AgentKitTagNames.TenantId || tag.Key == AgentKitTagNames.BudgetScopeId);
        var log = logger.Entries.Last(entry => entry.EventId.Id == 7070 && entry.State.Any(item => item.Key == "BudgetOperation" && Equals(item.Value, "snapshot")));
        log.State.ShouldContain(item => item.Key == "BudgetScopeId" && Equals(item.Value, scope.Id.ToString()));
        log.State.ShouldContain(item => item.Key == "TenantId" && Equals(item.Value, scope.Address.TenantId.ToString()));
        log.State.ShouldNotContain(item => item.Key.Contains("Resource", StringComparison.OrdinalIgnoreCase)
            || item.Key.Contains("Fingerprint", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CaptureLogger: ILogger<SqliteBudgetLedger>
    {
        internal ConcurrentQueue<(EventId EventId, KeyValuePair<string, object?>[] State)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.Enqueue((eventId, values.ToArray()));
            }
        }
    }

    private sealed class ThrowingLogger: ILogger<SqliteBudgetLedger>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger failure");
    }
}
