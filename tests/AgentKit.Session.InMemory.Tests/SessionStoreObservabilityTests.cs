// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

public sealed class SessionStoreObservabilityTests
{
    [Fact]
    public async Task CreateAsync_WhenLoggerThrows_PreservesCommittedResult()
    {
        var store = CreateStore(new ThrowingSessionStoreLogger());

        var result = await store.CreateAsync(
            TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_WhenActivityListenerThrows_PreservesCommittedResultAndParent(
        bool throwOnStart)
    {
        using var parent = new Activity("session.store.test.parent").Start();
        var parentTraceId = parent.TraceId;
        var parentSpanId = parent.SpanId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStarted = activity =>
            {
                if (throwOnStart
                    && activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                    && activity.TraceId == parentTraceId
                    && activity.ParentSpanId == parentSpanId)
                {
                    throw new InvalidOperationException("listener start failure");
                }
            },
            ActivityStopped = activity =>
            {
                if (!throwOnStart
                    && activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                    && activity.TraceId == parentTraceId
                    && activity.ParentSpanId == parentSpanId)
                {
                    throw new InvalidOperationException("listener stop failure");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var store = TestFactory.CreateStore();

        var result = await store.CreateAsync(
            TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task CreateAsync_WhenMeterListenerThrows_PreservesCommittedResult()
    {
        using var parent = new Activity("session.store.meter.test.parent").Start();
        var parentTraceId = parent.TraceId;
        using var listener = new MeterListener
        {
            InstrumentPublished = static (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionStoreOperationCount)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.TraceId == parentTraceId)
            {
                throw new InvalidOperationException("meter listener failure");
            }
        });
        listener.Start();
        var store = TestFactory.CreateStore();

        var result = await store.CreateAsync(
            TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
    }

    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsOnlyBoundedContentFreeDiagnostics()
    {
        const string protectedMarker = "never-export-this-request-marker";
        var logger = new RecordingSessionStoreLogger();
        var store = CreateStore(logger);
        var request = TestFactory.CreateRequest(idempotencyKey: new IdempotencyKey(protectedMarker));
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(request.AgentId.ToString()) == true);
        var measurements = new ConcurrentQueue<ImmutableArray<KeyValuePair<string, object?>>>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionStoreOperationCount)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Enqueue([.. tags]));
        meterListener.Start();

        _ = (await store.CreateAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("succeeded");
        foreach (var value in activity.Tags.Values)
        {
            string.Equals(value?.ToString(), protectedMarker, StringComparison.Ordinal).ShouldBeFalse();
        }

        var events = logger.Snapshot();
        events.ShouldContain(static item => item.EventId.Id == 16000);
        foreach (var item in events)
        {
            item.Message.Contains(protectedMarker, StringComparison.Ordinal).ShouldBeFalse();
        }

        measurements.Any(tags =>
            tags.Any(static tag => tag.Key == AgentKitTagNames.SessionOperation
                && tag.Value?.ToString() == "create")
            && tags.Any(static tag => tag.Key == AgentKitTagNames.Outcome
                && tag.Value?.ToString() == "succeeded")).ShouldBeTrue();
        foreach (var tags in measurements)
        {
            tags.All(static tag =>
                tag.Key is AgentKitTagNames.SessionOperation or AgentKitTagNames.Outcome).ShouldBeTrue();
        }
    }

    private static InMemorySessionStore CreateStore(ILogger<InMemorySessionStore> logger)
    {
        Debug.Assert(logger is not null, "A diagnostic logger is required.");
        var security = new TestSecurityHarness();
        var store = new InMemorySessionStore(
            new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)),
            new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)),
            security,
            security,
            TimeProvider.System,
            logger);
        TestSecurityHarness.Register(store, security);
        return store;
    }

    private static ActivitySamplingResult SampleAllData(
        ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
}
