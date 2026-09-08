// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

[Collection(ContinuationObservationGroup.Name)]
public sealed class DefaultRunContinuationPolicyObservabilityTests
{
    [Fact]
    public async Task DecideAsync_WhenClockMeasuresElapsedTime_RecordsMeasuredDurationAndBoundedDimensions()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        var clock = new SequencedTimeProvider([100, 350]);
        var durations = new List<double>();
        var tagSets = new List<KeyValuePair<string, object?>[]>();
        using var listener = MeterListenerForContinuation(
            onCount: (_, tags) => tagSets.Add(tags.ToArray()),
            onDuration: (measurement, _) => durations.Add(measurement));
        var policy = new DefaultRunContinuationPolicy(clock);

        var decision = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        durations.ShouldHaveSingleItem().ShouldBe(0.25);
        var tags = tagSets.ShouldHaveSingleItem();
        tags.Select(static tag => tag.Key).ShouldBe(
            [AgentKitTagNames.ContinuationBoundary, AgentKitTagNames.Outcome],
            ignoreOrder: true);
        tags.ShouldAllBe(static tag => tag.Value is string);
    }

    [Fact]
    public async Task DecideAsync_WhenInitialClockMeasurementThrows_PreservesDecisionAndDoesNotFabricateDuration()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        var durations = 0;
        var counts = 0L;
        using var listener = MeterListenerForContinuation(
            onCount: (measurement, _) => counts += measurement,
            onDuration: (_, _) => durations++);
        var policy = new DefaultRunContinuationPolicy(new ThrowingTimeProvider(throwOnCall: 1));

        var decision = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        counts.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_WhenElapsedClockMeasurementThrows_PreservesDecisionAndDoesNotRecordDuration()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        var durations = 0;
        var counts = 0L;
        using var listener = MeterListenerForContinuation(
            onCount: (measurement, _) => counts += measurement,
            onDuration: (_, _) => durations++);
        var policy = new DefaultRunContinuationPolicy(new ThrowingTimeProvider(throwOnCall: 2));

        var decision = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        counts.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_WhenLoggerThrows_PreservesSemanticDecision()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System, new ThrowingLogger());

        var decision = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    [Fact]
    public async Task DecideAsync_WhenMetricCallbackThrows_PreservesSemanticDecision()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        using var listener = MeterListenerForContinuation(
            onCount: static (_, _) => throw new InvalidOperationException("observer"),
            onDuration: null);
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System);

        var decision = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    [Fact]
    public async Task DecideAsync_WhenCancelledAndLoggerThrows_PreservesOriginalCancellationToken()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System, new ThrowingLogger());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await policy.DecideAsync(
                fixture.CreateContext(new IdleContinuationBoundary(), []),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task DecideAsync_WhenObserved_EmitsContentFreeLogActivityAndMetrics()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        var logger = new RecordingLogger();
        Activity? stopped = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAll,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(activityListener);
        List<KeyValuePair<string, object?>> metricTags = [];
        using var meterListener = MeterListenerForContinuation(
            onCount: (_, tags) => metricTags.AddRange(tags.ToArray()),
            onDuration: null);
        var retry = new ExplicitPolicyContinuationCause("contains-sensitive-test-marker");
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System, logger);

        _ = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), [retry]),
            TestContext.Current.CancellationToken);

        logger.Messages.ShouldHaveSingleItem().ShouldNotContain("contains-sensitive-test-marker");
        var activity = stopped.ShouldNotBeNull();
        activity.TagObjects.Select(static tag => tag.Value?.ToString())
            .ShouldNotContain("contains-sensitive-test-marker");
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe(
            [AgentKitTagNames.ContinuationBoundary, AgentKitTagNames.Outcome],
            ignoreOrder: true);
        metricTags.ShouldAllBe(static tag => tag.Value is string);
    }

    [Fact]
    public async Task DecideAsync_WhenDecisionsReachTerminalObservation_ReportsTruthfulActivityStatus()
    {
        var fixture = new DefaultRunContinuationPolicyTests.Fixture();
        List<Activity> stopped = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAll,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System);

        _ = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);
        _ = await policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), [new ExplicitPolicyContinuationCause("follow-up")]),
            TestContext.Current.CancellationToken);
        _ = await policy.DecideAsync(
            fixture.CreateContext(
                new IdleContinuationBoundary(),
                [],
                new AgentRunTurnLimitReached(2)),
            TestContext.Current.CancellationToken);
        var rejected = new OutputRejected(new OutputValidationFailure(
            OutputValidationFailureKind.ValidatorFailed,
            "safe rejection",
            []));
        _ = await policy.DecideAsync(
            fixture.CreateContext(fixture.CreateCommittedBoundary(rejected, requiresOutput: true), []),
            TestContext.Current.CancellationToken);

        stopped.Count.ShouldBe(4);
        stopped[0].Status.ShouldBe(ActivityStatusCode.Ok);
        stopped[0].GetTagItem(AgentKitTagNames.ContinuationDecision).ShouldBe(nameof(CompleteRun));
        stopped[1].Status.ShouldBe(ActivityStatusCode.Ok);
        stopped[1].GetTagItem(AgentKitTagNames.ContinuationDecision).ShouldBe(nameof(ContinueRun));
        stopped[2].Status.ShouldBe(ActivityStatusCode.Error);
        stopped[2].GetTagItem(AgentKitTagNames.ContinuationDecision).ShouldBe(nameof(HaltRun));
        stopped[3].Status.ShouldBe(ActivityStatusCode.Error);
        stopped[3].GetTagItem(AgentKitTagNames.ContinuationDecision).ShouldBe(nameof(HaltRun));
    }

    private static MeterListener MeterListenerForContinuation(
        Action<long, ReadOnlySpan<KeyValuePair<string, object?>>>? onCount,
        Action<double, ReadOnlySpan<KeyValuePair<string, object?>>>? onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name is AgentKitMetricNames.RunContinuationEvaluationCount
                        or AgentKitMetricNames.RunContinuationEvaluationDuration)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        if (onCount is not null)
        {
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == AgentKitMetricNames.RunContinuationEvaluationCount)
                {
                    onCount(measurement, tags);
                }
            });
        }
        if (onDuration is not null)
        {
            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == AgentKitMetricNames.RunContinuationEvaluationDuration)
                {
                    onDuration(measurement, tags);
                }
            });
        }
        listener.Start();
        return listener;
    }

    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private sealed class SequencedTimeProvider(IEnumerable<long> timestamps): TimeProvider
    {
        private readonly Queue<long> _timestamps = new(timestamps);

        public override long TimestampFrequency => 1_000;

        public override long GetTimestamp() => _timestamps.Dequeue();
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

    private sealed class ThrowingLogger: ILogger<DefaultRunContinuationPolicy>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger");
    }

    private sealed class RecordingLogger: ILogger<DefaultRunContinuationPolicy>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
