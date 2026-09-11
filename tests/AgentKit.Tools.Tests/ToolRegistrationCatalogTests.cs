// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolRegistrationCatalogTests: ToolRegistrationCatalogConformanceTests, IAsyncLifetime
{
    private readonly List<ServiceProvider> _hosts = [];

    protected override IToolRegistrationCatalog CreateCatalog(ImmutableArray<ToolsetPublication> toolsets, ImmutableArray<ToolProviderBinding> providers)
    {
        var services = new ServiceCollection();
        _ = services.AddToolRegistrationCatalog();
        foreach (var toolset in toolsets) { _ = services.AddToolset(toolset); }
        foreach (var binding in providers) { _ = services.AddToolProvider(binding.SourceId, binding.Provider); }
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _hosts.Add(host);
        return host.GetRequiredService<IToolRegistrationCatalog>();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts) { await host.DisposeAsync(); }
    }

    [Fact]
    public void Constructor_WhenInputsInvalid_RejectsBeforeClockLogsOrProviderCalls()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var provider = new CallbackToolProvider(candidate.Source.SourceId);
        var binding = new ToolProviderBinding(candidate.Source.SourceId, provider);
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<ToolRegistrationCatalog>();
        Exact<ArgumentNullException>(() => _ = new ToolRegistrationCatalog(null!, [], clock, logger), "toolsets");
        Exact<ArgumentNullException>(() => _ = new ToolRegistrationCatalog([], null!, clock, logger), "providers");
        Exact<ArgumentNullException>(() => _ = new ToolRegistrationCatalog([], [], null!, logger), "timeProvider");
        Exact<ArgumentNullException>(() => _ = new ToolRegistrationCatalog([], [], clock, null!), "logger");
        Exact<ArgumentNullException>(() => _ = new ToolRegistrationCatalog([null!], [], clock, logger), "toolsets");
        Exact<ArgumentNullException>(() => _ = new ToolRegistrationCatalog([], [null!], clock, logger), "providers");
        Exact<ArgumentException>(() => _ = new ToolRegistrationCatalog([candidate.Toolset, candidate.Toolset], [binding], clock, logger), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolRegistrationCatalog([], [binding, binding], clock, logger), "providers");
        Exact<ArgumentException>(() => _ = new ToolRegistrationCatalog([candidate.Toolset], [], clock, logger), "toolsets");
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
        provider.IdentityReads.ShouldBe(1);
        provider.Discoveries.ShouldBe(0);
        provider.Disposals.ShouldBe(0);
    }

    [Fact]
    public void ResolveSelection_WhenOriginalCollectionsChange_RetainsCapturedPublicationsAndBindings()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var provider = new CallbackToolProvider(candidate.Source.SourceId);
        var binding = new ToolProviderBinding(candidate.Source.SourceId, provider);
        List<ToolsetPublication> toolsets = [candidate.Toolset];
        List<ToolProviderBinding> providers = [binding];
        var catalog = new ToolRegistrationCatalog(toolsets, providers, TimeProvider.System, NullLogger<ToolRegistrationCatalog>.Instance);
        toolsets.Clear();
        providers.Clear();
        var selection = catalog.ResolveSelection(ToolCatalogMergeTestData.Request([candidate.Toolset]), TestContext.Current.CancellationToken);
        selection.Toolsets.ShouldBe([candidate.Toolset]);
        selection.Providers.ShouldBe([binding]);
    }

    [Fact]
    public void ResolveSelection_WhenRequestNull_RejectsBeforeObservation()
    {
        var reads = 0;
        var logger = new RecordingLogger<ToolRegistrationCatalog>();
        var catalog = new ToolRegistrationCatalog([], [], new CallbackTimestampTimeProvider(() => ++reads), logger);
        Exact<ArgumentNullException>(() => catalog.ResolveSelection(null!, TestContext.Current.CancellationToken), "request");
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void ResolveSelection_WhenNoListenersEnabled_ReturnsValidEmptySelection()
    {
        var catalog = new ToolRegistrationCatalog([], [], TimeProvider.System, NullLogger<ToolRegistrationCatalog>.Instance);
        catalog.ResolveSelection(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken).Providers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveSelection_WhenRegistrationsReplaced_RetainsOriginalDiscoveryAndInvokerBinding()
    {
        var original = ToolCatalogMergeTestData.Candidate();
        var newerSource = ToolCatalogMergeTestData.Source(original.Source.SourceId.Value, original.Source.Tools, "source-2");
        var newerToolset = ToolCatalogMergeTestData.Toolset(original.Toolset.Key.Value!, [newerSource], original.Toolset.Aliases, version: 2);
        var originalInvoker = new CaptureTestToolInvoker();
        var newerInvoker = new CaptureTestToolInvoker();
        var services = new ServiceCollection();
        _ = services.AddToolCatalogMerging();
        _ = services.AddToolset(original.Toolset);
        _ = services.AddStaticToolProvider(original.Source, ToolCaptureTestData.Bindings(original.Tool, originalInvoker));
        await using var oldHost = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var request = ToolCatalogMergeTestData.Request([original.Toolset]);
        var retained = oldHost.GetRequiredService<IToolRegistrationCatalog>().ResolveSelection(request, TestContext.Current.CancellationToken);

        _ = services.ReplaceToolset(newerToolset);
        _ = services.ReplaceStaticToolProvider(newerSource, ToolCaptureTestData.Bindings(original.Tool, newerInvoker));
        await using var newHost = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var newer = newHost.GetRequiredService<IToolRegistrationCatalog>().ResolveSelection(request, TestContext.Current.CancellationToken);

        await VerifyAsync(retained, oldHost, original.Source, originalInvoker, original.Toolset);
        await VerifyAsync(newer, newHost, newerSource, newerInvoker, newerToolset);
        originalInvoker.Invocations.ShouldBe(0);
        newerInvoker.Invocations.ShouldBe(0);
        originalInvoker.Disposals.ShouldBe(0);
        newerInvoker.Disposals.ShouldBe(0);
        return;

        async Task VerifyAsync(ToolDiscoverySelection selection, ServiceProvider host, ToolProviderSnapshot expectedSource, IToolInvoker expectedInvoker, ToolsetPublication expectedToolset)
        {
            selection.Toolsets.ShouldBe([expectedToolset]);
            await using var source = await selection.Providers.Single().Provider.DiscoverAsync(selection.Request, TestContext.Current.CancellationToken);
            source.Snapshot.ShouldBeSameAs(expectedSource);
            var (snapshot, _) = await host.GetRequiredService<ToolCatalogMerger>().MergeAsync(selection.Request, new ToolCatalogVersion(expectedSource.SourceVersion.Value),
                selection.Toolsets, ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(expectedSource.SourceId, source.Snapshot), TestContext.Current.CancellationToken);
            await using var catalog = new ToolCatalogCapture(snapshot.ShouldNotBeNull(), ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(expectedSource.SourceId, source),
                TimeProvider.System, NullLogger<ToolCatalogCapture>.Instance);
            await using var lease = (await catalog.AcquireInvokerAsync(original.Identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
            lease.Invoker.ShouldBeSameAs(expectedInvoker);
            catalog.Snapshot.SourceVersions[expectedSource.SourceId].ShouldBe(expectedSource.SourceVersion);
        }
    }


    [Theory]
    [InlineData("selected")]
    [InlineData("unavailable")]
    [InlineData("cancelled")]
    public void ResolveSelection_WhenObserved_ReportsSafeCorrelatedOutcome(string outcome)
    {
        const string content = "private-publication-content";
        var candidate = ToolCatalogMergeTestData.Candidate(alias: content, policy: content);
        var provider = new CallbackToolProvider(candidate.Source.SourceId);
        var logger = new RecordingLogger<ToolRegistrationCatalog>();
        long ticks = 0;
        var catalog = new ToolRegistrationCatalog([candidate.Toolset], [new(candidate.Source.SourceId, provider)],
            new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125)), logger);
        var request = ToolCatalogMergeTestData.Request(outcome == "unavailable" ? [ToolCatalogMergeTestData.Toolset("missing", [], [])] : [candidate.Toolset]);
        using var parent = new Activity("registration-selection").Start();
        Activity? observed = null;
        using var activities = Listen(parent, activity => observed = activity);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measured = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Add((name, value, tags)));
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled") { cancellation.Cancel(); }

        if (outcome == "cancelled")
        {
            Should.Throw<OperationCanceledException>(() => catalog.ResolveSelection(request, cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
        }
        else if (outcome == "unavailable") { _ = Should.Throw<InvalidOperationException>(() => catalog.ResolveSelection(request, cancellation.Token)); }
        else { catalog.ResolveSelection(request, cancellation.Token).Providers.Single().Provider.ShouldBeSameAs(provider); }

        var activity = observed.ShouldNotBeNull();
        activity.ParentId.ShouldBe(parent.Id);
        activity.OperationName.ShouldBe(AgentKitActivityNames.ToolRegistrationSelect);
        activity.Status.ShouldBe(outcome == "selected" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(request.Identity.TenantId.Value);
        activity.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(request.Identity.PrincipalId.Value);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(request.SessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
        if (outcome != "selected") { activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(outcome); }
        var logs = logger.Snapshot();
        logs.Select(static log => log.EventId.Id).ShouldBe([4070, 4071]);
        logs[0].Level.ShouldBe(LogLevel.Debug);
        logs[1].Level.ShouldBe(outcome switch { "selected" => LogLevel.Debug, "cancelled" => LogLevel.Information, _ => LogLevel.Warning });
        logs.ShouldAllBe(log => log.Category == typeof(ToolRegistrationCatalog).FullName);
        foreach (var log in logs)
        {
            log.State["TenantId"].ShouldBe(request.Identity.TenantId);
            log.State["PrincipalId"].ShouldBe(request.Identity.PrincipalId);
            log.State["AgentId"].ShouldBe(request.AgentId);
            log.State["SessionId"].ShouldBe(request.SessionId);
            log.State["RunId"].ShouldBe(request.RunId);
            log.Message.ShouldNotContain(content);
            log.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(content, StringComparison.Ordinal));
        }
        logs[1].State["Outcome"].ShouldBe(outcome);
        activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(content, StringComparison.Ordinal));
        measured.Count.ShouldBe(2);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolRegistrationSelectionCount).Value.ShouldBe(1);
        measured.Single(item => item.Name == AgentKitMetricNames.ToolRegistrationSelectionDuration).Value.ShouldBe(0.125);
        foreach (var (_, _, tags) in measured)
        {
            tags.Length.ShouldBe(1);
            tags[0].Key.ShouldBe(AgentKitTagNames.Outcome);
            tags[0].Value.ShouldBe(outcome);
        }
        provider.Discoveries.ShouldBe(0);
        provider.Disposals.ShouldBe(0);
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Theory]
    [InlineData("logger")]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("metric")]
    public void ResolveSelection_WhenObserversThrow_PreservesSelectionAndParent(string stage)
    {
        var catalog = new ToolRegistrationCatalog([], [], TimeProvider.System,
            new RecordingLogger<ToolRegistrationCatalog> { ThrowOnWrite = stage == "logger" });
        using var parent = new Activity("registration-throwing-observer").Start();
        var callbacks = 0;
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => stage == "sample" && options.Name == AgentKitActivityNames.ToolRegistrationSelect && options.Parent.TraceId == parent.TraceId
                ? throw new InvalidOperationException("observer") : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Fail("start", activity),
            ActivityStopped = activity => Fail("stop", activity),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = ListenMetrics(parent, (_, _, _) =>
        {
            if (stage == "metric") { _ = Interlocked.Increment(ref callbacks); throw new InvalidOperationException("observer"); }
        });
        var request = ToolCaptureTestData.Discovery();

        catalog.ResolveSelection(request, TestContext.Current.CancellationToken).Request.ShouldBeSameAs(request);

        Activity.Current.ShouldBeSameAs(parent);
        if (stage == "metric") { callbacks.ShouldBe(2); }
        return;
        void Fail(string callback, Activity activity)
        {
            if (stage == callback && activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolRegistrationSelect) { throw new InvalidOperationException("observer"); }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ResolveSelection_WhenClockFailsOrReverses_OmitsUnknownDuration(int failure)
    {
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => { reads++; return reads == failure ? throw new InvalidOperationException("clock") : failure == 3 ? -reads : reads; });
        var catalog = new ToolRegistrationCatalog([], [], clock, NullLogger<ToolRegistrationCatalog>.Instance);
        using var parent = new Activity("registration-clock-failure").Start();
        using var activities = Listen(parent, _ => { });
        List<string> names = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => names.Add(name));

        catalog.ResolveSelection(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken).Providers.ShouldBeEmpty();

        names.ShouldBe([AgentKitMetricNames.ToolRegistrationSelectionCount]);
        reads.ShouldBe(failure == 1 ? 1 : 2);
    }

    [Fact]
    public async Task ResolveSelection_WhenConcurrent_KeepsDiagnosticCorrelationIndependent()
    {
        var logger = new RecordingLogger<ToolRegistrationCatalog>();
        var catalog = new ToolRegistrationCatalog([], [], TimeProvider.System, logger);
        var observed = new ConcurrentQueue<Activity>();
        var expected = new ConcurrentDictionary<string, ToolDiscoveryRequest>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.ToolRegistrationSelect && expected.ContainsKey(activity.ParentId ?? "")) { observed.Enqueue(activity); }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var first = ToolCaptureTestData.Discovery("first", new RunId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        var second = ToolCaptureTestData.Discovery("second", new RunId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        await Task.WhenAll(new[] { first, second }.Select(request => Task.Run(() =>
        {
            using var parent = new Activity("concurrent-registration-request").Start();
            expected[parent.Id!] = request;
            catalog.ResolveSelection(request, TestContext.Current.CancellationToken).Request.ShouldBeSameAs(request);
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
                if (activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolRegistrationSelect) { stopped(activity); }
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
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolRegistrationSelectionCount or AgentKitMetricNames.ToolRegistrationSelectionDuration) { observer.EnableMeasurementEvents(instrument); }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        listener.Start();
        return listener;
        void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current is { } activity && activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolRegistrationSelect) { measured(instrument.Name, value, tags.ToArray()); }
        }
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
