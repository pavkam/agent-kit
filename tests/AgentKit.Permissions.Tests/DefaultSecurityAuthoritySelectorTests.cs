// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

using TestSupport;

/// <summary>Verifies DefaultSecurityAuthoritySelector behavior and contracts.</summary>
public sealed class DefaultSecurityAuthoritySelectorTests
{
    [Fact]
    public async Task SelectAsync_WhenCapturedKeyIsBound_ReturnsThatExactAuthorityAndCapturedContext()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var authority = new DenyAllSecurityAuthority();
        var context = Context(key);
        var selector = Selector([new SecurityAuthorityBinding(key, authority)]);
        var result = await selector.SelectAsync(context, TestContext.Current.CancellationToken);
        var selected = result.ShouldBeOfType<SecurityAuthoritySelected>();
        selected.Authority.ShouldBeSameAs(authority);
        selected.Authorization.ShouldBeSameAs(context);
    }

    [Fact]
    public async Task SelectAsync_WhenCapturedKeyIsNotBound_FailsClosedWithoutUsingAnotherBinding()
    {
        var requested = new ComponentKey<ISecurityAuthority>("security.requested");
        var alternate = new ComponentKey<ISecurityAuthority>("security.alternate");
        var selector = Selector([new SecurityAuthorityBinding(alternate, new DenyAllSecurityAuthority())]);
        var result = await selector.SelectAsync(Context(requested), TestContext.Current.CancellationToken);
        var unavailable = result.ShouldBeOfType<SecurityAuthoritySelectionUnavailable>();
        unavailable.Authorization.AuthorityKey.ShouldBe(requested);
        unavailable.SafeReason.ShouldNotContain("alternate");
    }

    [Fact]
    public async Task SelectAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var selector = Selector([]);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await selector.SelectAsync(Context(new ComponentKey<ISecurityAuthority>("security.primary")), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task SelectAsync_WhenAuthorizationIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Selector([]).SelectAsync(null!, TestContext.Current.CancellationToken));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenBindingsDuplicateAKey_ThrowsArgumentExceptionForBindings()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var exception = Should.Throw<ArgumentException>(() => Selector([new SecurityAuthorityBinding(key, new DenyAllSecurityAuthority()), new SecurityAuthorityBinding(key, new DenyAllSecurityAuthority()),]));
        exception.ParamName.ShouldBe("bindings");
    }

    [Fact]
    public void Constructor_WhenDependenciesAreInvalid_ThrowsWithExactParameterNames()
    {
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuthoritySelector(null!, TimeProvider.System), "bindings");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuthoritySelector([], null!), "timeProvider");
        AssertExact<ArgumentNullException>(() => new DefaultSecurityAuthoritySelector([null!], TimeProvider.System), "bindings");
    }

    [Fact]
    public async Task SelectAsync_WhenObserved_EmitsContentFreeParentedActivityAndBoundedMetrics()
    {
        Activity? stopped = null;
        var counts = 0L;
        var durations = 0;
        List<KeyValuePair<string, object?>> metricTags = [];
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        using var parent = new Activity("security-selector-test").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityAuthoritySelect && activity.ParentId == parent.Id)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = MeterListenerForSelector((measurement, tags) =>
        {
            counts += measurement;
            metricTags.AddRange(tags.ToArray());
        }, (_, _) => durations++);
        var logger = new RecordingLogger();
        var selector = Selector([new SecurityAuthorityBinding(key, new DenyAllSecurityAuthority())], logger: logger);
        _ = await selector.SelectAsync(Context(key, "sensitive-policy-fingerprint"), TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.SecurityAuthorityKey).ShouldBe(key.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain("sensitive-policy-fingerprint");
        logger.Messages.ShouldAllBe(static message => !message.Contains("sensitive-policy-fingerprint", StringComparison.Ordinal));
        counts.ShouldBe(1);
        durations.ShouldBe(1);
        metricTags.Select(static tag => tag.Key).Distinct().ShouldBe([AgentKitTagNames.Outcome]);
    }

    [Fact]
    public async Task SelectAsync_WhenObserversOrTimingFail_ReturnsTheSemanticSelectionWithoutInventingDuration()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var count = 0L;
        var durations = 0;
        using var meterListener = MeterListenerForSelector((measurement, _) => count += measurement, (_, _) => durations++);
        var selector = Selector([new SecurityAuthorityBinding(key, new DenyAllSecurityAuthority())], timeProvider: new ThrowingTimeProvider(throwOnCall: 1), logger: new ThrowingLogger());
        var result = await selector.SelectAsync(Context(key), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthoritySelected>();
        count.ShouldBe(1);
        durations.ShouldBe(0);
    }

    [Fact]
    public async Task SelectAsync_WhenUnavailableOrCancelled_EmitsTruthfulErrorActivityAndBoundedOutcome()
    {
        var outcomes = new List<string>();
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using var meterListener = MeterListenerForSelector((_, tags) => outcomes.Add(OutcomeFrom(tags)), static (_, _) =>
        {
        });
        var selector = Selector([]);
        _ = await selector.SelectAsync(Context(new ComponentKey<ISecurityAuthority>("security.missing")), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await selector.SelectAsync(Context(new ComponentKey<ISecurityAuthority>("security.missing")), cancellation.Token));
        _ = stopped.Where(activity => activity.Status == ActivityStatusCode.Error && activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "unavailable").ShouldHaveSingleItem();
        _ = stopped.Where(activity => activity.Status == ActivityStatusCode.Error && activity.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "cancelled").ShouldHaveSingleItem();
        outcomes.ShouldBe(["unavailable", "cancelled"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SelectAsync_WhenActivityListenerThrows_PreservesSelectionAndParentage(bool throwOnStart, bool throwOnStop)
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        using var parent = new Activity("security-selector-parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = throwOnStart || throwOnStop ? SampleSelectorOnly : ThrowingSelectorSample,
            ActivityStarted = throwOnStart ? static _ => throw new InvalidOperationException("observer") : null,
            ActivityStopped = throwOnStop ? static _ => throw new InvalidOperationException("observer") : null,
        };
        ActivitySource.AddActivityListener(listener);
        var result = await Selector([new SecurityAuthorityBinding(key, new DenyAllSecurityAuthority())]).SelectAsync(Context(key), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SecurityAuthoritySelected>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task SelectAsync_WhenMeterCallbackThrowsOrListenersAreDisabled_PreservesSemanticSelection()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var selector = Selector([new SecurityAuthorityBinding(key, new DenyAllSecurityAuthority())]);
        _ = await selector.SelectAsync(Context(key), TestContext.Current.CancellationToken);
        using var meterListener = MeterListenerForSelector(static (_, _) => throw new InvalidOperationException("observer"), static (_, _) => throw new InvalidOperationException("observer"));
        _ = await selector.SelectAsync(Context(key), TestContext.Current.CancellationToken);
    }

    private static DefaultSecurityAuthoritySelector Selector(IEnumerable<SecurityAuthorityBinding> bindings, TimeProvider? timeProvider = null, ILogger<DefaultSecurityAuthoritySelector>? logger = null) => new(bindings, timeProvider ?? TimeProvider.System, logger);
    private static SecurityAuthorizationContext Context(ComponentKey<ISecurityAuthority> authorityKey, string fingerprint = "policy-fingerprint") => new(new SecurityProfileKey("security.profile"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d1111111-1111-1111-1111-111111111111")), new SecurityPolicyVersion(1), new ContentHash(fingerprint)), authorityKey, new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(new AgentId(Guid.Parse("d2222222-2222-2222-2222-222222222222")), new SessionId(Guid.Parse("d3333333-3333-3333-3333-333333333333")), new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("d4444444-4444-4444-4444-444444444444")), new AdmissionId(Guid.Parse("d5555555-5555-5555-5555-555555555555")))), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
    private static MeterListener MeterListenerForSelector(Action<long, ReadOnlySpan<KeyValuePair<string, object?>>> onCount, Action<double, ReadOnlySpan<KeyValuePair<string, object?>>> onDuration)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SecurityAuthoritySelectionCount or AgentKitMetricNames.SecurityAuthoritySelectionDuration)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityAuthoritySelectionCount)
            {
                onCount(measurement, tags);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == AgentKitMetricNames.SecurityAuthoritySelectionDuration)
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

        throw new InvalidOperationException("Selection metric did not include its bounded outcome tag.");
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
    private static ActivitySamplingResult SampleSelectorOnly(ref ActivityCreationOptions<ActivityContext> options) => options.Name == AgentKitActivityNames.SecurityAuthoritySelect ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
    private static ActivitySamplingResult ThrowingSelectorSample(ref ActivityCreationOptions<ActivityContext> options) => options.Name == AgentKitActivityNames.SecurityAuthoritySelect ? throw new InvalidOperationException("observer") : ActivitySamplingResult.None;
    private sealed class RecordingLogger: ILogger<DefaultSecurityAuthoritySelector>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class ThrowingLogger: ILogger<DefaultSecurityAuthoritySelector>
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
