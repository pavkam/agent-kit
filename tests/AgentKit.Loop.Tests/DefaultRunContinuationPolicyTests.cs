// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Diagnostics.Metrics;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;

/// <summary>Verifies DefaultRunContinuationPolicy behavior and contracts.</summary>
[Collection(ContinuationObservationGroup.Name)]
public sealed class DefaultRunContinuationPolicyTests: RunContinuationPolicyConformanceTests<DefaultRunContinuationPolicyTests.Fixture>
{
    /// <inheritdoc/>
    protected override Fixture CreateFixture() => new();
    [Fact]
    public async Task DecideAsync_WhenPromotedInputAndOutputRepairCoexist_SelectsInputAndRetainsRepair()
    {
        var fixture = CreateFixture();
        var retry = CreateRetry();
        var boundary = fixture.CreateCommittedBoundary(retry, requiresOutput: true);
        var promotion = new PromotedInputContinuationCause(fixture.CreatePromotion(boundary.Response.TurnId!.Value));
        var repair = new OutputRepairContinuationCause(retry);
        var decision = await fixture.Policy.DecideAsync(fixture.CreateContext(boundary, [repair, promotion]), TestContext.Current.CancellationToken);
        var reason = decision.ShouldBeOfType<ContinueRun>().Reason;
        reason.SelectedCause.ShouldBeSameAs(promotion);
        reason.OtherPendingCauses.ShouldBe([repair]);
    }

    [Fact]
    public async Task DecideAsync_WhenRequiredOutputIsMissingAndExplicitCauseExists_HaltsInvalidState()
    {
        var fixture = CreateFixture();
        var context = fixture.CreateContext(fixture.CreateCommittedBoundary(decision: null, requiresOutput: true), [new ExplicitPolicyContinuationCause("follow-up")]);
        var decision = await fixture.Policy.DecideAsync(context, TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunInvalidState>();
    }

    [Fact]
    public async Task DecideAsync_WhenConfigurationRejectedAndPromotedInputExists_HaltsOutput()
    {
        var fixture = CreateFixture();
        var rejected = new OutputConfigurationRejected(new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "Invalid schema.", []));
        var boundary = fixture.CreateCommittedBoundary(rejected, requiresOutput: true);
        var promoted = new PromotedInputContinuationCause(fixture.CreatePromotion(boundary.Response.TurnId!.Value));
        var decision = await fixture.Policy.DecideAsync(fixture.CreateContext(boundary, [promoted]), TestContext.Current.CancellationToken);
        decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunOutputRejected>().Rejection.ShouldBeSameAs(rejected);
    }

    [Theory]
    [InlineData(AgentRunState.AwaitingModel)]
    [InlineData(AgentRunState.StreamingModel)]
    [InlineData(AgentRunState.RecordingToolCalls)]
    [InlineData(AgentRunState.AwaitingTools)]
    [InlineData(AgentRunState.CommittingToolResults)]
    public async Task DecideAsync_WhenEffectsAreActive_DoesNotCompleteFalseIdle(AgentRunState state)
    {
        var fixture = CreateFixture();
        var decision = await fixture.Policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), [], state: state), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunInvalidState>();
    }

    [Fact]
    public async Task DecideAsync_WhenModelIsStreaming_DoesNotCompleteCommittedBoundary()
    {
        var fixture = CreateFixture();
        var decision = await fixture.Policy.DecideAsync(fixture.CreateContext(fixture.CreateCommittedBoundary(), [], state: AgentRunState.StreamingModel), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunInvalidState>();
    }

    [Fact]
    public async Task DecideAsync_WhenActivityStartListenerThrows_PreservesSemanticDecision()
    {
        using var listener = CreateThrowingListener(throwOnStart: true);
        ActivitySource.AddActivityListener(listener);
        var fixture = CreateFixture();
        var decision = await fixture.Policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    [Fact]
    public async Task DecideAsync_WhenActivityStopListenerThrows_PreservesSemanticDecisionAndAmbientActivity()
    {
        using var parent = new Activity("parent").Start();
        using var listener = CreateThrowingListener(throwOnStart: false);
        ActivitySource.AddActivityListener(listener);
        var fixture = CreateFixture();
        var decision = await fixture.Policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    private static OutputRetryRequired CreateRetry() => new(new OutputRepairInstruction("Repair output."), new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "Rejected.", []));
    private static ActivityListener CreateThrowingListener(bool throwOnStart) => new()
    {
        ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
        Sample = SampleAll,
        ActivityStarted = throwOnStart ? static _ => throw new InvalidOperationException("observer") : null,
        ActivityStopped = throwOnStart ? null : static _ => throw new InvalidOperationException("observer"),
    };
    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData;
    /// <summary>Composes the first-party policy with one stable run correlation.</summary>
    public sealed class Fixture: IRunContinuationPolicyConformanceFixture
    {
        private readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        private readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        private readonly ExecutionLaneId _laneId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        private readonly OperationId _operationId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        private readonly RunId _runId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        private readonly BranchId _branchId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        private readonly OperationStateRevision _revision = new(1);
        private readonly SessionSequence _cutoff = new(7);
        /// <inheritdoc/>
        public IRunContinuationPolicy Policy { get; } = new DefaultRunContinuationPolicy(TimeProvider.System);

        /// <inheritdoc/>
        public RunContinuationContext CreateContext(RunContinuationBoundary boundary, ImmutableArray<RunContinuationCause> causes, AgentRunOutcome? requiredStop = null, AgentRunState state = AgentRunState.Driving) => new(_agentId, _sessionId, _laneId, _operationId, _runId, state, _revision, new SessionBranchCursor(_branchId, new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))), _cutoff, new ConfigurationVersion(1), new RunPolicyVersion(1), boundary, requiredStop, causes);
        /// <inheritdoc/>
        public CommittedTurnContinuationBoundary CreateCommittedBoundary(OutputProcessingResult? decision = null, bool requiresOutput = false)
        {
            var turnId = new TurnId(Guid.Parse("66666666-6666-6666-6666-666666666666"));
            var requestId = new ModelRequestId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
            var response = TestFactory.Response(requestId, [new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed);
            var message = new AssistantMessage(new MessageId(Guid.Parse("88888888-8888-8888-8888-888888888888")), _agentId, _sessionId, null, _branchId, _runId, turnId, DateTimeOffset.UnixEpoch, MessageState.Complete, response.Parts, new AssistantResponseMetadata(response.RequestId, response.Identity, response.StopReason, null, response.Usage, ExtensionData.Empty), ExtensionData.Empty);
            return new CommittedTurnContinuationBoundary(message, [], decision, requiresOutput);
        }

        /// <summary>Creates promotion evidence correlated with this fixture's committed turn.</summary>
        public InputPromotionSnapshot CreatePromotion(TurnId turnId) => new(_agentId, _sessionId, _laneId, new InRunOperationCorrelation(_operationId, _runId, turnId), _revision, new SessionBranchCursor(_branchId, new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))), _cutoff, expectedVersion: null, expectedFencingToken: null, PromotionBoundary.AfterTurnCommitted, turnId, new TurnId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), [new AdmissionId(Guid.Parse("99999999-9999-9999-9999-999999999999"))]);
    }

    [Fact]
    public async Task DecideAsync_WhenClockMeasuresElapsedTime_RecordsMeasuredDurationAndBoundedDimensions()
    {
        var fixture = new Fixture();
        var clock = new SequencedTimeProvider([100, 350]);
        var durations = new List<double>();
        var tagSets = new List<KeyValuePair<string, object?>[]>();
        using var listener = MeterListenerForContinuation(onCount: (_, tags) => tagSets.Add(tags.ToArray()), onDuration: (measurement, _) => durations.Add(measurement));
        var policy = new DefaultRunContinuationPolicy(clock);
        var decision = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        durations.ShouldHaveSingleItem().ShouldBe(0.25);
        var tags = tagSets.ShouldHaveSingleItem();
        tags.Select(static tag => tag.Key).ShouldBe([AgentKitTagNames.ContinuationBoundary, AgentKitTagNames.Outcome], ignoreOrder: true);
        tags.ShouldAllBe(static tag => tag.Value is string);
    }

    [Fact]
    public async Task DecideAsync_WhenInitialClockMeasurementThrows_PreservesDecisionAndDoesNotFabricateDuration()
    {
        var fixture = new Fixture();
        var durations = 0;
        var counts = 0L;
        using var listener = MeterListenerForContinuation(onCount: (measurement, _) => counts += measurement, onDuration: (_, _) => durations++);
        var policy = new DefaultRunContinuationPolicy(new ThrowingTimeProvider(throwOnCall: 1));
        var decision = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        counts.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_WhenElapsedClockMeasurementThrows_PreservesDecisionAndDoesNotRecordDuration()
    {
        var fixture = new Fixture();
        var durations = 0;
        var counts = 0L;
        using var listener = MeterListenerForContinuation(onCount: (measurement, _) => counts += measurement, onDuration: (_, _) => durations++);
        var policy = new DefaultRunContinuationPolicy(new ThrowingTimeProvider(throwOnCall: 2));
        var decision = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        counts.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_WhenLoggerThrows_PreservesSemanticDecision()
    {
        var fixture = new Fixture();
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System, new ThrowingLogger());
        var decision = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    [Fact]
    public async Task DecideAsync_WhenMetricCallbackThrows_PreservesSemanticDecision()
    {
        var fixture = new Fixture();
        using var listener = MeterListenerForContinuation(onCount: static (_, _) => throw new InvalidOperationException("observer"), onDuration: null);
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System);
        var decision = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    [Fact]
    public async Task DecideAsync_WhenCancelledAndLoggerThrows_PreservesOriginalCancellationToken()
    {
        var fixture = new Fixture();
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System, new ThrowingLogger());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task DecideAsync_WhenObserved_EmitsContentFreeLogActivityAndMetrics()
    {
        var fixture = new Fixture();
        var logger = new RecordingLogger();
        Activity? stopped = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllDefaultRunContinuationPolicyObservability,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(activityListener);
        List<KeyValuePair<string, object?>> metricTags = [];
        using var meterListener = MeterListenerForContinuation(onCount: (_, tags) => metricTags.AddRange(tags.ToArray()), onDuration: null);
        var retry = new ExplicitPolicyContinuationCause("contains-sensitive-test-marker");
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System, logger);
        _ = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), [retry]), TestContext.Current.CancellationToken);
        logger.Messages.ShouldHaveSingleItem().ShouldNotContain("contains-sensitive-test-marker");
        var activity = stopped.ShouldNotBeNull();
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("contains-sensitive-test-marker");
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.ContinuationBoundary, AgentKitTagNames.Outcome], ignoreOrder: true);
        metricTags.ShouldAllBe(static tag => tag.Value is string);
    }

    [Fact]
    public async Task DecideAsync_WhenDecisionsReachTerminalObservation_ReportsTruthfulActivityStatus()
    {
        var fixture = new Fixture();
        List<Activity> stopped = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllDefaultRunContinuationPolicyObservability,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var policy = new DefaultRunContinuationPolicy(TimeProvider.System);
        _ = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), []), TestContext.Current.CancellationToken);
        _ = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), [new ExplicitPolicyContinuationCause("follow-up")]), TestContext.Current.CancellationToken);
        _ = await policy.DecideAsync(fixture.CreateContext(new IdleContinuationBoundary(), [], new AgentRunTurnLimitReached(2)), TestContext.Current.CancellationToken);
        var rejected = new OutputRejected(new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "safe rejection", []));
        _ = await policy.DecideAsync(fixture.CreateContext(fixture.CreateCommittedBoundary(rejected, requiresOutput: true), []), TestContext.Current.CancellationToken);
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

    private static MeterListener MeterListenerForContinuation(Action<long, ReadOnlySpan<KeyValuePair<string, object?>>>? onCount, Action<double, ReadOnlySpan<KeyValuePair<string, object?>>>? onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.RunContinuationEvaluationCount or AgentKitMetricNames.RunContinuationEvaluationDuration)
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

    private static ActivitySamplingResult SampleAllDefaultRunContinuationPolicyObservability(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
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
            return _calls == throwOnCall ? throw new InvalidOperationException("clock") : _calls * 100;
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultRunContinuationPolicy>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger");
    }

    private sealed class RecordingLogger: ILogger<DefaultRunContinuationPolicy>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
