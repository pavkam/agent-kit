// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;



/// <summary>Verifies DefaultHumanQuestionBroker behavior and contracts.</summary>
public sealed class DefaultHumanQuestionBrokerTests
{
    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultHumanQuestionBroker(new RecordingGrantStore(), new RecordingQuestionChannel(), null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task AskAsync_WhenGrantMatches_ConsumesFreshExactEvidenceBeforePublishingGrantFreePrompt()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000001")));
        var broker = new DefaultHumanQuestionBroker(store, channel, intentIds);
        var request = Request(broker.SecurityAudience);
        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<HumanQuestionAnswered>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(broker.SecurityAudience);
        enforcement.Resources.ShouldBe([HumanQuestionSecurityBinding.Resource(request.Id)]);
        enforcement.InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(intentIds.Create());
        var prompt = channel.Prompts.ShouldHaveSingleItem();
        prompt.Id.ShouldBe(request.Id);
        prompt.Prompt.ShouldBe(request.Prompt);
    }

    [Fact]
    public async Task AskAsync_WhenGrantCannotBeConsumed_PerformsNoPublication()
    {
        var store = new RecordingGrantStore
        {
            Status = GrantConsumptionStatus.Exhausted
        };
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var result = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<HumanQuestionUnavailable>().SafeMessage.ShouldBe("Grant Exhausted.");
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Reconciled, true, true)]
    [InlineData(GrantConsumptionStatus.Consumed, false, true)]
    [InlineData(GrantConsumptionStatus.Consumed, true, false)]
    public async Task AskAsync_WhenReceiptDoesNotAuthorizeFreshIntent_PerformsNoPublication(GrantConsumptionStatus status, bool includeReceipt, bool exactReceipt)
    {
        var store = new RecordingGrantStore
        {
            Status = status,
            IncludeReceipt = includeReceipt,
            ReturnExactReceipt = exactReceipt,
        };
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var result = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<HumanQuestionUnavailable>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    public async Task AskAsync_WhenCapturedAuthorizationDoesNotMatch_DeniesBeforeIntentConsumptionOrPublication(string mismatch)
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000004")));
        var broker = new DefaultHumanQuestionBroker(store, channel, intentIds);
        var request = Request(broker.SecurityAudience, captured: true);
        var mismatched = mismatch == "scope" ? request with
        {
            AgentId = new AgentId(Guid.Parse("90000000-0000-0000-0000-000000000005"))
        }

        : request with
        {
            Identity = TestSupport.TestExecutionIdentity.Create(new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human),
        };
        var result = await broker.AskAsync(mismatched, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<HumanQuestionUnavailable>().SafeMessage.ShouldBe("The captured authorization does not match the question publication.");
        intentIds.Calls.ShouldBe(0);
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenRequestChangesAfterAuthorization_FailsClosedBeforePublication()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var request = Request(broker.SecurityAudience) with
        {
            Prompt = "A different question."
        };
        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<HumanQuestionUnavailable>().SafeMessage.ShouldBe("Grant Mismatch.");
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenCallerCancelsDuringNonCooperativeConsumption_PropagatesBeforePublication()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingGrantStore
        {
            OnConsume = cancellation.Cancel
        };
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var action = async () => await broker.AskAsync(Request(broker.SecurityAudience), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenAlreadyCancelled_PerformsNoGrantConsumptionOrPublication()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var action = () => broker.AskAsync(Request(broker.SecurityAudience), cancellation.Token).AsTask();
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationBeforePublication()
    {
        var store = new InMemorySecurityGrantStore(new FixedTimeProvider());
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(store, channel);
        var request = Request(broker.SecurityAudience, captured: true);
        await store.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        var result = await broker.AskAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<HumanQuestionAnswered>();
        channel.Prompts.ShouldHaveSingleItem().Id.ShouldBe(request.Id);
    }

    [Fact]
    public async Task AskAsync_WhenAnswered_EmitsSafeCorrelatedActivityAndBoundedMetrics()
    {
        Activity? stopped = null;
        var countMeasurements = 0;
        var durationMeasurements = 0;
        var metricTags = new ConcurrentQueue<KeyValuePair<string, object?>>();
        using var parent = new Activity("human-question-publication-test").Start();
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.HumanQuestionPublicationCount or AgentKitMetricNames.HumanQuestionPublicationDuration)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        metrics.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.HumanQuestionPublicationCount && Activity.Current?.TraceId == parent.Context.TraceId)
            {
                _ = Interlocked.Add(ref countMeasurements, checked((int) measurement));
                foreach (var tag in tags)
                {
                    metricTags.Enqueue(tag);
                }
            }
        });
        metrics.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.HumanQuestionPublicationDuration && Activity.Current?.TraceId == parent.Context.TraceId)
            {
                _ = Interlocked.Increment(ref durationMeasurements);
                foreach (var tag in tags)
                {
                    metricTags.Enqueue(tag);
                }
            }
        });
        metrics.Start();
        var logger = new RecordingQuestionLogger();
        var request = Request(new ComponentId("agentkit.io.human-question"));
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore(), new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.NewGuid())), new FixedTimeProvider(), logger);
        _ = await broker.AskAsync(request, TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.HumanQuestionPublish);
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.QuestionId).ShouldBe(request.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe("tenant");
        activity.GetTagItem(AgentKitTagNames.TurnId).ShouldBeNull();
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}")).ShouldNotContain(request.Prompt);
        logger.Events.ShouldHaveSingleItem().ShouldBe((1003, LogLevel.Information));
        logger.FieldNames.ShouldHaveSingleItem().ShouldBe(["QuestionId", "TenantId", "AgentId", "SessionId", "RunId", "TurnId", "ToolCallId", "OperationId", "SecurityRequestId", "Outcome", "{OriginalFormat}"]);
        logger.Messages.ShouldAllBe(message => !message.Contains(request.Prompt, StringComparison.Ordinal));
        Volatile.Read(ref countMeasurements).ShouldBe(1);
        Volatile.Read(ref durationMeasurements).ShouldBe(1);
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.Outcome]);
        metricTags.Any(static tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "answered")).ShouldBeTrue();
    }

    [Fact]
    public async Task AskAsync_WhenCorrelationHasTurn_EmitsEstablishedTenantAndTurnTags()
    {
        Activity? stopped = null;
        using var parent = new Activity("human-question-turn-correlation-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore(), new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000019"))), new FixedTimeProvider(), new RecordingQuestionLogger());
        var turnId = new TurnId(Guid.Parse("90000000-0000-0000-0000-000000000020"));
        _ = await broker.AskAsync(Request(broker.SecurityAudience, turnId: turnId), TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe("tenant");
        activity.GetTagItem(AgentKitTagNames.TurnId).ShouldBe(turnId.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void RecordHumanQuestionPublication_WhenElapsedIsNullOrZero_RecordsDefinedOutcome(int? elapsedMilliseconds)
    {
        using var parent = new Activity("human-question-metrics-valid-test").Start();
        using var measurementScope = new Activity("human-question-metrics-valid-operation").Start();
        using var metrics = new MeterListener();
        var measurements = new ConcurrentQueue<long>();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == AgentKitMetricNames.HumanQuestionPublicationCount)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        metrics.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            if (Activity.Current?.TraceId == parent.Context.TraceId)
            {
                measurements.Enqueue(measurement);
            }
        });
        metrics.Start();
        IOMetrics.RecordHumanQuestionPublication(HumanQuestionPublicationOutcome.Answered, elapsedMilliseconds is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null);
        measurements.ShouldBe([1L]);
    }

    [Fact]
    public void RecordHumanQuestionPublication_WhenArgumentsAreInvalid_ThrowsBeforeMeasurement()
    {
        using var parent = new Activity("human-question-metrics-invalid-test").Start();
        using var measurementScope = new Activity("human-question-metrics-invalid-operation").Start();
        using var metrics = new MeterListener();
        var measurements = new ConcurrentQueue<long>();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == AgentKitMetricNames.HumanQuestionPublicationCount)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        metrics.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            if (Activity.Current?.TraceId == parent.Context.TraceId)
            {
                measurements.Enqueue(measurement);
            }
        });
        metrics.Start();
        var outcomeException = Should.Throw<ArgumentOutOfRangeException>(() => IOMetrics.RecordHumanQuestionPublication((HumanQuestionPublicationOutcome) (-1), null));
        var elapsedException = Should.Throw<ArgumentOutOfRangeException>(() => IOMetrics.RecordHumanQuestionPublication(HumanQuestionPublicationOutcome.Answered, TimeSpan.FromTicks(-1)));
        outcomeException.ParamName.ShouldBe("outcome");
        elapsedException.ParamName.ShouldBe("elapsed");
        measurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenObserversThrow_PreservesSemanticPublication()
    {
        using var parent = new Activity("human-question-observer-test").Start();
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => ThrowForParent(ref options, parent.Context.TraceId),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == AgentKitMetricNames.HumanQuestionPublicationCount)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        metrics.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.TraceId == parent.Context.TraceId)
            {
                throw new InvalidOperationException("observer");
            }
        });
        metrics.Start();
        var channel = new RecordingQuestionChannel();
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore(), channel, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.NewGuid())), new FixedTimeProvider(), new ThrowingLogger());
        var result = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<HumanQuestionAnswered>();
        _ = channel.Prompts.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AskAsync_WhenCancelled_EmitsCancelledObservationAndPreservesCallerCancellation()
    {
        Activity? stopped = null;
        using var parent = new Activity("human-question-cancellation-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingQuestionLogger();
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore(), new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.NewGuid())), new FixedTimeProvider(), logger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await broker.AskAsync(Request(broker.SecurityAudience), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        logger.Events.ShouldHaveSingleItem().ShouldBe((1004, LogLevel.Information));
    }

    [Fact]
    public async Task AskAsync_WhenGrantStoreFaults_LogsOnlyErrorTypeAndRethrows()
    {
        Activity? stopped = null;
        using var parent = new Activity("human-question-fault-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingQuestionLogger();
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore { OnConsume = static () => throw new InvalidOperationException("raw channel-adjacent detail") }, new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.NewGuid())), new FixedTimeProvider(), logger);
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken));
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        logger.Events.ShouldHaveSingleItem().ShouldBe((1005, LogLevel.Error));
        logger.Messages.ShouldAllBe(message => !message.Contains("raw channel-adjacent detail", StringComparison.Ordinal));
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((HumanQuestionPublicationOutcome) (-1)).ToStableValue());
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public async Task AskAsync_WhenGrantIsDenied_EmitsTypedDenialWithoutPromptContent()
    {
        Activity? stopped = null;
        using var parent = new Activity("human-question-denial-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingQuestionLogger();
        var request = Request(new ComponentId("agentkit.io.human-question"));
        var result = await new DefaultHumanQuestionBroker(new RecordingGrantStore { Status = GrantConsumptionStatus.Exhausted }, new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000011"))), new FixedTimeProvider(), logger).AskAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<HumanQuestionUnavailable>();
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("grant_denied");
        logger.Events.ShouldHaveSingleItem().ShouldBe((1003, LogLevel.Information));
        logger.Messages.ShouldAllBe(message => !message.Contains(request.Prompt, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AskAsync_WhenCallerCancelsAfterNonCooperativeChannel_ReturnsCancellationAfterPublication()
    {
        Activity? stopped = null;
        using var parent = new Activity("human-question-post-channel-cancellation-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        var channel = new RecordingQuestionChannel
        {
            OnAsk = cancellation.Cancel
        };
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore(), channel, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000012"))), new FixedTimeProvider(), new RecordingQuestionLogger());
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await broker.AskAsync(Request(broker.SecurityAudience), cancellation.Token));
        _ = channel.Prompts.ShouldHaveSingleItem();
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }

    [Fact]
    public async Task AskAsync_WhenChannelReturnsDifferentQuestionId_PreservesResultAndEmitsFailedObservation()
    {
        Activity? stopped = null;
        using var parent = new Activity("human-question-result-mismatch-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.HumanQuestionPublish && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var expected = new HumanQuestionUnavailable(new QuestionId(Guid.Parse("90000000-0000-0000-0000-000000000013")), "Channel returned a different correlation.");
        var channel = new RecordingQuestionChannel
        {
            Result = _ => expected
        };
        var logger = new RecordingQuestionLogger();
        var broker = new DefaultHumanQuestionBroker(new RecordingGrantStore(), channel, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000014"))), new FixedTimeProvider(), logger);
        var actual = await broker.AskAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        actual.ShouldBeSameAs(expected);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        logger.Events.ShouldHaveSingleItem().ShouldBe((1003, LogLevel.Error));
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultHumanQuestionBroker(new RecordingGrantStore(), new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.NewGuid())), null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultHumanQuestionBroker>.Instance));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultHumanQuestionBroker(new RecordingGrantStore(), new RecordingQuestionChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.NewGuid())), new FixedTimeProvider(), null!));
        exception.ParamName.ShouldBe("logger");
    }

    private static ActivitySamplingResult SampleForParent(ref ActivityCreationOptions<ActivityContext> options, ActivityTraceId parentTraceId) => options.Parent.TraceId == parentTraceId ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
    private static ActivitySamplingResult ThrowForParent(ref ActivityCreationOptions<ActivityContext> options, ActivityTraceId parentTraceId) => options.Parent.TraceId == parentTraceId ? throw new InvalidOperationException("observer") : ActivitySamplingResult.None;
    private static HumanQuestionRequest Request(ComponentId audience, bool captured = false, TurnId? turnId = null)
    {
        var id = new QuestionId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var agentId = new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var sessionId = new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var toolCallId = new ToolCallId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new RunId(Guid.Parse("60000000-0000-0000-0000-000000000006")), turnId);
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        ImmutableArray<HumanQuestionOption> options = [new(new QuestionOptionId("one"), "One", "First option."), new(new QuestionOptionId("two"), "Two", "Second option."),];
        var deadline = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var fingerprint = HumanQuestionSecurityBinding.Fingerprint(id, "Choose.", options, false, deadline);
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        var policyVersion = new SecurityPolicyVersion(1);
        var authorization = captured ? new SecurityAuthorizationContext(new SecurityProfileKey("test"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("90000000-0000-0000-0000-000000000003")), policyVersion, new ContentHash("sha256:test-policy")), new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), scope, identity) : null;
        var grant = authorization is { } context ? new SecurityGrant(new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")), new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")), scope, identity, context, audience, SecurityOperationKind.StateMutation, SecurityEffect.Create, [HumanQuestionSecurityBinding.Resource(id)], fingerprint, policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, deadline, 1) : new SecurityGrant(new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")), new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")), scope, identity, audience, SecurityOperationKind.StateMutation, SecurityEffect.Create, [HumanQuestionSecurityBinding.Resource(id)], fingerprint, policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, deadline, 1);
        return new HumanQuestionRequest(id, agentId, sessionId, toolCallId, correlation, identity, "Choose.", options, false, deadline, grant);
    }
}
