// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Verifies DefaultSecurityAuditDispatcher behavior and contracts.</summary>
public sealed class DefaultSecurityAuditDispatcherTests: SecurityAuditDispatcherConformanceTests<DefaultSecurityAuditDispatcherConformanceFixture>
{
    [Fact]
    public async Task DispatchAsync_WhenRequiredDeliveryHasNoCompatibleDurableSink_ReturnsUnavailableBeforeWriting()
    {
        var sink = new RecordingSink();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.BestEffort, durable: false, sink)]);
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditUnavailable>();
        sink.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenALaterRequiredSinkCannotProveDurableAcceptance_ReturnsUnavailableAfterEarlierWrites()
    {
        var durableSink = new RecordingSink();
        var nonDurableRequiredSink = new RecordingSink();
        var dispatcher = Dispatcher(
            SecurityAuditDelivery.Required,
            [
                Binding(SecurityAuditDelivery.Required, durable: true, durableSink),
                Binding(SecurityAuditDelivery.Required, durable: false, nonDurableRequiredSink),
            ]);
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditUnavailable>();
        _ = durableSink.Records.ShouldHaveSingleItem();
        nonDurableRequiredSink.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenConfiguredRequiredSinkFails_ReturnsFailed()
    {
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new ThrowingSink())]);
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditFailed>();
    }

    [Fact]
    public async Task DispatchAsync_WhenOnlyBestEffortDurableSinkFails_DoesNotReportAcceptedUnderGlobalRequiredPolicy()
    {
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.BestEffort, durable: true, new ThrowingSink())]);
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditFailed>();
    }

    [Fact]
    public async Task DispatchAsync_WhenOptionalSinkFails_IsolatesTheObserverFailure()
    {
        var dispatcher = Dispatcher(SecurityAuditDelivery.BestEffort, [Binding(SecurityAuditDelivery.BestEffort, durable: false, new ThrowingSink())]);
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
    }

    [Fact]
    public async Task DispatchAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, []);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatcher.DispatchAsync(Record(), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task DispatchAsync_WhenCompletedSinkCancels_DoesNotInvokeLaterSinksAndPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var laterSink = new RecordingSink();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new CancellingSink(cancellation)), Binding(SecurityAuditDelivery.Required, durable: true, laterSink),]);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatcher.DispatchAsync(Record(), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        laterSink.Records.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task DispatchAsync_WhenSinkCancelsAndFails_PropagatesCancellationWithoutBeginningAnotherSink(bool required, bool includeLaterSink)
    {
        using var cancellation = new CancellationTokenSource();
        var laterSink = includeLaterSink ? new RecordingSink() : null;
        var bindings = new List<SecurityAuditSinkBinding>
        {
            Binding(required ? SecurityAuditDelivery.Required : SecurityAuditDelivery.BestEffort, durable: required, new CancellingAndThrowingSink(cancellation)),
        };
        if (laterSink is not null)
        {
            bindings.Add(Binding(SecurityAuditDelivery.Required, durable: true, laterSink));
        }

        var dispatcher = Dispatcher(includeLaterSink ? SecurityAuditDelivery.Required : SecurityAuditDelivery.BestEffort, bindings);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatcher.DispatchAsync(Record(), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        laterSink?.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenOptionalSinkTimesOut_ContinuesToTheLaterRequiredDurableSink()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var blocking = new BlockingSink();
        var durable = new RecordingSink();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.BestEffort, durable: false, blocking), Binding(SecurityAuditDelivery.Required, durable: true, durable),], timeProvider: timeProvider, deliveryTimeout: TimeSpan.FromSeconds(1));
        var dispatch = dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var result = await dispatch;
        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        _ = durable.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DispatchAsync_WhenOnlyBestEffortDurableSinkTimesOutUnderGlobalRequired_ReturnsAmbiguousTimeout()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var blocking = new BlockingSink();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.BestEffort, durable: true, blocking)], timeProvider: timeProvider, deliveryTimeout: TimeSpan.FromSeconds(1));
        var dispatch = dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var result = await dispatch;
        _ = result.ShouldBeOfType<SecurityAuditTimedOut>();
        blocking.DeliveryCancellation.IsCancellationRequested.ShouldBeTrue();
        blocking.Complete();
    }

    [Fact]
    public async Task DispatchAsync_WhenRequiredSinkTimesOut_DoesNotInvokeLaterSinkAndReturnsAmbiguousTimeout()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var blocking = new BlockingSink();
        var laterSink = new RecordingSink();
        Activity? stopped = null;
        ConcurrentQueue<string> outcomes = [];
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForAudit((_, tags) => outcomes.Enqueue(OutcomeFrom(tags)), static (_, _) =>
        {
        });
        var logger = new RecordingLogger();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, blocking), Binding(SecurityAuditDelivery.Required, durable: true, laterSink),], timeProvider: timeProvider, logger: logger, deliveryTimeout: TimeSpan.FromSeconds(1));
        var dispatch = dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var result = await dispatch;
        result.ShouldBeOfType<SecurityAuditTimedOut>().SafeReason.ShouldContain("unknown");
        laterSink.Records.ShouldBeEmpty();
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("timed_out");
        outcomes.ShouldBe(["timed_out"]);
        logger.EventIds.ShouldContain(new EventId(5024));
        blocking.DeliveryCancellation.IsCancellationRequested.ShouldBeTrue();
        blocking.Complete();
    }

    [Fact]
    public async Task DispatchAsync_WhenBestEffortSinkCompletesJustAfterItsDeadline_ContinuesWithoutThrowing()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var deliveryTimeout = TimeSpan.FromSeconds(1);
        var laterSink = new RecordingSink();
        var logger = new RecordingLogger();
        var dispatcher = Dispatcher(
            SecurityAuditDelivery.Required,
            [
                Binding(SecurityAuditDelivery.BestEffort, durable: false, new DeadlineRacingSink(timeProvider, deliveryTimeout)),
                Binding(SecurityAuditDelivery.Required, durable: true, laterSink),
            ],
            timeProvider: timeProvider,
            logger: logger,
            deliveryTimeout: deliveryTimeout);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        _ = laterSink.Records.ShouldHaveSingleItem();
        logger.EventIds.ShouldContain(new EventId(5015));
    }

    [Fact]
    public async Task DispatchAsync_WhenRequiredSinkCompletesJustAfterItsDeadline_ReturnsAmbiguousTimeout()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var deliveryTimeout = TimeSpan.FromSeconds(1);
        var laterSink = new RecordingSink();
        var dispatcher = Dispatcher(
            SecurityAuditDelivery.Required,
            [
                Binding(SecurityAuditDelivery.Required, durable: true, new DeadlineRacingSink(timeProvider, deliveryTimeout)),
                Binding(SecurityAuditDelivery.Required, durable: true, laterSink),
            ],
            timeProvider: timeProvider,
            deliveryTimeout: deliveryTimeout);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SecurityAuditTimedOut>().SafeReason.ShouldContain("unknown");
        laterSink.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenASinkThrowsSynchronouslyAfterExternallyTriggeredCancellation_PropagatesWithoutFaultingLateObservation()
    {
        using var cancellation = new CancellationTokenSource();
        var dispatcher = Dispatcher(
            SecurityAuditDelivery.Required,
            [Binding(SecurityAuditDelivery.Required, durable: true, new SynchronouslyCancelingSink(cancellation))]);

        var exception = await Should.ThrowAsync<OperationCanceledException>(
            async () => await dispatcher.DispatchAsync(Record(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task DispatchAsync_WhenElapsedTimeObservationFailsAfterASuccessfulTimestamp_RecordsNoDuration()
    {
        var count = 0L;
        var durations = 0;
        using var meterListener = MeterListenerForAudit((measurement, _) => count += measurement, (_, _) => durations++);
        var dispatcher = Dispatcher(
            SecurityAuditDelivery.Required,
            [Binding(SecurityAuditDelivery.Required, durable: true, new RecordingSink())],
            timeProvider: new ThrowingTimeProvider(throwOnCall: 2));

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        count.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task DispatchAsync_WhenCallerCancelsBeforeDeadline_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var blocking = new BlockingSink();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, blocking)], timeProvider: new FakeTimeProvider(DateTimeOffset.UnixEpoch), deliveryTimeout: TimeSpan.FromSeconds(1));
        var dispatch = dispatcher.DispatchAsync(Record(), cancellation.Token).AsTask();
        await blocking.Started;
        await cancellation.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatch);
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task DispatchAsync_WhenTimedOutSinkFaultsLate_RetainsOneTimeoutOutcomeAndTheOriginalRecordIdentity()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var blocking = new BlockingSink();
        ConcurrentQueue<string> outcomes = [];
        using var meterListener = MeterListenerForAudit((_, tags) => outcomes.Enqueue(OutcomeFrom(tags)), static (_, _) =>
        {
        });
        var record = Record();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, blocking)], timeProvider: timeProvider, deliveryTimeout: TimeSpan.FromSeconds(1));
        var dispatch = dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        _ = await dispatch;
        blocking.Fail();
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await blocking.Completion);
        await Task.Yield();
        blocking.Records.ShouldBe([record]);
        blocking.Records[0].Id.ShouldBe(record.Id);
        outcomes.ShouldBe(["timed_out"]);
    }

    [Fact]
    public async Task DispatchAsync_WhenObserved_EmitsContentFreeParentedActivityAndBoundedMetrics()
    {
        Activity? stopped = null;
        var count = 0L;
        var durations = 0;
        ConcurrentQueue<KeyValuePair<string, object?>> metricTags = [];
        using var parent = new Activity("security-audit-dispatch-parent").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityAuditDispatch && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForAudit((measurement, tags) =>
        {
            count += measurement;
            foreach (var tag in tags)
            {
                metricTags.Enqueue(tag);
            }
        }, (_, _) => durations++);
        var logger = new RecordingLogger();
        var record = Record(fields: [new KeyValuePair<string, RedactedAuditValue>("safe-field", RedactedAuditValue.FromPolicyId(new SecurityPolicyId("sensitive-audit-value")))]);
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new RecordingSink())], logger: logger);
        _ = await dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.SecurityAuditRecordId).ShouldBe(record.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.SecurityAuditEventKind).ShouldBe(record.EventKind.ToString());
        activity.GetTagItem(AgentKitTagNames.SecurityAuditOutcome).ShouldBe(record.Outcome.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("sensitive-audit-value");
        logger.Messages.ShouldAllBe(static message => !message.Contains("sensitive-audit-value", StringComparison.Ordinal));
        logger.EventIds.ShouldContain(new EventId(5010));
        logger.EventIds.ShouldContain(new EventId(5011));
        count.ShouldBe(1);
        durations.ShouldBe(1);
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.Outcome]);
    }

    [Fact]
    public async Task DispatchAsync_WhenUnavailableFailedOrCancelled_EmitsTruthfulErrorActivitiesAndBoundedOutcomes()
    {
        ConcurrentQueue<Activity> stopped = [];
        ConcurrentQueue<string> outcomes = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        using var meterListener = MeterListenerForAudit((_, tags) => outcomes.Enqueue(OutcomeFrom(tags)), static (_, _) =>
        {
        });
        var logger = new RecordingLogger();
        var unavailable = Dispatcher(SecurityAuditDelivery.Required, [], logger: logger);
        var failed = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new ThrowingSink())], logger: logger);
        _ = await unavailable.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = await failed.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await unavailable.DispatchAsync(Record(), cancellation.Token));
        _ = stopped.Where(activity => activity.Status == ActivityStatusCode.Error && activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "unavailable").ShouldHaveSingleItem();
        _ = stopped.Where(activity => activity.Status == ActivityStatusCode.Error && activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "failed").ShouldHaveSingleItem();
        _ = stopped.Where(activity => activity.Status == ActivityStatusCode.Error && activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "cancelled").ShouldHaveSingleItem();
        outcomes.ShouldBe(["unavailable", "failed", "cancelled"]);
        logger.EventIds.ShouldContain(new EventId(5012));
        logger.EventIds.ShouldContain(new EventId(5013));
        logger.EventIds.ShouldContain(new EventId(5014));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task DispatchAsync_WhenActivityListenerThrows_PreservesDeliveryAndParentage(bool throwOnStart, bool throwOnStop)
    {
        using var parent = new Activity("security-audit-dispatch-parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = throwOnStart || throwOnStop ? SampleAuditOnly : ThrowingAuditSample,
            ActivityStarted = throwOnStart ? static _ => throw new InvalidOperationException("observer") : null,
            ActivityStopped = throwOnStop ? static _ => throw new InvalidOperationException("observer") : null,
        };
        ActivitySource.AddActivityListener(listener);
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new RecordingSink())]);
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task DispatchAsync_WhenLoggingMeterOrTimingObserversFail_PreservesSemanticDeliveryWithoutInventingDuration()
    {
        var count = 0L;
        var durations = 0;
        using var meterListener = MeterListenerForAudit((measurement, _) => count += measurement, (_, _) => durations++);
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new RecordingSink())], timeProvider: new ThrowingTimeProvider(throwOnCall: 1), logger: new ThrowingLogger());
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        count.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task DispatchAsync_WhenMeterListenerThrowsOrObserversAreDisabled_PreservesSemanticDelivery()
    {
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, new RecordingSink())]);
        _ = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        using var meterListener = MeterListenerForAudit(static (_, _) => throw new InvalidOperationException("observer"), static (_, _) => throw new InvalidOperationException("observer"));
        _ = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DispatchAsync_WhenSameRecordIsRetried_ForwardsItsStableIdentityWithoutMintingAnotherRecord()
    {
        var sink = new RecordingSink();
        var record = Record();
        var dispatcher = Dispatcher(SecurityAuditDelivery.Required, [Binding(SecurityAuditDelivery.Required, durable: true, sink)]);
        _ = await dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken);
        _ = await dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken);
        sink.Records.ShouldBe([record, record]);
        sink.Records.ShouldAllBe(value => value.Id == record.Id);
    }

    [Fact]
    public async Task ConstructorAndDispatch_WhenArgumentsAreInvalid_ThrowWithExactParameterNames()
    {
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuditDispatcher(null!, Options.Create(new AgentPermissionOptions()), TimeProvider.System), "bindings");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuditDispatcher([], null!, TimeProvider.System), "options");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions()), null!), "timeProvider");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuditDispatcher([null!], Options.Create(new AgentPermissionOptions()), TimeProvider.System), "bindings");
        AssertExact<ArgumentOutOfRangeException>(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions { AuditDelivery = (SecurityAuditDelivery) 99 }), TimeProvider.System), "options");
        AssertExact<ArgumentOutOfRangeException>(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions { AuditDeliveryTimeout = TimeSpan.Zero }), TimeProvider.System), "options");
        AssertExact<ArgumentOutOfRangeException>(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions { AuditDeliveryTimeout = TimeSpan.FromTicks(-1) }), TimeProvider.System), "options");
        _ = Should.NotThrow(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions { AuditDeliveryTimeout = AgentPermissionOptions.MaximumAuditDeliveryTimeout }), TimeProvider.System));
        AssertExact<ArgumentOutOfRangeException>(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions { AuditDeliveryTimeout = AgentPermissionOptions.MaximumAuditDeliveryTimeout.Add(TimeSpan.FromTicks(1)), }), TimeProvider.System), "options");
        AssertExact<ArgumentOutOfRangeException>(() => new DefaultSecurityAuditDispatcher([], Options.Create(new AgentPermissionOptions { AuditDeliveryTimeout = TimeSpan.MaxValue }), TimeProvider.System), "options");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuditDispatcher([], new NullOptions(), TimeProvider.System), "options");
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Dispatcher(SecurityAuditDelivery.Required, []).DispatchAsync(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("record");
    }

    [Fact]
    public async Task Constructor_WhenOptionsAreReadOnce_CapturesTheDeliveryPolicyBeforeFutureMutation()
    {
        var options = new CountingOptions(new AgentPermissionOptions { AuditDelivery = SecurityAuditDelivery.Required });
        var dispatcher = new DefaultSecurityAuditDispatcher([], options, TimeProvider.System);
        options.Reads.ShouldBe(1);
        options.Current.AuditDelivery = SecurityAuditDelivery.BestEffort;
        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuditUnavailable>();
        options.Reads.ShouldBe(1);
    }

    [Fact]
    public void RecordAuditDispatch_WhenOutcomeIsUndefinedOrDurationIsNegative_ThrowsWithExactParameterNames()
    {
        AssertExact<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordAuditDispatch((SecurityAuditDispatchOutcome) 99, null), "outcome");
        AssertExact<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordAuditDispatch(SecurityAuditDispatchOutcome.Accepted, TimeSpan.FromTicks(-1)), "elapsed");
    }

    private static DefaultSecurityAuditDispatcher Dispatcher(SecurityAuditDelivery delivery, IEnumerable<SecurityAuditSinkBinding> bindings, TimeProvider? timeProvider = null, ILogger<DefaultSecurityAuditDispatcher>? logger = null, TimeSpan? deliveryTimeout = null) => new(bindings, Options.Create(new AgentPermissionOptions { AuditDelivery = delivery, AuditDeliveryTimeout = deliveryTimeout ?? TimeSpan.FromSeconds(30), }), timeProvider ?? TimeProvider.System, logger);
    private static SecurityAuditSinkBinding Binding(SecurityAuditDelivery delivery, bool durable, ISecurityAuditSink sink) => new(new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], delivery, durable), sink);
    private static SecurityAuditRecord Record(SecurityRequestId? requestId = null, GrantId? grantId = null, ApprovalRequestId? approvalRequestId = null, SecurityPolicyVersion? policyVersion = null, IEnumerable<KeyValuePair<string, RedactedAuditValue>>? fields = null) => new(new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")), new SecurityAuthorizationScope(new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")), new SessionId(Guid.Parse("a3333333-3333-3333-3333-333333333333")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("a4444444-4444-4444-4444-444444444444")), new AdmissionId(Guid.Parse("a5555555-5555-5555-5555-555555555555")))), requestId ?? new SecurityRequestId(Guid.Parse("a6666666-6666-6666-6666-666666666666")), grantId ?? new GrantId(Guid.Parse("a7777777-7777-7777-7777-777777777777")), approvalRequestId, SecurityAuditEventKind.GrantConsumptionIntent, SecurityAuditOutcome.Accepted, policyVersion ?? new SecurityPolicyVersion(1), fields?.ToImmutableDictionary() ?? [], DateTimeOffset.UnixEpoch);
    private static MeterListener MeterListenerForAudit(Action<long, ReadOnlySpan<KeyValuePair<string, object?>>> onCount, Action<double, ReadOnlySpan<KeyValuePair<string, object?>>> onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SecurityAuditDispatchCount or AgentKitMetricNames.SecurityAuditDispatchDuration)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityAuditDispatchCount)
            {
                onCount(measurement, tags);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityAuditDispatchDuration)
            {
                onDuration(measurement, tags);
            }
        });
        listener.Start();
        return listener;
    }

    private static string OutcomeFrom(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == AgentKitTagNames.Outcome)
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Audit dispatch metric did not include its bounded outcome tag.");
    }

    private static void AssertExact<TException>(Action action, string parameterName)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }

    private static void AssertExact<TException>(Func<object?> factory, string parameterName)
        where TException : ArgumentException => AssertExact<TException>(() =>
    {
        _ = factory();
    }, parameterName);
    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    private static ActivitySamplingResult SampleAuditOnly(ref ActivityCreationOptions<ActivityContext> options) => options.Name == AgentKitActivityNames.SecurityAuditDispatch ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
    private static ActivitySamplingResult ThrowingAuditSample(ref ActivityCreationOptions<ActivityContext> options) => options.Name == AgentKitActivityNames.SecurityAuditDispatch ? throw new InvalidOperationException("observer") : ActivitySamplingResult.None;
    private sealed class RecordingSink: ISecurityAuditSink
    {
        public List<SecurityAuditRecord> Records { get; } = [];

        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingSink: ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) => ValueTask.FromException(new InvalidOperationException("sink"));
    }

    /// <summary>A cancellation-ignoring sink that fires its own per-write deadline synchronously before completing.</summary>
    /// <remarks>
    /// Advancing the fake clock during the synchronous body of <see cref="WriteAsync"/> lets the write's own deadline
    /// elapse and complete before the dispatcher ever awaits it, so <c>Task.WaitAsync</c> observes an already
    /// completed task and returns it without inspecting the (now cancelled) linked token. This deterministically
    /// exercises the dispatcher's "sink completed exactly at its deadline without throwing" branch.
    /// </remarks>
    private sealed class DeadlineRacingSink(FakeTimeProvider timeProvider, TimeSpan deliveryTimeout): ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            timeProvider.Advance(deliveryTimeout);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A sink that triggers an external cancellation and then throws synchronously before returning a task.</summary>
    private sealed class SynchronouslyCancelingSink(CancellationTokenSource externalCancellation): ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            externalCancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellingSink(CancellationTokenSource cancellation): ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellingAndThrowingSink(CancellationTokenSource cancellation): ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromException(new InvalidOperationException("sink"));
        }
    }

    private sealed class BlockingSink: ISecurityAuditSink
    {
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => _completion.Task;
        public List<SecurityAuditRecord> Records { get; } = [];
        public Task Started => _started.Task;
        public CancellationToken DeliveryCancellation { get; private set; }

        public void Complete() => _completion.TrySetResult(true);
        public void Fail() => _completion.TrySetException(new InvalidOperationException("late sink fault"));
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            DeliveryCancellation = cancellationToken;
            _ = _started.TrySetResult(true);
            return new ValueTask(_completion.Task);
        }
    }

    private sealed class RecordingLogger: ILogger<DefaultSecurityAuditDispatcher>
    {
        public List<string> Messages { get; } = [];
        public List<EventId> EventIds { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            EventIds.Add(eventId);
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultSecurityAuditDispatcher>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => throw new InvalidOperationException("observer");
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class ThrowingTimeProvider(int throwOnCall): TimeProvider
    {
        private int _calls;
        public override long TimestampFrequency => 1_000;

        public override long GetTimestamp() => ++_calls == throwOnCall ? throw new InvalidOperationException("clock") : _calls * 100;
    }

    private sealed class CountingOptions(AgentPermissionOptions value): IOptions<AgentPermissionOptions>
    {
        public int Reads { get; private set; }
        public AgentPermissionOptions Current => value;

        public AgentPermissionOptions Value
        {
            get
            {
                Reads++;
                return value;
            }
        }
    }

    private sealed class NullOptions: IOptions<AgentPermissionOptions>
    {
        public AgentPermissionOptions Value => null!;
    }

    /// <summary>Creates isolated default dispatcher composition with a deterministic fixture clock.</summary>
    /// <returns>The fixture used by the inherited contract case.</returns>
    protected override DefaultSecurityAuditDispatcherConformanceFixture CreateFixture() => new();
}
