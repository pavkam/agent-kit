// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

using Microsoft.Extensions.Logging;

/// <summary>Verifies safe, content-free promotion-policy observation.</summary>
[Collection(InputPromotionObservationGroup.Name)]
public sealed class InputPromotionObservabilityTests
{
    [Fact]
    public async Task PlanAsync_WhenObserved_EmitsContentFreeSuccessfulActivityAndBoundedMetric()
    {
        Activity? stopped = null;
        long measurements = 0;
        List<KeyValuePair<string, object?>> metricTags = [];
        using var parent = new Activity("input-promotion-test").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.InputPromotionPlan
                    && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForPromotion(
            (measurement, tags) =>
            {
                _ = Interlocked.Add(ref measurements, measurement);
                metricTags.AddRange(tags.ToArray());
            },
            null);
        var logger = new RecordingLogger();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, logger);
        var context = DefaultInputPromotionPolicyTests.Context(
            DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer));

        _ = await policy.PlanAsync(context, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.InputPromotionPlan);
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(context.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(context.SessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(context.ExpectedOperation.RunId.ToString());
        activity.GetTagItem(AgentKitTagNames.OperationId).ShouldBe(context.ExpectedOperation.OperationId.ToString());
        activity.GetTagItem(AgentKitTagNames.InputPromotionBoundary).ShouldBe(PromotionBoundary.AfterTurnCommitted.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("input-1");
        logger.Messages.ShouldAllBe(message => !message.Contains("input-1", StringComparison.Ordinal));
        logger.Events.ShouldHaveSingleItem().ShouldBe((1000, LogLevel.Information));
        measurements.ShouldBe(1);
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe(
            [AgentKitTagNames.InputPromotionBoundary, AgentKitTagNames.Outcome], ignoreOrder: true);
        metricTags.ShouldAllBe(static tag => tag.Value is string);
    }

    [Fact]
    public async Task PlanAsync_WhenPlanIsRejected_EmitsTruthfulErrorActivityAndLog()
    {
        Activity? stopped = null;
        using var parent = new Activity("input-promotion-rejection-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.InputPromotionPlan
                    && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingLogger();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, logger);
        var context = DefaultInputPromotionPolicyTests.Context(PromotionBoundary.AfterTurnCommitted, 1,
            DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer),
            DefaultInputPromotionPolicyTests.Admitted(2, InputDelivery.Steer));

        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionPlanRejected>();
        var activity = stopped.ShouldNotBeNull();
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        logger.Events.ShouldHaveSingleItem().ShouldBe((1000, LogLevel.Information));
    }

    [Fact]
    public async Task PlanAsync_WhenLoggerThrows_ReturnsTheSemanticPlan()
    {
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, new ThrowingLogger());

        var result = await policy.PlanAsync(
            DefaultInputPromotionPolicyTests.Context(DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionPlan>();
    }

    [Fact]
    public async Task PlanAsync_WhenMetricCallbackThrows_ReturnsTheSemanticPlan()
    {
        using var listener = MeterListenerForPromotion(
            static (_, _) => throw new InvalidOperationException("observer"),
            null);
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System);

        var result = await policy.PlanAsync(
            DefaultInputPromotionPolicyTests.Context(DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionPlan>();
    }

    [Fact]
    public async Task PlanAsync_WhenInitialClockMeasurementFails_RecordsCountWithoutFakeDuration()
    {
        var count = 0L;
        var durationMeasurements = 0;
        using var listener = MeterListenerForPromotion(
            (measurement, _) => count += measurement,
            (_, _) => durationMeasurements++);
        var policy = new DefaultInputPromotionPolicy(new ThrowingTimeProvider(throwOnCall: 1));

        var result = await policy.PlanAsync(
            DefaultInputPromotionPolicyTests.Context(DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionPlan>();
        count.ShouldBe(1);
        durationMeasurements.ShouldBe(0);
    }

    [Fact]
    public async Task PlanAsync_WhenElapsedClockMeasurementFails_RecordsCountWithoutFakeDuration()
    {
        var count = 0L;
        var durationMeasurements = 0;
        using var listener = MeterListenerForPromotion(
            (measurement, _) => count += measurement,
            (_, _) => durationMeasurements++);
        var policy = new DefaultInputPromotionPolicy(new ThrowingTimeProvider(throwOnCall: 2));

        var result = await policy.PlanAsync(
            DefaultInputPromotionPolicyTests.Context(DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<InputPromotionPlan>();
        count.ShouldBe(1);
        durationMeasurements.ShouldBe(0);
    }

    [Fact]
    public async Task PlanAsync_WhenCancelledAndLoggerThrows_PreservesTheCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, new ThrowingLogger());

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await policy.PlanAsync(
                DefaultInputPromotionPolicyTests.Context(DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer)),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task PlanAsync_WhenCancelled_EmitsCancelledActivityLogAndMetricWithoutInputContent()
    {
        Activity? stopped = null;
        List<KeyValuePair<string, object?>> metricTags = [];
        using var parent = new Activity("input-promotion-cancellation-test").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.InputPromotionPlan
                    && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForPromotion(
            (_, tags) => metricTags.AddRange(tags.ToArray()),
            null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var logger = new RecordingLogger();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, logger);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await policy.PlanAsync(
                DefaultInputPromotionPolicyTests.Context(DefaultInputPromotionPolicyTests.Admitted(1, InputDelivery.Steer)),
                cancellation.Token));

        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("input-1");
        logger.Events.ShouldHaveSingleItem().ShouldBe((1001, LogLevel.Information));
        logger.Messages.ShouldAllBe(message => !message.Contains("input-1", StringComparison.Ordinal));
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe(
            [AgentKitTagNames.InputPromotionBoundary, AgentKitTagNames.Outcome], ignoreOrder: true);
        metricTags.Any(static tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "cancelled"))
            .ShouldBeTrue();
    }

    private static MeterListener MeterListenerForPromotion(
        Action<long, ReadOnlySpan<KeyValuePair<string, object?>>>? onCount,
        Action<double, ReadOnlySpan<KeyValuePair<string, object?>>>? onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name is AgentKitMetricNames.InputPromotionPlanCount
                        or AgentKitMetricNames.InputPromotionPlanDuration)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        if (onCount is not null)
        {
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == AgentKitMetricNames.InputPromotionPlanCount)
                {
                    onCount(measurement, tags);
                }
            });
        }
        if (onDuration is not null)
        {
            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == AgentKitMetricNames.InputPromotionPlanDuration)
                {
                    onDuration(measurement, tags);
                }
            });
        }
        listener.Start();
        return listener;
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private sealed class RecordingLogger: ILogger<DefaultInputPromotionPolicy>
    {
        public List<string> Messages { get; } = [];
        public List<(int EventId, LogLevel Level)> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Events.Add((eventId.Id, logLevel));
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class ThrowingTimeProvider(int throwOnCall): TimeProvider
    {
        private int _calls;

        public override long TimestampFrequency => 1_000;

        public override long GetTimestamp()
        {
            _calls++;
            return _calls == throwOnCall
                ? throw new InvalidOperationException("clock")
                : _calls * 100;
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultInputPromotionPolicy>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => throw new InvalidOperationException("observer");

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }
}
