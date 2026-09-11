// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class StaticToolProviderTests: ToolProviderConformanceTests, IAsyncLifetime
{
    private readonly List<ServiceProvider> _hosts = [];

    protected override IToolProvider CreateProvider(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers)
    {
        var services = new ServiceCollection();
        _ = services.AddStaticToolProvider(snapshot, invokers);
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _hosts.Add(host);
        return host.GetRequiredKeyedService<IToolProvider>(snapshot.SourceId);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }
    }

    [Fact]
    public void Constructor_WhenReferencesInvalid_RejectsBeforeClockLoggingOrInvocation()
    {
        var snapshot = ToolCaptureTestData.Snapshot([]);
        var clockReads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++clockReads);
        var logger = new RecordingLogger<StaticToolProvider>();
        var captureLogger = new RecordingLogger<ToolProviderCapture>();
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(null!, [], clock, logger, captureLogger), "snapshot");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(snapshot, null!, clock, logger, captureLogger), "invokers");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(snapshot, [], null!, logger, captureLogger), "timeProvider");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(snapshot, [], clock, null!, captureLogger), "logger");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(snapshot, [], clock, logger, null!), "captureLogger");
        var bindings = new ToolProviderBindings(snapshot, []);
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(null!, clock, logger, captureLogger), "bindings");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(bindings, null!, logger, captureLogger), "timeProvider");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(bindings, clock, null!, captureLogger), "logger");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(bindings, clock, logger, null!), "captureLogger");
        clockReads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
        captureLogger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenBindingsDoNotMatchPublication_RejectsBeforeProviderCanDiscover()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var bindings = ToolCaptureTestData.Bindings(tool, invoker);
        Exact<ArgumentException>(() => _ = new StaticToolProvider(snapshot, [], TimeProvider.System, NullLogger<StaticToolProvider>.Instance, NullLogger<ToolProviderCapture>.Instance), "invokers");
        Exact<ArgumentException>(() => _ = new StaticToolProvider(ToolCaptureTestData.Snapshot([]), bindings, TimeProvider.System, NullLogger<StaticToolProvider>.Instance, NullLogger<ToolProviderCapture>.Instance), "invokers");
        Exact<ArgumentOutOfRangeException>(() => _ = new StaticToolProvider(snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty.Add(default, invoker), TimeProvider.System, NullLogger<StaticToolProvider>.Instance, NullLogger<ToolProviderCapture>.Instance), "invokers");
        Exact<ArgumentNullException>(() => _ = new StaticToolProvider(snapshot, bindings.SetItem(new(tool.Id, tool.Version), null!), TimeProvider.System, NullLogger<StaticToolProvider>.Instance, NullLogger<ToolProviderCapture>.Instance), "invokers");
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenModelOrSelectionDiffers_ReturnsConfiguredMetadataForCatalogPreflight()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var provider = CreateProvider(snapshot, ToolCaptureTestData.Bindings(tool, new CaptureTestToolInvoker()));
        var request = ToolCaptureTestData.Discovery();
        var unsupportedModel = new ToolDiscoveryRequest(request.AgentId, request.SessionId, request.RunId, request.Identity, request.Authorization,
            request.AgentDefinitionRevision, request.Configuration, [], new ModelCapabilities(false, false, false, false, false, false, false, ExtensionData.Empty));

        var result = provider.DiscoverAsync(unsupportedModel, TestContext.Current.CancellationToken);
        result.IsCompletedSuccessfully.ShouldBeTrue();
        await using var capture = await result;
        capture.Snapshot.ShouldBeSameAs(snapshot);
        provider.SourceId.ShouldBe(snapshot.SourceId);
    }

    [Fact]
    public async Task DiscoverAsync_WhenCallerTokenCancelsAfterTransfer_DoesNotCloseReturnedCapture()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var provider = CreateProvider(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker));
        using var cancellation = new CancellationTokenSource();
        await using var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), cancellation.Token);

        cancellation.Cancel();

        await using var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        lease.Invoker.ShouldBeSameAs(invoker);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenNestedInCatalog_CatalogClosureKeepsBorrowedInvokerHostOwned()
    {
        var services = new ServiceCollection();
        _ = services.AddScoped<CaptureTestToolInvoker>();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = host.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<CaptureTestToolInvoker>();
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var provider = CreateProvider(publication, ToolCaptureTestData.Bindings(tool, invoker));
        var source = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        var catalog = new ToolCatalogCapture(ToolCaptureTestData.Catalog([publication], [tool]), ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(provider.SourceId, source),
            TimeProvider.System, NullLogger<ToolCatalogCapture>.Instance);
        var lease = (await catalog.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var closing = catalog.DisposeAsync().AsTask();
        closing.IsCompleted.ShouldBeFalse();
        lease.Invoker.ShouldBeSameAs(invoker);
        await lease.DisposeAsync();
        await closing;
        invoker.Disposals.ShouldBe(0);
        await scope.DisposeAsync();
        invoker.Disposals.ShouldBe(1);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoverAsync_WhenObserved_ReportsSafeCorrelatedOutcome(bool cancelled)
    {
        const string content = "descriptor-content-must-stay-private";
        var tool = ToolCaptureTestData.Descriptor(description: content);
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var logger = new RecordingLogger<StaticToolProvider>();
        long ticks = 0;
        var provider = new StaticToolProvider(snapshot, ToolCaptureTestData.Bindings(tool, new CaptureTestToolInvoker()),
            new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125)), logger, NullLogger<ToolProviderCapture>.Instance);
        var request = ToolCaptureTestData.Discovery();
        using var parent = new Activity("static-provider-discovery").Start();
        Activity? observed = null;
        using var activities = Listen(parent, activity => observed = activity);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measured = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Add((name, value, tags)));
        using var cancellation = new CancellationTokenSource();
        if (cancelled) { cancellation.Cancel(); }
        IToolProviderCapture? capture = null;
        try
        {
            if (cancelled)
            {
                (await Should.ThrowAsync<OperationCanceledException>(async () => await provider.DiscoverAsync(request, cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
            }
            else { capture = await provider.DiscoverAsync(request, cancellation.Token); }
            var outcome = cancelled ? "cancelled" : "discovered";
            var activity = observed.ShouldNotBeNull();
            activity.ParentId.ShouldBe(parent.Id);
            activity.OperationName.ShouldBe(AgentKitActivityNames.ToolProviderDiscover);
            activity.Status.ShouldBe(cancelled ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
            activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(request.Identity.TenantId.Value);
            activity.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(request.Identity.PrincipalId.Value);
            activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
            activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(request.SessionId.ToString());
            activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
            activity.GetTagItem(AgentKitTagNames.ToolSourceId).ShouldBe(snapshot.SourceId.Value);
            activity.GetTagItem(AgentKitTagNames.ToolSourceVersion).ShouldBe(snapshot.SourceVersion.Value);
            if (cancelled) { activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe("cancelled"); }
            var logs = logger.Snapshot();
            logs.Select(static log => log.EventId.Id).ShouldBe([4050, 4051]);
            logs[0].Level.ShouldBe(LogLevel.Debug);
            logs[1].Level.ShouldBe(cancelled ? LogLevel.Information : LogLevel.Debug);
            logs.ShouldAllBe(log => log.Category == typeof(StaticToolProvider).FullName);
            foreach (var log in logs)
            {
                log.State["TenantId"].ShouldBe(request.Identity.TenantId);
                log.State["PrincipalId"].ShouldBe(request.Identity.PrincipalId);
                log.State["AgentId"].ShouldBe(request.AgentId);
                log.State["SessionId"].ShouldBe(request.SessionId);
                log.State["RunId"].ShouldBe(request.RunId);
                log.State["SourceId"].ShouldBe(snapshot.SourceId);
                log.Message.ShouldNotContain(content);
                log.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(content, StringComparison.Ordinal));
            }
            logs[1].State["SourceVersion"].ShouldBe(snapshot.SourceVersion);
            activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(content, StringComparison.Ordinal));
            measured.Count.ShouldBe(2);
            measured.Single(item => item.Name == AgentKitMetricNames.ToolProviderDiscoveryCount).Value.ShouldBe(1);
            measured.Single(item => item.Name == AgentKitMetricNames.ToolProviderDiscoveryDuration).Value.ShouldBe(0.125);
            foreach (var (Name, Value, Tags) in measured)
            {
                Tags.Length.ShouldBe(1);
                Tags[0].Key.ShouldBe(AgentKitTagNames.Outcome);
                Tags[0].Value.ShouldBe(outcome);
            }
        }
        finally { if (capture is not null) { await capture.DisposeAsync(); } }
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Theory]
    [InlineData("logger")]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("metric")]
    public async Task DiscoverAsync_WhenObserversThrow_PreservesCaptureAndParent(string stage)
    {
        var snapshot = ToolCaptureTestData.Snapshot([]);
        var provider = new StaticToolProvider(snapshot, [], TimeProvider.System,
            new RecordingLogger<StaticToolProvider> { ThrowOnWrite = stage == "logger" }, NullLogger<ToolProviderCapture>.Instance);
        using var parent = new Activity("provider-throwing-observer").Start();
        var callbacks = 0;
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => stage == "sample" && options.Name == AgentKitActivityNames.ToolProviderDiscover && options.Parent.TraceId == parent.TraceId
                ? throw new InvalidOperationException("observer") : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Fail("start", activity),
            ActivityStopped = activity => Fail("stop", activity),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = ListenMetrics(parent, (_, _, _) =>
        {
            if (stage == "metric") { _ = Interlocked.Increment(ref callbacks); throw new InvalidOperationException("observer"); }
        });

        await using var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);

        capture.Snapshot.ShouldBeSameAs(snapshot);
        Activity.Current.ShouldBeSameAs(parent);
        if (stage == "metric") { callbacks.ShouldBe(2); }
        return;
        void Fail(string callback, Activity activity)
        {
            if (stage == callback && activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolProviderDiscover) { throw new InvalidOperationException("observer"); }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task DiscoverAsync_WhenClockFailsOrReverses_OmitsUnknownDuration(int failure)
    {
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => { reads++; return reads == failure ? throw new InvalidOperationException("clock") : failure == 3 ? -reads : reads; });
        var provider = new StaticToolProvider(ToolCaptureTestData.Snapshot([]), [], clock, NullLogger<StaticToolProvider>.Instance, NullLogger<ToolProviderCapture>.Instance);
        using var parent = new Activity("provider-clock-failure").Start();
        using var activities = Listen(parent, _ => { });
        List<string> names = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => names.Add(name));

        await using var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);

        names.ShouldBe([AgentKitMetricNames.ToolProviderDiscoveryCount]);
        reads.ShouldBe(failure == 1 ? 1 : 2);
    }

    [Fact]
    public async Task DiscoverAsync_WhenRequestInvalid_RejectsBeforeDiagnostics()
    {
        var logger = new RecordingLogger<StaticToolProvider>();
        var reads = 0;
        var provider = new StaticToolProvider(ToolCaptureTestData.Snapshot([]), [], new CallbackTimestampTimeProvider(() => ++reads), logger, NullLogger<ToolProviderCapture>.Instance);
        using var parent = new Activity("invalid-provider-request").Start();
        var count = 0;
        using var activities = Listen(parent, _ => count++);

        var error = await Should.ThrowAsync<ArgumentNullException>(async () => await provider.DiscoverAsync(null!, TestContext.Current.CancellationToken));

        error.ParamName.ShouldBe("request");
        provider.SourceId.ShouldBe(new ToolSourceId("source.tests"));
        reads.ShouldBe(0);
        count.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscoverAsync_WhenRunsUseDifferentIdentities_KeepsConcurrentCorrelationIndependent()
    {
        var snapshot = new ToolProviderSnapshot(new ToolSourceId("concurrent.discovery.source"), new ToolSourceVersion("1"), []);
        var logger = new RecordingLogger<StaticToolProvider>();
        var provider = new StaticToolProvider(snapshot, [], TimeProvider.System, logger, NullLogger<ToolProviderCapture>.Instance);
        var first = ToolCaptureTestData.Discovery("first", new RunId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        var second = ToolCaptureTestData.Discovery("second", new RunId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        var observed = new ConcurrentQueue<Activity>();
        var expected = new ConcurrentDictionary<string, ToolDiscoveryRequest>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.ToolProviderDiscover && (string?) activity.GetTagItem(AgentKitTagNames.ToolSourceId) == snapshot.SourceId.Value) { observed.Enqueue(activity); }
            },
        };
        ActivitySource.AddActivityListener(listener);

        await Task.WhenAll(new[] { first, second }.Select(request => Task.Run(async () =>
        {
            using var parent = new Activity("concurrent-provider-request").Start();
            expected[parent.Id!] = request;
            await using var capture = await provider.DiscoverAsync(request, TestContext.Current.CancellationToken);
            capture.Snapshot.ShouldBeSameAs(snapshot);
            Activity.Current.ShouldBeSameAs(parent);
        }, TestContext.Current.CancellationToken)));

        observed.Count.ShouldBe(2);
        foreach (var activity in observed)
        {
            var request = expected[activity.ParentId!];
            activity.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(request.Identity.PrincipalId.Value);
            activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
        }
        foreach (var request in new[] { first, second })
        {
            var logs = logger.Snapshot().Where(entry => Equals(entry.State["RunId"], request.RunId)).ToArray();
            logs.Length.ShouldBe(2);
            logs.ShouldAllBe(log => Equals(log.State["PrincipalId"], request.Identity.PrincipalId));
        }
    }

    private static ActivityListener Listen(Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolProviderDiscover) { stopped(activity); }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener ListenMetrics(Activity parent, Action<string, double, KeyValuePair<string, object?>[]> measured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, observer) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolProviderDiscoveryCount or AgentKitMetricNames.ToolProviderDiscoveryDuration) { observer.EnableMeasurementEvents(instrument); }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
        return listener;
        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current is { } activity && activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolProviderDiscover) { measured(instrument.Name, value, tags.ToArray()); }
        }
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
