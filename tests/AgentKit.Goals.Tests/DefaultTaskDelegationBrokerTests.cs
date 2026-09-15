// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;



/// <summary>Verifies DefaultTaskDelegationBroker behavior and contracts.</summary>
public sealed class DefaultTaskDelegationBrokerTests
{
    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultTaskDelegationBroker(new RecordingGrantStore(), new RecordingDelegationChannel(), null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task DelegateAsync_WhenGrantMatches_ConsumesFreshExactEvidenceBeforeGrantFreeDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var intentId = new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-00000000000e"));
        var broker = new DefaultTaskDelegationBroker(store, channel, new FixedSecurityEnforcementIntentIdGenerator(intentId));
        var request = Request(broker.SecurityAudience);
        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<TaskDelegationChildResult>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Kind.ShouldBe(SecurityOperationKind.Delegation);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Resources.ShouldBe([TaskDelegationSecurityBinding.Resource(request.Prompt.Id)]);
        enforcement.InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(intentId);
        channel.Prompts.ShouldHaveSingleItem().ShouldBe(request.Prompt);
    }

    [Fact]
    public async Task DelegateAsync_WhenGrantCannotBeConsumed_PerformsNoDispatchAndInventsNoChildIdentity()
    {
        var store = new RecordingGrantStore
        {
            Status = GrantConsumptionStatus.Exhausted
        };
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var result = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldBe("Grant Exhausted.");
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Reconciled, true, true)]
    [InlineData(GrantConsumptionStatus.Consumed, false, true)]
    [InlineData(GrantConsumptionStatus.Consumed, true, false)]
    public async Task DelegateAsync_WhenReceiptDoesNotAuthorizeFreshIntent_PerformsNoDispatch(GrantConsumptionStatus status, bool includeReceipt, bool exactReceipt)
    {
        var store = new RecordingGrantStore
        {
            Status = status,
            IncludeReceipt = includeReceipt,
            ReturnExactReceipt = exactReceipt,
        };
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var result = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<TaskDelegationRejected>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    public async Task DelegateAsync_WhenCapturedAuthorizationDoesNotMatch_DeniesBeforeIntentConsumptionOrDispatch(string mismatch)
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var intentIds = new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-00000000000b")));
        var broker = new DefaultTaskDelegationBroker(store, channel, intentIds);
        var request = Request(broker.SecurityAudience, captured: true);
        var mismatched = mismatch == "scope" ? request with
        {
            Prompt = request.Prompt with
            {
                ParentAgentId = new AgentId(Guid.Parse("e0000000-0000-0000-0000-00000000000c")),
            },
        }

        : request with
        {
            Prompt = request.Prompt with
            {
                Identity = TestSupport.TestExecutionIdentity.Create(new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human),
            },
        };
        var result = await broker.DelegateAsync(mismatched, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<TaskDelegationRejected>().SafeMessage.ShouldBe("The captured authorization does not match the task delegation.");
        intentIds.Calls.ShouldBe(0);
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenEnvelopeChangesAfterAuthorization_FailsClosedBeforeDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var request = Request(broker.SecurityAudience);
        request = request with
        {
            Prompt = request.Prompt with
            {
                Objective = "Different objective."
            }
        };
        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<TaskDelegationRejected>();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenCallerCancelsDuringNonCooperativeConsumption_PropagatesBeforeDispatch()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingGrantStore
        {
            OnConsume = cancellation.Cancel
        };
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var action = async () => await broker.DelegateAsync(Request(broker.SecurityAudience), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenAlreadyCancelled_PerformsNoConsumptionOrDispatch()
    {
        var store = new RecordingGrantStore();
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var action = () => broker.DelegateAsync(Request(broker.SecurityAudience), cancellation.Token).AsTask();
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        channel.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationBeforeDispatch()
    {
        var store = new InMemorySecurityGrantStore(new FixedTimeProvider());
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(store, channel);
        var request = Request(broker.SecurityAudience, captured: true);
        await store.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        var result = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<TaskDelegationChildResult>();
        channel.Prompts.ShouldHaveSingleItem().ShouldBe(request.Prompt);
    }

    [Fact]
    public async Task DelegateAsync_WhenDispatched_EmitsSafeCorrelatedActivityLogAndBoundedMetrics()
    {
        Activity? stopped = null;
        var countMeasurements = 0;
        var durationMeasurements = 0;
        var metricTags = new ConcurrentQueue<KeyValuePair<string, object?>>();
        using var parent = new Activity("task-delegation-dispatch-test").Start();
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.TaskDelegationPublicationCount or AgentKitMetricNames.TaskDelegationPublicationDuration)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        metrics.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.TaskDelegationPublicationCount && Activity.Current?.TraceId == parent.Context.TraceId)
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
            if (instrument.Name == AgentKitMetricNames.TaskDelegationPublicationDuration && Activity.Current?.TraceId == parent.Context.TraceId)
            {
                _ = Interlocked.Increment(ref durationMeasurements);
                foreach (var tag in tags)
                {
                    metricTags.Enqueue(tag);
                }
            }
        });
        metrics.Start();
        var logger = new RecordingDelegationLogger();
        var request = Request(new ComponentId("agentkit.goals.delegation"));
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore(), new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000021"))), new FixedTimeProvider(), logger);
        _ = await broker.DelegateAsync(request, TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.TaskDelegationDispatch);
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.DelegationId).ShouldBe(request.Prompt.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe("tenant");
        activity.GetTagItem(AgentKitTagNames.TurnId).ShouldBeNull();
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}")).ShouldNotContain(request.Prompt.Objective);
        logger.Events.ShouldHaveSingleItem().ShouldBe((23000, LogLevel.Information));
        logger.Messages.ShouldAllBe(message => !message.Contains(request.Prompt.Objective, StringComparison.Ordinal));
        Volatile.Read(ref countMeasurements).ShouldBe(1);
        Volatile.Read(ref durationMeasurements).ShouldBe(1);
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.Outcome]);
        metricTags.Any(static tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "dispatched")).ShouldBeTrue();
    }

    [Fact]
    public async Task DelegateAsync_WhenCorrelationHasTurn_EmitsEstablishedTenantAndTurnTags()
    {
        Activity? stopped = null;
        using var parent = new Activity("task-delegation-turn-correlation-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore(), new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000033"))), new FixedTimeProvider(), new RecordingDelegationLogger());
        var turnId = new TurnId(Guid.Parse("e0000000-0000-0000-0000-000000000034"));
        _ = await broker.DelegateAsync(Request(broker.SecurityAudience, turnId: turnId), TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe("tenant");
        activity.GetTagItem(AgentKitTagNames.TurnId).ShouldBe(turnId.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void RecordTaskDelegationPublication_WhenElapsedIsNullOrZero_RecordsDefinedOutcome(int? elapsedMilliseconds)
    {
        using var parent = new Activity("task-delegation-metrics-valid-test").Start();
        using var measurementScope = new Activity("task-delegation-metrics-valid-operation").Start();
        using var metrics = new MeterListener();
        var measurements = new ConcurrentQueue<long>();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == AgentKitMetricNames.TaskDelegationPublicationCount)
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
        GoalsMetrics.RecordTaskDelegationPublication(TaskDelegationPublicationOutcome.Dispatched, elapsedMilliseconds is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null);
        measurements.ShouldBe([1L]);
    }

    [Fact]
    public void RecordTaskDelegationPublication_WhenArgumentsAreInvalid_ThrowsBeforeMeasurement()
    {
        using var parent = new Activity("task-delegation-metrics-invalid-test").Start();
        using var measurementScope = new Activity("task-delegation-metrics-invalid-operation").Start();
        using var metrics = new MeterListener();
        var measurements = new ConcurrentQueue<long>();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == AgentKitMetricNames.TaskDelegationPublicationCount)
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
        var outcomeException = Should.Throw<ArgumentOutOfRangeException>(() => GoalsMetrics.RecordTaskDelegationPublication((TaskDelegationPublicationOutcome) (-1), null));
        var elapsedException = Should.Throw<ArgumentOutOfRangeException>(() => GoalsMetrics.RecordTaskDelegationPublication(TaskDelegationPublicationOutcome.Dispatched, TimeSpan.FromTicks(-1)));
        outcomeException.ParamName.ShouldBe("outcome");
        elapsedException.ParamName.ShouldBe("elapsed");
        measurements.ShouldBeEmpty();
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((TaskDelegationPublicationOutcome) (-1)).ToStableValue());
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public async Task DelegateAsync_WhenObserversThrow_PreservesSemanticDispatch()
    {
        using var parent = new Activity("task-delegation-observer-test").Start();
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => ThrowForParent(ref options, parent.Context.TraceId),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == AgentKitMetricNames.TaskDelegationPublicationCount)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        metrics.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.TraceId == parent.Context.TraceId)
            {
                throw new InvalidOperationException("observer");
            }
        });
        metrics.Start();
        var channel = new RecordingDelegationChannel();
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore(), channel, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000022"))), new FixedTimeProvider(), new ThrowingDelegationLogger());
        var result = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<TaskDelegationChildResult>();
        _ = channel.Prompts.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DelegateAsync_WhenCancelled_EmitsCancelledObservationAndPreservesCallerCancellation()
    {
        Activity? stopped = null;
        using var parent = new Activity("task-delegation-cancellation-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingDelegationLogger();
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore(), new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000023"))), new FixedTimeProvider(), logger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await broker.DelegateAsync(Request(broker.SecurityAudience), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        logger.Events.ShouldHaveSingleItem().ShouldBe((23001, LogLevel.Information));
    }

    [Fact]
    public async Task DelegateAsync_WhenGrantStoreFaults_LogsOnlyErrorTypeAndRethrows()
    {
        Activity? stopped = null;
        using var parent = new Activity("task-delegation-fault-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingDelegationLogger();
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore { OnConsume = static () => throw new InvalidOperationException("raw child dispatch detail") }, new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000024"))), new FixedTimeProvider(), logger);
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken));
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        logger.Events.ShouldHaveSingleItem().ShouldBe((23002, LogLevel.Error));
        logger.Messages.ShouldAllBe(message => !message.Contains("raw child dispatch detail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DelegateAsync_WhenGrantIsDenied_EmitsTypedDenialWithoutDelegationContent()
    {
        Activity? stopped = null;
        using var parent = new Activity("task-delegation-denial-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingDelegationLogger();
        var request = Request(new ComponentId("agentkit.goals.delegation"));
        var result = await new DefaultTaskDelegationBroker(new RecordingGrantStore { Status = GrantConsumptionStatus.Exhausted }, new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000027"))), new FixedTimeProvider(), logger).DelegateAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<TaskDelegationRejected>();
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("grant_denied");
        logger.Events.ShouldHaveSingleItem().ShouldBe((23000, LogLevel.Information));
        logger.Messages.ShouldAllBe(message => !message.Contains(request.Prompt.Objective, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DelegateAsync_WhenCallerCancelsAfterNonCooperativeChannel_ReturnsCancellationAfterDispatch()
    {
        Activity? stopped = null;
        using var parent = new Activity("task-delegation-post-channel-cancellation-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        var channel = new RecordingDelegationChannel
        {
            OnDelegate = cancellation.Cancel
        };
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore(), channel, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000028"))), new FixedTimeProvider(), new RecordingDelegationLogger());
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await broker.DelegateAsync(Request(broker.SecurityAudience), cancellation.Token));
        _ = channel.Prompts.ShouldHaveSingleItem();
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }

    [Fact]
    public async Task DelegateAsync_WhenChannelReturnsDifferentDelegationId_PreservesResultAndEmitsFailedObservation()
    {
        Activity? stopped = null;
        using var parent = new Activity("task-delegation-result-mismatch-test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => SampleForParent(ref options, parent.Context.TraceId),
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.TaskDelegationDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var expected = new TaskDelegationRejected(new DelegationId(Guid.Parse("e0000000-0000-0000-0000-000000000029")), "Channel returned a different correlation.");
        var channel = new RecordingDelegationChannel
        {
            Result = _ => expected
        };
        var logger = new RecordingDelegationLogger();
        var broker = new DefaultTaskDelegationBroker(new RecordingGrantStore(), channel, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000030"))), new FixedTimeProvider(), logger);
        var actual = await broker.DelegateAsync(Request(broker.SecurityAudience), TestContext.Current.CancellationToken);
        actual.ShouldBeSameAs(expected);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
        logger.Events.ShouldHaveSingleItem().ShouldBe((23000, LogLevel.Error));
    }

    [Fact]
    public void Constructor_WhenGrantStoreIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultTaskDelegationBroker(null!, new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000031"))), new FixedTimeProvider(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultTaskDelegationBroker>.Instance));
        exception.ParamName.ShouldBe("grantStore");
    }

    [Fact]
    public void Constructor_WhenChannelIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultTaskDelegationBroker(new RecordingGrantStore(), null!, new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000032"))), new FixedTimeProvider(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultTaskDelegationBroker>.Instance));
        exception.ParamName.ShouldBe("channel");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultTaskDelegationBroker(new RecordingGrantStore(), new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000025"))), null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultTaskDelegationBroker>.Instance));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultTaskDelegationBroker(new RecordingGrantStore(), new RecordingDelegationChannel(), new FixedSecurityEnforcementIntentIdGenerator(new SecurityEnforcementIntentId(Guid.Parse("e0000000-0000-0000-0000-000000000026"))), new FixedTimeProvider(), null!));
        exception.ParamName.ShouldBe("logger");
    }

    private static ActivitySamplingResult SampleForParent(ref ActivityCreationOptions<ActivityContext> options, ActivityTraceId parentTraceId) => options.Parent.TraceId == parentTraceId ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
    private static ActivitySamplingResult ThrowForParent(ref ActivityCreationOptions<ActivityContext> options, ActivityTraceId parentTraceId) => options.Parent.TraceId == parentTraceId ? throw new InvalidOperationException("observer") : ActivitySamplingResult.None;
    private static TaskDelegationRequest Request(ComponentId audience, bool captured = false, TurnId? turnId = null)
    {
        var prompt = new TaskDelegationPrompt(new DelegationId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")), new InRunOperationCorrelation(new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")), turnId), new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), new AgentId(Guid.Parse("70000000-0000-0000-0000-000000000007")), "Implement the parser.", ["Tests pass."], [new ToolId("read")], new TaskDelegationBudget(10, 20), DateTimeOffset.UnixEpoch.AddMinutes(5));
        var scope = new SecurityAuthorizationScope(prompt.ParentAgentId, prompt.ParentSessionId, prompt.Correlation);
        var policyVersion = new SecurityPolicyVersion(1);
        var authorization = captured ? new SecurityAuthorizationContext(new SecurityProfileKey("test"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-00000000000d")), policyVersion, new ContentHash("sha256:test-policy")), new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), scope, prompt.Identity) : null;
        var grant = authorization is { } context ? new SecurityGrant(new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")), new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")), scope, prompt.Identity, context, audience, SecurityOperationKind.Delegation, SecurityEffect.Create, [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt), policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1) : new SecurityGrant(new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")), new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")), scope, prompt.Identity, audience, SecurityOperationKind.Delegation, SecurityEffect.Create, [TaskDelegationSecurityBinding.Resource(prompt.Id)], TaskDelegationSecurityBinding.Fingerprint(prompt), policyVersion, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, prompt.Deadline, 1);
        return new TaskDelegationRequest(prompt, grant);
    }
}
