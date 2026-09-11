// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Conformance;
using AgentKit.Observability;

using Microsoft.Extensions.Logging;

/// <summary>Verifies DefaultInputPromotionPolicy behavior and contracts.</summary>
[Collection(InputPromotionObservationGroup.Name)]
public sealed class DefaultInputPromotionPolicyTests: InputPromotionPolicyConformanceTests<DefaultInputPromotionPolicyConformanceFixture>
{
    [Fact]
    public async Task PlanAsync_WhenSteeringBoundary_SelectsOnlySteersInAdmissionOrder()
    {
        var policy = ResolvePolicy();
        var context = Context(Admitted(3, InputDelivery.Steer), Admitted(1, InputDelivery.FollowUp), Admitted(2, InputDelivery.Steer));
        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds.ShouldBe([Admission(2), Admission(3)]);
    }

    [Fact]
    public async Task PlanAsync_WhenIdle_SelectsOldestFollowUpThenEveryEligibleSteer()
    {
        var policy = ResolvePolicy();
        var context = Context(PromotionBoundary.OtherwiseIdle, 4, Admitted(4, InputDelivery.FollowUp), Admitted(2, InputDelivery.Steer), Admitted(1, InputDelivery.FollowUp), Admitted(3, InputDelivery.Steer));
        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds.ShouldBe([Admission(1), Admission(2), Admission(3)]);
    }

    [Fact]
    public async Task PlanAsync_WhenBoundCannotIncludeRequiredSteers_ReturnsTypedRejectionWithoutTruncating()
    {
        var policy = ResolvePolicy();
        var context = Context(PromotionBoundary.AfterTurnCommitted, 1, Admitted(1, InputDelivery.Steer), Admitted(2, InputDelivery.Steer));
        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);
        var rejection = result.ShouldBeOfType<InputPromotionPlanRejected>();
        rejection.Kind.ShouldBe(InputPromotionPlanRejectionKind.SelectionLimitExceeded);
        rejection.RequiredCount.ShouldBe(2);
    }

    [Fact]
    public async Task PlanAsync_WhenSteeringBoundaryHasManyIrrelevantFollowUps_SelectsBoundedSteerOnly()
    {
        var policy = ResolvePolicy();
        var inputs = Enumerable.Range(2, 10_000).Select(index => Admitted(index, InputDelivery.FollowUp)).Prepend(Admitted(1, InputDelivery.Steer)).ToArray();
        var context = Context(PromotionBoundary.AfterTurnCommitted, 1, inputs);
        var result = await policy.PlanAsync(context, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds.ShouldBe([Admission(1)]);
    }

    [Fact]
    public async Task PlanAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var policy = ResolvePolicy();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var action = () => policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), cancellation.Token).AsTask();
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void RecordPromotionPlan_WhenArgumentsAreInvalid_ThrowsBeforePublishingMeasurements()
    {
        var boundaryException = Should.Throw<ArgumentOutOfRangeException>(() => IOMetrics.RecordPromotionPlan((PromotionBoundary) 42, InputPromotionPlanOutcome.Planned, null));
        var outcomeException = Should.Throw<ArgumentOutOfRangeException>(() => IOMetrics.RecordPromotionPlan(PromotionBoundary.OtherwiseIdle, (InputPromotionPlanOutcome) 42, null));
        var durationException = Should.Throw<ArgumentOutOfRangeException>(() => IOMetrics.RecordPromotionPlan(PromotionBoundary.OtherwiseIdle, InputPromotionPlanOutcome.Planned, TimeSpan.FromTicks(-1)));
        boundaryException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        boundaryException.ParamName.ShouldBe("boundary");
        outcomeException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        outcomeException.ParamName.ShouldBe("outcome");
        durationException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        durationException.ParamName.ShouldBe("elapsed");
    }

    private static IInputPromotionPolicy ResolvePolicy() => new ServiceCollection().AddInputPromotionPolicy().BuildServiceProvider().GetRequiredService<IInputPromotionPolicy>();
    internal static InputPromotionContext Context(params AdmittedInput[] inputs) => Context(PromotionBoundary.AfterTurnCommitted, 16, inputs);
    internal static InputPromotionContext Context(PromotionBoundary boundary, int maximumPromotions, params AdmittedInput[] inputs) => new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(2), new SessionBranchCursor(Branch(), Entry(9)), new SessionSequence(Math.Max(10, inputs.Max(static input => input.AdmittedSequence.Value))), new SessionVersion(4), null, boundary, Turn(), NextTurn(), [.. inputs], maximumPromotions);
    internal static AdmittedInput Admitted(long sequence, InputDelivery delivery)
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var input = new AgentInput(Input(sequence), delivery, [new TextPart($"input-{sequence}", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new AdmittedInput(Admission(sequence), Agent(), Session(), Lane(), identity, new SessionSequence(sequence), input, input, new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint($"original:{sequence}"), new InputFingerprint($"effective:{sequence}")), DateTimeOffset.UnixEpoch.AddSeconds(sequence));
    }

    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static SessionEntryId Entry(long value) => new(Guid.Parse($"20000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static ExecutionLaneId Lane() => new(Guid.Parse("45000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("60000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("80000000-0000-0000-0000-000000000001")), Turn());
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
                if (activity.OperationName == AgentKitActivityNames.InputPromotionPlan && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForPromotion((measurement, tags) =>
        {
            _ = Interlocked.Add(ref measurements, measurement);
            metricTags.AddRange(tags.ToArray());
        }, null);
        var logger = new RecordingLogger();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, logger);
        var context = Context(Admitted(1, InputDelivery.Steer));
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
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.InputPromotionBoundary, AgentKitTagNames.Outcome], ignoreOrder: true);
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
                if (activity.OperationName == AgentKitActivityNames.InputPromotionPlan && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingLogger();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, logger);
        var context = Context(PromotionBoundary.AfterTurnCommitted, 1, Admitted(1, InputDelivery.Steer), Admitted(2, InputDelivery.Steer));
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
        var result = await policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<InputPromotionPlan>();
    }

    [Fact]
    public async Task PlanAsync_WhenMetricCallbackThrows_ReturnsTheSemanticPlan()
    {
        using var listener = MeterListenerForPromotion(static (_, _) => throw new InvalidOperationException("observer"), null);
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System);
        var result = await policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<InputPromotionPlan>();
    }

    [Fact]
    public async Task PlanAsync_WhenInitialClockMeasurementFails_RecordsCountWithoutFakeDuration()
    {
        var count = 0L;
        var durationMeasurements = 0;
        using var listener = MeterListenerForPromotion((measurement, _) => count += measurement, (_, _) => durationMeasurements++);
        var policy = new DefaultInputPromotionPolicy(new ThrowingTimeProvider(throwOnCall: 1));
        var result = await policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<InputPromotionPlan>();
        count.ShouldBe(1);
        durationMeasurements.ShouldBe(0);
    }

    [Fact]
    public async Task PlanAsync_WhenElapsedClockMeasurementFails_RecordsCountWithoutFakeDuration()
    {
        var count = 0L;
        var durationMeasurements = 0;
        using var listener = MeterListenerForPromotion((measurement, _) => count += measurement, (_, _) => durationMeasurements++);
        var policy = new DefaultInputPromotionPolicy(new ThrowingTimeProvider(throwOnCall: 2));
        var result = await policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), TestContext.Current.CancellationToken);
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
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), cancellation.Token));
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
                if (activity.OperationName == AgentKitActivityNames.InputPromotionPlan && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForPromotion((_, tags) => metricTags.AddRange(tags.ToArray()), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var logger = new RecordingLogger();
        var policy = new DefaultInputPromotionPolicy(TimeProvider.System, logger);
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.PlanAsync(Context(Admitted(1, InputDelivery.Steer)), cancellation.Token));
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("input-1");
        logger.Events.ShouldHaveSingleItem().ShouldBe((1001, LogLevel.Information));
        logger.Messages.ShouldAllBe(message => !message.Contains("input-1", StringComparison.Ordinal));
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.InputPromotionBoundary, AgentKitTagNames.Outcome], ignoreOrder: true);
        metricTags.Any(static tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "cancelled")).ShouldBeTrue();
    }

    private static MeterListener MeterListenerForPromotion(Action<long, ReadOnlySpan<KeyValuePair<string, object?>>>? onCount, Action<double, ReadOnlySpan<KeyValuePair<string, object?>>>? onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.InputPromotionPlanCount or AgentKitMetricNames.InputPromotionPlanDuration)
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

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    private sealed class RecordingLogger: ILogger<DefaultInputPromotionPolicy>
    {
        public List<string> Messages { get; } = [];
        public List<(int EventId, LogLevel Level)> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
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
            return _calls == throwOnCall ? throw new InvalidOperationException("clock") : _calls * 100;
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultInputPromotionPolicy>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => throw new InvalidOperationException("observer");
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    /// <inheritdoc/>
    protected override DefaultInputPromotionPolicyConformanceFixture CreateFixture() => new();
}
