// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

using TestSupport;

/// <summary>Verifies DefaultSecurityProfileSelector behavior and contracts.</summary>
public sealed class DefaultSecurityProfileSelectorTests
{
    [Fact]
    public async Task SelectAsync_WhenExactPublicationIsFound_CreatesFreshContextPreservingScopeAndIdentity()
    {
        var request = Request();
        var publication = Publication(request);
        var reader = new RecordingReader(new SecurityProfilePublicationFound(publication));
        var selector = Selector(reader);
        var first = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var second = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var firstContext = first.ShouldBeOfType<SecurityAuthorizationCaptured>().Authorization;
        var secondContext = second.ShouldBeOfType<SecurityAuthorizationCaptured>().Authorization;
        firstContext.ShouldNotBeSameAs(secondContext);
        firstContext.Scope.ShouldBeSameAs(request.Scope);
        firstContext.Identity.ShouldBeSameAs(request.Identity);
        firstContext.ProfileKey.ShouldBe(publication.ProfileKey);
        firstContext.ProfileVersion.ShouldBe(publication.ProfileVersion);
        firstContext.PolicySnapshot.ShouldBeSameAs(publication.PolicySnapshot);
        firstContext.AuthorityKey.ShouldBe(publication.AuthorityKey);
        firstContext.AgentDefinitionRevision.ShouldBe(request.AgentDefinitionRevision);
        firstContext.ConfigurationVersion.ShouldBe(request.ConfigurationVersion);
        reader.CallCount.ShouldBe(2);
        reader.AgentId.ShouldBe(request.Scope.AgentId);
        reader.AgentDefinitionRevision.ShouldBe(request.AgentDefinitionRevision);
        reader.ConfigurationVersion.ShouldBe(request.ConfigurationVersion);
        reader.ProfileKey.ShouldBe(request.ProfileKey);
        reader.CancellationToken.ShouldBe(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SelectAsync_WhenPublicationIsUnavailable_ReturnsGenericTypedUnavailableWithoutLeakingReaderReason()
    {
        var selector = Selector(new RecordingReader(new SecurityProfilePublicationUnavailable("sensitive internal publication failure")));
        var result = await selector.SelectAsync(Request(), TestContext.Current.CancellationToken);
        var unavailable = result.ShouldBeOfType<SecurityAuthorizationCaptureUnavailable>();
        unavailable.SafeReason.ShouldNotContain("sensitive");
    }

    [Fact]
    public async Task SelectAsync_WhenReaderReturnsNull_ReturnsTypedUnavailable()
    {
        var selector = Selector(new RecordingReader(null!));
        var result = await selector.SelectAsync(Request(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthorizationCaptureUnavailable>();
    }

    [Fact]
    public async Task SelectAsync_WhenReaderReturnsMismatchedCoordinates_ReturnsUnavailableWithoutPartialContext()
    {
        var request = Request();
        var mismatches = new[]
        {
            Publication(request, agentId: new AgentId(Guid.Parse("c9999999-9999-9999-9999-999999999999"))),
            Publication(request, definitionRevision: new AgentDefinitionRevision(request.AgentDefinitionRevision.Value + 1)),
            Publication(request, configurationVersion: new ConfigurationVersion(request.ConfigurationVersion.Value + 1)),
            Publication(request, profileKey: new SecurityProfileKey("security.other")),
        };
        foreach (var publication in mismatches)
        {
            var selector = Selector(new RecordingReader(new SecurityProfilePublicationFound(publication)));
            var result = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
            _ = result.ShouldBeOfType<SecurityAuthorizationCaptureUnavailable>();
        }
    }

    [Fact]
    public async Task SelectAsync_WhenCallerCancelsAfterReaderReturns_PropagatesCancellationWithoutSuccess()
    {
        var request = Request();
        var reader = new AwaitingReader();
        var selector = Selector(reader);
        using var cancellation = new CancellationTokenSource();
        var selection = selector.SelectAsync(request, cancellation.Token).AsTask();
        await reader.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        reader.Completion.SetResult(new SecurityProfilePublicationFound(Publication(request)));
        var exception = await Should.ThrowAsync<OperationCanceledException>(selection);
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task SelectAsync_WhenReaderFaults_PropagatesOriginalFailure()
    {
        var expected = new InvalidOperationException("reader failed");
        var selector = Selector(new ThrowingReader(expected));
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await selector.SelectAsync(Request(), TestContext.Current.CancellationToken));
        exception.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task SelectAsync_WhenUnavailableCancelledOrFaulted_LogsTheirDistinctEvents()
    {
        var logger = new RecordingLogger();
        var unavailableSelector = Selector(new RecordingReader(new SecurityProfilePublicationUnavailable("unavailable")), logger: logger);
        var awaitingReader = new AwaitingReader();
        var cancelledSelector = Selector(awaitingReader, logger: logger);
        var faultedSelector = Selector(new ThrowingReader(new InvalidOperationException("boom")), logger: logger);
        var request = Request();

        _ = await unavailableSelector.SelectAsync(request, TestContext.Current.CancellationToken);

        using var cancellation = new CancellationTokenSource();
        var cancelledTask = cancelledSelector.SelectAsync(request, cancellation.Token).AsTask();
        await awaitingReader.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        awaitingReader.Completion.SetResult(new SecurityProfilePublicationFound(Publication(request)));
        _ = await Should.ThrowAsync<OperationCanceledException>(cancelledTask);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await faultedSelector.SelectAsync(request, TestContext.Current.CancellationToken));

        logger.EventIds.ShouldContain(5018);
        logger.EventIds.ShouldContain(5019);
        logger.EventIds.ShouldContain(5020);
    }

    [Fact]
    public async Task SelectAsync_WhenRequestIsNull_ThrowsBeforeReaderOrClockEffects()
    {
        var reader = new RecordingReader(new SecurityProfilePublicationUnavailable("Unavailable."));
        var selector = Selector(reader, new ThrowingTimeProvider(throwOnCall: 1));
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await selector.SelectAsync(null!, TestContext.Current.CancellationToken));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("request");
        reader.CallCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenDependenciesAreNull_ThrowsWithExactParameterNames()
    {
        AssertExact<ArgumentNullException>(() => new DefaultSecurityProfileSelector(null!, TimeProvider.System), "publications");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityProfileSelector(new RecordingReader(new SecurityProfilePublicationUnavailable("Unavailable.")), null!), "timeProvider");
    }

    [Fact]
    public void RecordProfileCapture_WhenOutcomeIsUndefinedOrDurationIsNegative_ThrowsWithExactParameterNames()
    {
        AssertExact<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordProfileCapture((SecurityProfileCaptureOutcome) 99, null), "outcome");
        AssertExact<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordProfileCapture(SecurityProfileCaptureOutcome.Captured, TimeSpan.FromTicks(-1)), "elapsed");
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsWithExactParameterName() => AssertExact<ArgumentOutOfRangeException>(() => ((SecurityProfileCaptureOutcome) 99).ToStableValue(), "outcome");

    [Fact]
    public async Task SelectAsync_WhenObserved_EmitsSafeParentedActivityStructuredLogsAndBoundedMetrics()
    {
        Activity? stopped = null;
        var count = 0L;
        var durationCount = 0;
        List<KeyValuePair<string, object?>> metricTags = [];
        var request = Request();
        using var parent = new Activity("profile-capture-test").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityProfileCapture && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForCapture((measurement, tags) =>
        {
            count += measurement;
            metricTags.AddRange(tags.ToArray());
        }, (_, _) => durationCount++);
        var logger = new RecordingLogger();
        var selector = Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request, fingerprint: "sensitive-policy-fingerprint"))), logger: logger);
        _ = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.SecurityProfileKey).ShouldBe(request.ProfileKey.ToString());
        activity.GetTagItem(AgentKitTagNames.SecurityAuthorityKey).ShouldBeNull();
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.Scope.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(request.Scope.SessionId?.ToString());
        activity.GetTagItem(AgentKitTagNames.OperationId).ShouldBe(request.Scope.Correlation.OperationId.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("sensitive-policy-fingerprint");
        logger.EventIds.ShouldBe([5016, 5017]);
        logger.Messages.ShouldAllBe(static message => !message.Contains("sensitive-policy-fingerprint", StringComparison.Ordinal));
        count.ShouldBe(1);
        durationCount.ShouldBe(1);
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.Outcome]);
        metricTags.Any(static tag => tag.Value?.ToString() == "captured").ShouldBeTrue();
    }

    [Fact]
    public async Task SelectAsync_WhenUnavailableMismatchedCancelledOrFailed_EmitsTruthfulTerminalOutcomes()
    {
        var outcomes = new List<string>();
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityProfileCapture)
                {
                    stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var meterListener = MeterListenerForCapture((_, tags) => outcomes.Add(OutcomeFrom(tags)), static (_, _) =>
        {
        });
        var request = Request();
        _ = await Selector(new RecordingReader(new SecurityProfilePublicationUnavailable("Unavailable."))).SelectAsync(request, TestContext.Current.CancellationToken);
        _ = await Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request, profileKey: new SecurityProfileKey("security.other"))))).SelectAsync(request, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request)))).SelectAsync(request, cancellation.Token));
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await Selector(new ThrowingReader(new InvalidOperationException("failure"))).SelectAsync(request, TestContext.Current.CancellationToken));
        AssertTerminalActivity(stopped, "unavailable", "unavailable");
        AssertTerminalActivity(stopped, "mismatched_publication", "mismatched_publication");
        AssertTerminalActivity(stopped, "cancelled", nameof(OperationCanceledException));
        AssertTerminalActivity(stopped, "failed", typeof(InvalidOperationException).FullName!);
        outcomes.ShouldBe(["unavailable", "mismatched_publication", "cancelled", "failed"], ignoreOrder: true);
    }

    [Fact]
    public async Task SelectAsync_WhenLoggerOrTimingFails_ReturnsCaptureWithoutInventingDuration()
    {
        var count = 0L;
        var durations = 0;
        using var meterListener = MeterListenerForCapture((measurement, _) => count += measurement, (_, _) => durations++);
        var request = Request();
        var selector = Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request))), new ThrowingTimeProvider(throwOnCall: 1), new ThrowingLogger());
        var result = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthorizationCaptured>();
        count.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task SelectAsync_WhenElapsedTimeObservationFailsAfterASuccessfulTimestamp_RecordsNoDuration()
    {
        var count = 0L;
        var durations = 0;
        using var meterListener = MeterListenerForCapture((measurement, _) => count += measurement, (_, _) => durations++);
        var request = Request();
        var selector = Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request))), new ThrowingTimeProvider(throwOnCall: 2));
        var result = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthorizationCaptured>();
        count.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Theory]
    [InlineData("sample")]
    [InlineData("started")]
    [InlineData("stopped")]
    public async Task SelectAsync_WhenActivityListenerThrows_PreservesCaptureAndParentage(string failureStage)
    {
        var request = Request();
        using var parent = new Activity("profile-capture-parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = failureStage == "sample" ? ThrowingCaptureSample : SampleCaptureOnly,
            ActivityStarted = failureStage == "started" ? static _ => throw new InvalidOperationException("observer") : null,
            ActivityStopped = failureStage == "stopped" ? static _ => throw new InvalidOperationException("observer") : null,
        };
        ActivitySource.AddActivityListener(listener);
        var result = await Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request)))).SelectAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthorizationCaptured>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task SelectAsync_WhenMeterCallbackThrowsOrListenersAreDisabled_PreservesCapture()
    {
        var request = Request();
        var selector = Selector(new RecordingReader(new SecurityProfilePublicationFound(Publication(request))));
        _ = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        using var meterListener = MeterListenerForCapture(static (_, _) => throw new InvalidOperationException("observer"), static (_, _) => throw new InvalidOperationException("observer"));
        var observed = await selector.SelectAsync(request, TestContext.Current.CancellationToken);
        _ = observed.ShouldBeOfType<SecurityAuthorizationCaptured>();
    }

    private static DefaultSecurityProfileSelector Selector(ISecurityProfilePublicationReader reader, TimeProvider? timeProvider = null, ILogger<DefaultSecurityProfileSelector>? logger = null) => new(reader, timeProvider ?? TimeProvider.System, logger);
    private static SecurityAuthorizationCaptureRequest Request() => new(new SecurityAuthorizationScope(new AgentId(Guid.Parse("c1111111-1111-1111-1111-111111111111")), new SessionId(Guid.Parse("c2222222-2222-2222-2222-222222222222")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("c3333333-3333-3333-3333-333333333333")), new AdmissionId(Guid.Parse("c4444444-4444-4444-4444-444444444444")))), new SecurityProfileKey("security.primary"), new AgentDefinitionRevision(2), new ConfigurationVersion(3), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
    private static SecurityProfilePublication Publication(SecurityAuthorizationCaptureRequest request, AgentId? agentId = null, AgentDefinitionRevision? definitionRevision = null, ConfigurationVersion? configurationVersion = null, SecurityProfileKey? profileKey = null, string fingerprint = "sha256:policy") => new(agentId ?? request.Scope.AgentId, definitionRevision ?? request.AgentDefinitionRevision, configurationVersion ?? request.ConfigurationVersion, profileKey ?? request.ProfileKey, new SecurityProfileVersion(4), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("c5555555-5555-5555-5555-555555555555")), new SecurityPolicyVersion(5), new ContentHash(fingerprint)), new ComponentKey<ISecurityAuthority>("authority.primary"));
    private static MeterListener MeterListenerForCapture(Action<long, ReadOnlySpan<KeyValuePair<string, object?>>> onCount, Action<double, ReadOnlySpan<KeyValuePair<string, object?>>> onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SecurityProfileCaptureCount or AgentKitMetricNames.SecurityProfileCaptureDuration)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityProfileCaptureCount)
            {
                onCount(measurement, tags);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityProfileCaptureDuration)
            {
                onDuration(measurement, tags);
            }
        });
        listener.Start();
        return listener;
    }

    private static void AssertTerminalActivity(IEnumerable<Activity> activities, string outcome, string errorType)
    {
        var activity = activities.Where(activity => activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == outcome).ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(errorType);
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

        throw new InvalidOperationException("Capture metric did not include its bounded outcome tag.");
    }

    private static void AssertExact<TException>(Func<object?> factory, string parameterName)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(() => _ = factory());
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }

    private static void AssertExact<TException>(Action action, string parameterName)
        where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameterName);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    private static ActivitySamplingResult SampleCaptureOnly(ref ActivityCreationOptions<ActivityContext> options) => options.Name == AgentKitActivityNames.SecurityProfileCapture ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
    private static ActivitySamplingResult ThrowingCaptureSample(ref ActivityCreationOptions<ActivityContext> options) => options.Name == AgentKitActivityNames.SecurityProfileCapture ? throw new InvalidOperationException("observer") : ActivitySamplingResult.None;
    private sealed class RecordingReader(SecurityProfilePublicationResult result): ISecurityProfilePublicationReader
    {
        public int CallCount { get; private set; }
        public AgentId AgentId { get; private set; }
        public AgentDefinitionRevision AgentDefinitionRevision { get; private set; }
        public ConfigurationVersion ConfigurationVersion { get; private set; }
        public SecurityProfileKey ProfileKey { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<SecurityProfilePublicationResult> ReadAsync(AgentId agentId, AgentDefinitionRevision agentDefinitionRevision, ConfigurationVersion configurationVersion, SecurityProfileKey profileKey, CancellationToken cancellationToken = default)
        {
            CallCount++;
            AgentId = agentId;
            AgentDefinitionRevision = agentDefinitionRevision;
            ConfigurationVersion = configurationVersion;
            ProfileKey = profileKey;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AwaitingReader: ISecurityProfilePublicationReader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<SecurityProfilePublicationResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<SecurityProfilePublicationResult> ReadAsync(AgentId agentId, AgentDefinitionRevision agentDefinitionRevision, ConfigurationVersion configurationVersion, SecurityProfileKey profileKey, CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            return await Completion.Task.ConfigureAwait(false);
        }
    }

    private sealed class ThrowingReader(Exception exception): ISecurityProfilePublicationReader
    {
        public ValueTask<SecurityProfilePublicationResult> ReadAsync(AgentId agentId, AgentDefinitionRevision agentDefinitionRevision, ConfigurationVersion configurationVersion, SecurityProfileKey profileKey, CancellationToken cancellationToken = default) => ValueTask.FromException<SecurityProfilePublicationResult>(exception);
    }

    private sealed class RecordingLogger: ILogger<DefaultSecurityProfileSelector>
    {
        public List<int> EventIds { get; } = [];
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            EventIds.Add(eventId.Id);
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultSecurityProfileSelector>
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
}
