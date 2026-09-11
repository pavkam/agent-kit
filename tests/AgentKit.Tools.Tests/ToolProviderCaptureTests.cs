// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolProviderCaptureTests: ToolProviderCaptureConformanceTests
{
    protected override IToolProviderCapture CreateCapture(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers, IAsyncDisposable? lifetime) =>
        new ToolProviderCapture(snapshot, invokers, lifetime, TimeProvider.System, NullLogger<ToolProviderCapture>.Instance);

    [Fact]
    public void Constructor_WhenPrevalidatedDependenciesAreNull_RejectsBeforeLifetimeOwnership()
    {
        var bindings = new ToolProviderBindings(ToolCaptureTestData.Snapshot([]), []);
        var owner = new CaptureLifetimeProbe();
        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(null!, owner, TimeProvider.System, NullLogger<ToolProviderCapture>.Instance), "bindings");
        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(bindings, owner, null!, NullLogger<ToolProviderCapture>.Instance), "timeProvider");
        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(bindings, owner, TimeProvider.System, null!), "logger");
        owner.Calls.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenDependenciesAreInvalid_RejectsBeforeTakingOwnershipOrReadingClock()
    {
        var owner = new CaptureLifetimeProbe();
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<ToolProviderCapture>();
        var snapshot = ToolCaptureTestData.Snapshot([]);

        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(null!, [], owner, clock, logger), "snapshot");
        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(snapshot, null!, owner, clock, logger), "invokers");
        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(snapshot, [], owner, null!, logger), "timeProvider");
        AssertExact<ArgumentNullException>(() => _ = new ToolProviderCapture(snapshot, [], owner, clock, null!), "logger");
        owner.Calls.ShouldBe(0);
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenBindingsAreIncompleteOrInvalid_RejectsBeforeTakingOwnership()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var bindings = ToolCaptureTestData.Bindings(tool, invoker);
        var identity = new ToolIdentity(tool.Id, tool.Version);

        AssertExact<ArgumentException>(() => CreateCapture(snapshot, [], owner), "invokers");
        AssertExact<ArgumentException>(() => CreateCapture(ToolCaptureTestData.Snapshot([]), bindings, owner), "invokers");
        AssertExact<ArgumentException>(() => CreateCapture(snapshot, bindings.Add(new(new ToolId("extra"), tool.Version), invoker), owner), "invokers");
        AssertExact<ArgumentException>(() => CreateCapture(snapshot, ToolCaptureTestData.Bindings(ToolCaptureTestData.Descriptor(version: "2"), invoker), owner), "invokers");
        AssertExact<ArgumentOutOfRangeException>(() => CreateCapture(snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty.Add(default, invoker), owner), "invokers");
        AssertExact<ArgumentNullException>(() => CreateCapture(snapshot, bindings.SetItem(identity, null!), owner), "invokers");
        owner.Calls.ShouldBe(0);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenComparerWeakensExactIdentity_RejectsInsteadOfSelectingACaseInsensitiveBinding()
    {
        var tool = ToolCaptureTestData.Descriptor(version: "v1");
        var comparer = EqualityComparer<ToolIdentity>.Create(
            static (left, right) => StringComparer.OrdinalIgnoreCase.Equals(left.Id.Value, right.Id.Value)
                && StringComparer.OrdinalIgnoreCase.Equals(left.Version.Value, right.Version.Value),
            static value => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Id.Value), StringComparer.OrdinalIgnoreCase.GetHashCode(value.Version.Value)));
        var invoker = new CaptureTestToolInvoker();
        var bindings = ImmutableDictionary.Create<ToolIdentity, IToolInvoker>(comparer)
            .Add(new(new ToolId("TOOL.READ"), new ToolVersion("V1")), invoker);

        AssertExact<ArgumentException>(() => CreateCapture(ToolCaptureTestData.Snapshot([tool]), bindings, null), "invokers");
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenNormalizationRevealsDuplicateKeys_RejectsEvenTheSameInvoker()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var identity = new ToolIdentity(tool.Id, tool.Version);
        var invoker = new CaptureTestToolInvoker();
        var comparer = EqualityComparer<ToolIdentity>.Create(static (_, _) => false, static key => key.GetHashCode());
        var bindings = ImmutableDictionary.Create<ToolIdentity, IToolInvoker>(comparer).Add(identity, invoker).Add(identity, invoker);

        AssertExact<ArgumentException>(() => CreateCapture(ToolCaptureTestData.Snapshot([tool]), bindings, null), "invokers");
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenSeveralVersionsShareAnId_UsesExactBindingsAndPublicationOrder()
    {
        var first = ToolCaptureTestData.Descriptor(version: "1");
        var second = ToolCaptureTestData.Descriptor(version: "2");
        var firstInvoker = new CaptureTestToolInvoker();
        var secondInvoker = new CaptureTestToolInvoker();
        var bindings = ToolCaptureTestData.Bindings(first, firstInvoker).Add(new(second.Id, second.Version), secondInvoker);
        await using var capture = CreateCapture(ToolCaptureTestData.Snapshot([second, first]), bindings, null);

        await using var firstLease = (await capture.AcquireInvokerAsync(new(first.Id, first.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        await using var secondLease = (await capture.AcquireInvokerAsync(new(second.Id, second.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        firstLease.Invoker.ShouldBeSameAs(firstInvoker);
        secondLease.Invoker.ShouldBeSameAs(secondInvoker);
        capture.Snapshot.Tools.ShouldBe([second, first]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_WhenNoLeasesRemain_ReportsOwnedCleanupFailureOnce(bool asynchronous)
    {
        var failure = new InvalidOperationException("controlled failure");
        var owner = new CaptureLifetimeProbe(() => asynchronous ? new ValueTask(Task.FromException(failure)) : throw failure);
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([]), [], owner);

        (await Should.ThrowAsync<InvalidOperationException>(async () => await capture.DisposeAsync())).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await capture.DisposeAsync())).ShouldBeSameAs(failure);
        owner.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnedCleanupReentersAcquisition_ObservesClosedCapture()
    {
        IToolProviderCapture? capture = null;
        var identity = new ToolIdentity(new ToolId("tool"), new ToolVersion("1"));
        var owner = new CaptureLifetimeProbe(async () =>
        {
            var result = await capture!.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken);
            result.ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(identity);
        });
        capture = CreateCapture(ToolCaptureTestData.Snapshot([]), [], owner);

        await capture.DisposeAsync();

        owner.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenSourceOwnsAnAsyncServiceScope_DrainsLeasesBeforeDisposingScopedInvokers()
    {
        await using var provider = new ServiceCollection().AddScoped<CaptureTestToolInvoker>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = provider.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<CaptureTestToolInvoker>();
        var tool = ToolCaptureTestData.Descriptor();
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), scope);
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var closing = capture.DisposeAsync().AsTask();

        closing.IsCompleted.ShouldBeFalse();
        invoker.Disposals.ShouldBe(0);
        lease.Invoker.ShouldBeSameAs(invoker);
        await lease.DisposeAsync();
        await closing;
        await capture.DisposeAsync();
        invoker.Disposals.ShouldBe(1);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_WhenInvokersBelongToHostScope_PreservesHostDisposalOwnership()
    {
        await using var provider = new ServiceCollection().AddScoped<CaptureTestToolInvoker>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = provider.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<CaptureTestToolInvoker>();
        var tool = ToolCaptureTestData.Descriptor();
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), null);
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        await lease.DisposeAsync();
        await capture.DisposeAsync();

        invoker.Disposals.ShouldBe(0);
        await scope.DisposeAsync();
        invoker.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData("acquired", 4031, LogLevel.Debug)]
    [InlineData("unavailable", 4034, LogLevel.Warning)]
    [InlineData("cancelled", 4032, LogLevel.Information)]
    public async Task AcquireInvokerAsync_WhenObserved_ReportsSafeCorrelatedSignals(string outcome, int terminalEvent, LogLevel level)
    {
        const string protectedContent = "descriptor-content-must-not-be-observed";
        var tool = ToolCaptureTestData.Descriptor(description: protectedContent);
        var logger = new RecordingLogger<ToolProviderCapture>();
        long ticks = 0;
        var clock = new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125));
        var snapshot = ToolCaptureTestData.Snapshot(outcome == "unavailable" ? [] : [tool]);
        var capture = new ToolProviderCapture(snapshot, outcome == "unavailable" ? [] : ToolCaptureTestData.Bindings(tool, new CaptureTestToolInvoker()), null, clock, logger);
        using var parent = new Activity("capture-acquisition-observation").Start();
        Activity? observed = null;
        using var activities = Listen(parent, activity => observed = activity, AgentKitActivityNames.ToolInvokerAcquire);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measurements.Add((name, value, tags)), AgentKitActivityNames.ToolInvokerAcquire);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled")
        {
            await cancellation.CancelAsync();
        }
        IToolInvokerLease? lease = null;
        try
        {
            if (outcome == "cancelled")
            {
                var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), cancellation.Token));
                exception.CancellationToken.ShouldBe(cancellation.Token);
            }
            else
            {
                var result = await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken);
                lease = (result as ToolInvokerAcquired)?.Lease;
                (lease is not null).ShouldBe(outcome == "acquired");
            }

            var activity = observed.ShouldNotBeNull();
            activity.ParentId.ShouldBe(parent.Id);
            activity.Status.ShouldBe(outcome == "acquired" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
            activity.GetTagItem(AgentKitTagNames.ToolSourceId).ShouldBe(snapshot.SourceId.Value);
            activity.GetTagItem(AgentKitTagNames.ToolSourceVersion).ShouldBe(snapshot.SourceVersion.Value);
            activity.GetTagItem(AgentKitTagNames.ToolId).ShouldBe(tool.Id.Value);
            activity.GetTagItem(AgentKitTagNames.ToolVersion).ShouldBe(tool.Version.Value);
            if (outcome != "acquired")
            {
                activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(outcome);
            }
            var entries = logger.Snapshot();
            entries.Select(static entry => entry.EventId.Id).ShouldBe([4030, terminalEvent]);
            entries[^1].Level.ShouldBe(level);
            entries.ShouldAllBe(entry => entry.Category == typeof(ToolProviderCapture).FullName);
            foreach (var entry in entries)
            {
                entry.State["SourceId"].ShouldBe(snapshot.SourceId);
                entry.State["SourceVersion"].ShouldBe(snapshot.SourceVersion);
            }
            AssertSafe(entries, [activity], protectedContent);
            measurements.Count.ShouldBe(2);
            measurements.Single(item => item.Name == AgentKitMetricNames.ToolProviderCaptureOperationCount).Value.ShouldBe(1);
            measurements.Single(item => item.Name == AgentKitMetricNames.ToolProviderCaptureOperationDuration).Value.ShouldBe(0.125);
            foreach (var (Name, Value, Tags) in measurements)
            {
                AssertMetricTags(Tags, AgentKitActivityNames.ToolInvokerAcquire, outcome);
            }
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
            await capture.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_WhenObserved_RecordsDrainReleaseAndOwnedCleanup(bool fails)
    {
        const string protectedContent = "cleanup-error-and-descriptor-content-must-not-be-observed";
        var tool = ToolCaptureTestData.Descriptor(description: protectedContent);
        var error = new InvalidOperationException(protectedContent);
        var owner = new CaptureLifetimeProbe(() => fails ? new ValueTask(Task.FromException(error)) : ValueTask.CompletedTask);
        var logger = new RecordingLogger<ToolProviderCapture>();
        long timestamp = 0;
        var capture = new ToolProviderCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, new CaptureTestToolInvoker()), owner,
            new CallbackTimestampTimeProvider(() => Interlocked.Increment(ref timestamp)), logger);
        using var parent = new Activity("capture-closure-observation").Start();
        var observed = new ConcurrentQueue<Activity>();
        using var activities = Listen(parent, observed.Enqueue);
        var measurements = new ConcurrentQueue<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var metrics = ListenMetrics(parent, (name, value, tags) => measurements.Enqueue((name, value, tags)));
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var closing = capture.DisposeAsync().AsTask();
        if (fails)
        {
            (await Should.ThrowAsync<InvalidOperationException>(async () => await lease.DisposeAsync())).ShouldBeSameAs(error);
            (await Should.ThrowAsync<InvalidOperationException>(async () => await closing)).ShouldBeSameAs(error);
        }
        else
        {
            await lease.DisposeAsync();
            await closing;
        }

        var spans = observed.ToArray();
        spans.Length.ShouldBe(4);
        var release = spans.Single(activity => activity.OperationName == AgentKitActivityNames.ToolInvokerRelease);
        foreach (var activity in spans)
        {
            activity.ParentId.ShouldBe(activity.OperationName == AgentKitActivityNames.ToolProviderCaptureDisposeResources ? release.Id : parent.Id);
            activity.Status.ShouldBe(fails && activity.OperationName != AgentKitActivityNames.ToolInvokerAcquire ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            if (fails && activity.OperationName != AgentKitActivityNames.ToolInvokerAcquire)
            {
                activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
                activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(typeof(InvalidOperationException).FullName);
            }
        }
        var entries = logger.Snapshot();
        entries.Count(entry => entry.EventId.Id == 4030).ShouldBe(4);
        entries.Count(entry => entry.EventId.Id == 4031).ShouldBe(fails ? 1 : 4);
        entries.Count(entry => entry.EventId.Id == 4033).ShouldBe(fails ? 3 : 0);
        AssertSafe(entries, spans, protectedContent);
        var measured = measurements.ToArray();
        measured.Length.ShouldBe(8);
        foreach (var activity in spans)
        {
            var matching = measured.Where(item => (item.Tags[0].Value as string) == activity.OperationName).ToArray();
            matching.Length.ShouldBe(2);
            foreach (var (Name, Value, Tags) in matching)
            {
                AssertMetricTags(Tags, activity.OperationName, (string) activity.GetTagItem(AgentKitTagNames.Outcome)!);
                Value.ShouldBeGreaterThanOrEqualTo(0);
            }
        }
        owner.Calls.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenIdentityIsInvalid_RejectsBeforeDiagnostics()
    {
        var logger = new RecordingLogger<ToolProviderCapture>();
        var reads = 0;
        var capture = new ToolProviderCapture(ToolCaptureTestData.Snapshot([]), [], null, new CallbackTimestampTimeProvider(() => ++reads), logger);
        using var parent = new Activity("invalid-capture-identity").Start();
        var activities = 0;
        using var listener = Listen(parent, _ => activities++);

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await capture.AcquireInvokerAsync(default, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("identity");
        reads.ShouldBe(0);
        activities.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
        await capture.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenLoggerThrows_PreservesAcquisitionAndOwnedCleanup()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        var capture = new ToolProviderCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), owner,
            TimeProvider.System, new RecordingLogger<ToolProviderCapture> { ThrowOnWrite = true });

        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var closing = capture.DisposeAsync().AsTask();
        lease.Invoker.ShouldBeSameAs(invoker);
        await lease.DisposeAsync();
        await closing;

        owner.Calls.ShouldBe(1);
        invoker.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    public async Task DisposeAsync_WhenActivityListenerThrows_PreservesOwnershipAndParent(string stage)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), owner);
        using var parent = new Activity("throwing-capture-activity").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => stage == "sample" && IsCaptureOperation(options.Name) && options.Parent.TraceId == parent.TraceId
                ? throw new InvalidOperationException("observer failure") : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Fail("start", activity),
            ActivityStopped = activity => Fail("stop", activity),
        };
        ActivitySource.AddActivityListener(listener);

        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var closing = capture.DisposeAsync().AsTask();
        await lease.DisposeAsync();
        await closing;

        owner.Calls.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(parent);
        return;

        void Fail(string callback, Activity activity)
        {
            if (stage == callback && activity.TraceId == parent.TraceId && IsCaptureOperation(activity.OperationName))
            {
                throw new InvalidOperationException("observer failure");
            }
        }
    }

    [Fact]
    public async Task DisposeAsync_WhenMetricListenerThrows_PreservesOwnership()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var owner = new CaptureLifetimeProbe();
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, new CaptureTestToolInvoker()), owner);
        using var parent = new Activity("throwing-capture-metrics").Start();
        using var activities = Listen(parent, _ => { });
        var callbacks = 0;
        using var metrics = ListenMetrics(parent, (_, _, _) =>
        {
            _ = Interlocked.Increment(ref callbacks);
            throw new InvalidOperationException("observer failure");
        });

        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var closing = capture.DisposeAsync().AsTask();
        await lease.DisposeAsync();
        await closing;

        owner.Calls.ShouldBe(1);
        callbacks.ShouldBe(8);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AcquireInvokerAsync_WhenClockFailsOrReverses_OmitsUnknownDuration(int failure)
    {
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() =>
        {
            reads++;
            return reads == failure ? throw new InvalidOperationException("clock failure") : failure == 3 ? -reads : reads;
        });
        await using var capture = new ToolProviderCapture(ToolCaptureTestData.Snapshot([]), [], null, clock, NullLogger<ToolProviderCapture>.Instance);
        using var parent = new Activity("capture-clock-failure").Start();
        using var activities = Listen(parent, _ => { });
        List<string> names = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => names.Add(name), AgentKitActivityNames.ToolInvokerAcquire);

        _ = (await capture.AcquireInvokerAsync(new(new ToolId("absent"), new ToolVersion("1")), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();

        names.ShouldBe([AgentKitMetricNames.ToolProviderCaptureOperationCount]);
        reads.ShouldBe(failure == 1 ? 1 : 2);
    }

    private static void AssertExact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }

    private static void AssertSafe(RecordingLogEntry[] logs, Activity[] activities, string protectedContent)
    {
        foreach (var entry in logs)
        {
            entry.Message.ShouldNotContain(protectedContent);
            entry.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(protectedContent, StringComparison.Ordinal));
        }
        foreach (var activity in activities)
        {
            activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(protectedContent, StringComparison.Ordinal));
        }
    }

    private static void AssertMetricTags(KeyValuePair<string, object?>[] tags, string operation, string outcome)
    {
        tags.Select(static tag => tag.Key).ShouldBe([AgentKitTagNames.GenAiOperationName, AgentKitTagNames.Outcome]);
        tags[0].Value.ShouldBe(operation);
        tags[1].Value.ShouldBe(outcome);
    }

    private static bool IsCaptureOperation(string name) => name is AgentKitActivityNames.ToolInvokerAcquire
        or AgentKitActivityNames.ToolInvokerRelease or AgentKitActivityNames.ToolProviderCaptureClose or AgentKitActivityNames.ToolProviderCaptureDisposeResources;

    private static ActivityListener Listen(Activity parent, Action<Activity> stopped, string? operation = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && IsCaptureOperation(activity.OperationName) && (operation is null || activity.OperationName == operation))
                {
                    stopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener ListenMetrics(Activity parent, Action<string, double, KeyValuePair<string, object?>[]> measured, string? operation = null)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, observer) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolProviderCaptureOperationCount or AgentKitMetricNames.ToolProviderCaptureOperationDuration)
                {
                    observer.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
        return listener;

        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current is { } activity && activity.TraceId == parent.TraceId && IsCaptureOperation(activity.OperationName)
                && (operation is null || activity.OperationName == operation))
            {
                measured(instrument.Name, value, tags.ToArray());
            }
        }
    }
}
