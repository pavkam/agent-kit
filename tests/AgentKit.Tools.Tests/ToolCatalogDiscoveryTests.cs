// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Concurrent;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolCatalogDiscoveryTests
{
    [Fact]
    public void Constructor_WhenDependencyNull_RejectsBeforeObservationOrDiscovery()
    {
        var catalog = new CallbackToolRegistrationCatalog();
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        var captureLogger = NullLogger<ToolDiscoveryCapture>.Instance;
        var catalogLogger = NullLogger<ToolCatalogCapture>.Instance;
        Exact<ArgumentNullException>(() => _ = new ToolCatalogDiscovery(null!, clock, logger, captureLogger, catalogLogger), "registrations");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogDiscovery(catalog, null!, logger, captureLogger, catalogLogger), "timeProvider");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogDiscovery(catalog, clock, null!, captureLogger, catalogLogger), "logger");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogDiscovery(catalog, clock, logger, null!, catalogLogger), "captureLogger");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogDiscovery(catalog, clock, logger, captureLogger, null!), "catalogLogger");
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscoverAsync_WhenRequestNull_RejectsBeforeSelectionOrObservation()
    {
        var selections = 0;
        var catalog = new CallbackToolRegistrationCatalog { SelectRequest = (_, _) => { selections++; throw new InvalidOperationException(); } };
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        var reads = 0;
        var discovery = new ToolCatalogDiscovery(catalog, new CallbackTimestampTimeProvider(() => ++reads), logger,
            NullLogger<ToolDiscoveryCapture>.Instance, NullLogger<ToolCatalogCapture>.Instance);
        var failure = await Should.ThrowAsync<ArgumentNullException>(async () => await discovery.DiscoverAsync(null!, TestContext.Current.CancellationToken));
        failure.GetType().ShouldBe(typeof(ArgumentNullException));
        failure.ParamName.ShouldBe("request");
        selections.ShouldBe(0);
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoverAsync_WhenSelectionSubstitutesRequest_RejectsBeforeSources(bool returnNull)
    {
        var catalog = new CallbackToolRegistrationCatalog
        {
            SelectRequest = (_, _) => returnNull ? null! : new ToolDiscoverySelection(ToolCaptureTestData.Discovery("other-principal"), [], []),
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await Create(catalog).DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DiscoverAsync_WhenSourcesShared_PreservesAuthoredOrderAndDiscoversEachOnce()
    {
        var first = ToolCatalogMergeTestData.Source("first", []);
        var second = ToolCatalogMergeTestData.Source("second", []);
        var firstCapture = new CallbackToolProviderCapture(first);
        var secondCapture = new CallbackToolProviderCapture(second);
        List<ToolSourceId> calls = [];
        var a = Provider(first, firstCapture, () => calls.Add(first.SourceId));
        var b = Provider(second, secondCapture, () => calls.Add(second.SourceId));
        var shared = ToolCatalogMergeTestData.Toolset("shared", [second, first], []);
        var extra = ToolCatalogMergeTestData.Toolset("extra", [first], []);
        var (host, discovery, request) = Compose([shared, extra], [a, b]);
        await using var owningHost = host;

        var capture = await discovery.DiscoverAsync(request, TestContext.Current.CancellationToken);

        calls.ShouldBe([second.SourceId, first.SourceId]);
        capture.Selection.Request.ShouldBeSameAs(request);
        capture.Selection.Toolsets.ShouldBe([shared, extra]);
        capture.SourceSnapshots[first.SourceId].ShouldBeSameAs(first);
        capture.SourceSnapshots[second.SourceId].ShouldBeSameAs(second);
        firstCapture.SnapshotReads.ShouldBe(1);
        secondCapture.SnapshotReads.ShouldBe(1);
        firstCapture.Disposals.ShouldBe(0);
        await capture.DisposeAsync();
        await capture.DisposeAsync();
        firstCapture.Disposals.ShouldBe(1);
        secondCapture.Disposals.ShouldBe(1);
        a.Disposals.ShouldBe(0);
        b.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenEmptySelection_ReturnsEmptyOwnerWithoutFallbackDiscovery()
    {
        var snapshot = ToolCatalogMergeTestData.Source("unused", []);
        var provider = new CallbackToolProvider(snapshot.SourceId);
        var (host, discovery, _) = Compose([ToolCatalogMergeTestData.Toolset("unused", [snapshot], [])], [provider]);
        await using var owningHost = host;
        await using var capture = await discovery.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        capture.SourceSnapshots.ShouldBeEmpty();
        provider.Discoveries.ShouldBe(0);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("null-capture")]
    [InlineData("null-snapshot")]
    [InlineData("snapshot-throws")]
    [InlineData("wrong-source")]
    [InlineData("same-owner")]
    [InlineData("provider-identity")]
    public async Task DiscoverAsync_WhenLaterSourceMalformed_ReleasesEveryReturnedOwnerWithoutCallingFurtherSources(string problem)
    {
        var first = ToolCatalogMergeTestData.Source("first", []);
        var second = ToolCatalogMergeTestData.Source("second", []);
        var third = ToolCatalogMergeTestData.Source("third", []);
        var firstCapture = new CallbackToolProviderCapture(first);
        var secondCapture = new CallbackToolProviderCapture(second);
        var failure = new InvalidOperationException("private-provider-failure");
        var a = Provider(first, firstCapture);
        var b = Provider(second, secondCapture);
        var c = new CallbackToolProvider(third.SourceId);
        var (host, discovery, request) = Compose([ToolCatalogMergeTestData.Toolset("all", [first, second, third], [])], [a, b, c]);
        await using var owningHost = host;
        if (problem == "throw") { b.Discover = (_, _) => throw failure; }
        if (problem == "null-capture") { b.Discover = static (_, _) => ValueTask.FromResult<IToolProviderCapture>(null!); }
        if (problem == "null-snapshot") { secondCapture.ReadSnapshot = static () => null!; }
        if (problem == "snapshot-throws") { secondCapture.ReadSnapshot = () => throw failure; }
        if (problem == "wrong-source") { secondCapture.ReadSnapshot = () => first; }
        if (problem == "same-owner") { b.Discover = (_, _) => ValueTask.FromResult<IToolProviderCapture>(firstCapture); }
        if (problem == "provider-identity") { b.ReadSourceId = () => first.SourceId; }

        var error = await Should.ThrowAsync<InvalidOperationException>(async () => await discovery.DiscoverAsync(request, TestContext.Current.CancellationToken));

        if (problem is "throw" or "snapshot-throws") { error.ShouldBeSameAs(failure); }
        firstCapture.Disposals.ShouldBe(1);
        secondCapture.Disposals.ShouldBe(problem is "null-snapshot" or "snapshot-throws" or "wrong-source" ? 1 : 0);
        c.Discoveries.ShouldBe(0);
        a.Disposals.ShouldBe(0);
        b.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenCancelledBeforeEntry_DoesNotSelectOrDiscover()
    {
        var calls = 0;
        var registrations = new CallbackToolRegistrationCatalog { SelectRequest = (_, _) => { calls++; throw new InvalidOperationException(); } };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        (await Should.ThrowAsync<OperationCanceledException>(async () => await Create(registrations).DiscoverAsync(ToolCaptureTestData.Discovery(), cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenProviderReturnsAfterCancellation_ReleasesLateOwnerBeforePropagating()
    {
        var first = ToolCatalogMergeTestData.Source("first", []);
        var second = ToolCatalogMergeTestData.Source("second", []);
        var firstCapture = new CallbackToolProviderCapture(first);
        var secondCapture = new CallbackToolProviderCapture(second);
        var completion = new TaskCompletionSource<IToolProviderCapture>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Provider(first, firstCapture);
        var b = new CallbackToolProvider(second.SourceId) { Discover = (_, _) => { entered.SetResult(); return new(completion.Task); } };
        var (host, discovery, request) = Compose([ToolCatalogMergeTestData.Toolset("all", [first, second], [])], [a, b]);
        await using var owningHost = host;
        using var cancellation = new CancellationTokenSource();
        var pending = discovery.DiscoverAsync(request, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        pending.IsCompleted.ShouldBeFalse();
        completion.SetResult(secondCapture);

        (await Should.ThrowAsync<OperationCanceledException>(() => pending)).CancellationToken.ShouldBe(cancellation.Token);

        firstCapture.Disposals.ShouldBe(1);
        secondCapture.Disposals.ShouldBe(1);
        secondCapture.SnapshotReads.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenMetadataCancels_ReleasesOwnerBeforeTransfer()
    {
        var snapshot = ToolCatalogMergeTestData.Source("source", []);
        using var cancellation = new CancellationTokenSource();
        var source = new CallbackToolProviderCapture(snapshot) { ReadSnapshot = () => { cancellation.Cancel(); return snapshot; } };
        var (host, discovery, request) = Compose([ToolCatalogMergeTestData.Toolset("tools", [snapshot], [])], [Provider(snapshot, source)]);
        await using var owningHost = host;
        (await Should.ThrowAsync<OperationCanceledException>(async () => await discovery.DiscoverAsync(request, cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task DiscoverAsync_WhenDiscoveryAndCleanupsFail_StartsEveryCleanupAndPreservesOrderedFailures()
    {
        var z = ToolCatalogMergeTestData.Source("z", []);
        var a = ToolCatalogMergeTestData.Source("a", []);
        var last = ToolCatalogMergeTestData.Source("last", []);
        var zCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var zCapture = new CallbackToolProviderCapture(z) { Release = () => new(zCompletion.Task) };
        var aCapture = new CallbackToolProviderCapture(a) { Release = () => new(aCompletion.Task) };
        var original = new InvalidOperationException("discovery");
        var zFailure = new InvalidOperationException("z-cleanup");
        var aFailure = new InvalidOperationException("a-cleanup");
        var lastProvider = new CallbackToolProvider(last.SourceId) { Discover = (_, _) => throw original };
        var (host, discovery, request) = Compose([ToolCatalogMergeTestData.Toolset("tools", [z, a, last], [])], [Provider(z, zCapture), Provider(a, aCapture), lastProvider]);
        await using var owningHost = host;

        var pending = discovery.DiscoverAsync(request, TestContext.Current.CancellationToken).AsTask();

        zCapture.Disposals.ShouldBe(1);
        aCapture.Disposals.ShouldBe(1);
        pending.IsCompleted.ShouldBeFalse();
        zCompletion.SetException(zFailure);
        aCompletion.SetException(aFailure);
        var failure = await Should.ThrowAsync<AggregateException>(() => pending);
        failure.InnerExceptions.ShouldBe([original, aFailure, zFailure]);
    }

    [Fact]
    public async Task DiscoverAsync_WhenConcurrent_ReturnsIndependentOwnersAndRequestEvidence()
    {
        var sourceId = new ToolSourceId("shared");
        var snapshots = new ConcurrentBag<CallbackToolProviderCapture>();
        var provider = new CallbackToolProvider(sourceId)
        {
            Discover = (request, _) =>
            {
                var snapshot = ToolCatalogMergeTestData.Source(sourceId.Value, [], request.Identity.PrincipalId.Value);
                var source = new CallbackToolProviderCapture(snapshot);
                snapshots.Add(source);
                return ValueTask.FromResult<IToolProviderCapture>(source);
            },
        };
        var publication = ToolCatalogMergeTestData.Toolset("tools", [ToolCatalogMergeTestData.Source(sourceId.Value, [])], []);
        var (host, discovery, _) = Compose([publication], [provider]);
        await using var owningHost = host;
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(async () =>
        {
            var request = ToolCatalogMergeTestData.Request([publication], $"principal-{index}");
            await using var capture = await discovery.DiscoverAsync(request, TestContext.Current.CancellationToken);
            capture.Selection.Request.ShouldBeSameAs(request);
            capture.SourceSnapshots[sourceId].SourceVersion.Value.ShouldBe(request.Identity.PrincipalId.Value);
        }, TestContext.Current.CancellationToken)));
        snapshots.Count.ShouldBe(24);
        snapshots.ShouldAllBe(static source => source.Disposals == 1 && source.SnapshotReads == 1 && source.Acquisitions == 0);
        provider.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenMerged_TransfersRetainedPublicationAndExactInvokerThroughPublicComposition()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var invoker = new CaptureTestToolInvoker();
        var releases = 0;
        var source = new CallbackToolProviderCapture(candidate.Source)
        {
            Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(new ToolInvokerLease(candidate.Tool, candidate.Source.SourceVersion, invoker,
                () => { releases++; return ValueTask.CompletedTask; }))),
        };
        var (host, discovery, request) = Compose([candidate.Toolset], [Provider(candidate.Source, source)]);
        await using var owningHost = host;
        var discovered = await discovery.DiscoverAsync(request, TestContext.Current.CancellationToken);
        source.ReadSnapshot = static () => throw new InvalidOperationException("metadata must remain pinned");
        var (snapshot, _) = await host.GetRequiredService<ToolCatalogMerger>().MergeAsync(request, new ToolCatalogVersion("merged"), discovered.Selection.Toolsets,
            discovered.SourceSnapshots, TestContext.Current.CancellationToken);
        var catalog = discovered.TransferToCatalog(snapshot.ShouldNotBeNull(), TestContext.Current.CancellationToken);
        await discovered.DisposeAsync();
        source.Disposals.ShouldBe(0);
        var lease = (await catalog.AcquireInvokerAsync(candidate.Identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        lease.Invoker.ShouldBeSameAs(invoker);
        var closing = catalog.DisposeAsync().AsTask();
        closing.IsCompleted.ShouldBeFalse();
        await lease.DisposeAsync();
        await closing;
        source.SnapshotReads.ShouldBe(1);
        source.Disposals.ShouldBe(1);
        releases.ShouldBe(1);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenMergeRejects_KeepsAllSourceOwnersUntilCallerCleanup()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "first");
        var second = ToolCatalogMergeTestData.Candidate("second", "second");
        var a = new CallbackToolProviderCapture(first.Source);
        var b = new CallbackToolProviderCapture(second.Source);
        var (host, discovery, request) = Compose([first.Toolset, second.Toolset], [Provider(first.Source, a), Provider(second.Source, b)]);
        await using var owningHost = host;
        var discovered = await discovery.DiscoverAsync(request, TestContext.Current.CancellationToken);
        var (snapshot, context) = await host.GetRequiredService<ToolCatalogMerger>().MergeAsync(request, new ToolCatalogVersion("rejected"), discovered.Selection.Toolsets,
            discovered.SourceSnapshots, TestContext.Current.CancellationToken);
        snapshot.ShouldBeNull();
        context.Collisions.ShouldNotBeEmpty();
        a.Disposals.ShouldBe(0);
        b.Disposals.ShouldBe(0);
        await discovered.DisposeAsync();
        a.Disposals.ShouldBe(1);
        b.Disposals.ShouldBe(1);
        a.Acquisitions.ShouldBe(0);
        b.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task DiscoverAsync_WhenCancellationCleanupFails_PreservesCancellationAndCleanupFailure()
    {
        var snapshot = ToolCatalogMergeTestData.Source("source", []);
        using var cancellation = new CancellationTokenSource();
        var cleanup = new InvalidOperationException("cleanup");
        var source = new CallbackToolProviderCapture(snapshot) { Release = () => throw cleanup };
        var provider = Provider(snapshot, source, cancellation.Cancel);
        var (host, discovery, request) = Compose([ToolCatalogMergeTestData.Toolset("tools", [snapshot], [])], [provider]);
        await using var owningHost = host;
        var failure = await Should.ThrowAsync<AggregateException>(async () => await discovery.DiscoverAsync(request, cancellation.Token));
        failure.InnerExceptions.Count.ShouldBe(2);
        failure.InnerExceptions[0].ShouldBeOfType<OperationCanceledException>().CancellationToken.ShouldBe(cancellation.Token);
        failure.InnerExceptions[1].ShouldBeSameAs(cleanup);
        source.Disposals.ShouldBe(1);
        source.SnapshotReads.ShouldBe(0);
    }

    [Theory]
    [InlineData("discovered")]
    [InlineData("cancelled")]
    [InlineData("failed")]
    [InlineData("cleanup-failed")]
    public async Task DiscoverAsync_WhenObserved_KeepsSourceParentageAndSafeTerminalSignals(string outcome)
    {
        const string content = "private-publication-and-error-content";
        var tool = ToolCaptureTestData.Descriptor(description: content);
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var source = new CallbackToolProviderCapture(snapshot);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled") { source.ReadSnapshot = () => { cancellation.Cancel(); return snapshot; }; }
        if (outcome is "failed" or "cleanup-failed") { source.ReadSnapshot = static () => throw new InvalidOperationException(content); }
        if (outcome == "cleanup-failed") { source.Release = static () => throw new InvalidOperationException(content); }
        var provider = Provider(snapshot, source);
        var publication = ToolCatalogMergeTestData.Toolset("tools", [snapshot], [ToolCatalogMergeTestData.Alias(content, tool)]);
        var registrations = new ToolRegistrationCatalog([publication], [new(snapshot.SourceId, provider)], TimeProvider.System, NullLogger<ToolRegistrationCatalog>.Instance);
        var logger = new RecordingLogger<ToolCatalogDiscovery>();
        var discovery = new ToolCatalogDiscovery(registrations, TimeProvider.System, logger, NullLogger<ToolDiscoveryCapture>.Instance, NullLogger<ToolCatalogCapture>.Instance);
        var request = ToolCatalogMergeTestData.Request([publication]);
        using var parent = new Activity("discovery-pipeline").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static candidate => candidate.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { stopped.Enqueue(activity); } },
        };
        ActivitySource.AddActivityListener(listener);
        if (outcome == "discovered") { await using var capture = await discovery.DiscoverAsync(request, cancellation.Token); }
        else if (outcome == "cancelled") { _ = await Should.ThrowAsync<OperationCanceledException>(async () => await discovery.DiscoverAsync(request, cancellation.Token)); }
        else if (outcome == "cleanup-failed") { _ = await Should.ThrowAsync<AggregateException>(async () => await discovery.DiscoverAsync(request, cancellation.Token)); }
        else { _ = await Should.ThrowAsync<InvalidOperationException>(async () => await discovery.DiscoverAsync(request, cancellation.Token)); }
        var activity = stopped.Single(static item => item.OperationName == AgentKitActivityNames.ToolCatalogDiscover);
        var sourceActivity = stopped.Single(static item => item.OperationName == AgentKitActivityNames.ToolCatalogDiscoverSource);
        sourceActivity.ParentId.ShouldBe(activity.Id);
        activity.ParentId.ShouldBe(parent.Id);
        var terminal = outcome == "cleanup-failed" ? "failed" : outcome;
        activity.Status.ShouldBe(outcome == "discovered" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(terminal);
        sourceActivity.GetTagItem(AgentKitTagNames.ToolSourceId).ShouldBe(snapshot.SourceId.Value);
        foreach (var signal in stopped)
        {
            signal.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(request.Identity.PrincipalId.Value);
            signal.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
            signal.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(content, StringComparison.Ordinal));
        }
        foreach (var entry in logger.Snapshot())
        {
            entry.EventId.Id.ShouldBeOneOf(4080, 4081);
            entry.Category.ShouldBe(typeof(ToolCatalogDiscovery).FullName);
            entry.State["RunId"].ShouldBe(request.RunId);
            entry.State["PrincipalId"].ShouldBe(request.Identity.PrincipalId);
            entry.Message.ShouldNotContain(content);
            entry.State.Values.ShouldAllBe(value => value == null || !value.ToString()!.Contains(content, StringComparison.Ordinal));
        }
        logger.Snapshot().Last().State["Outcome"].ShouldBe(terminal);
        source.Disposals.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(parent);
    }

    private static ToolCatalogDiscovery Create(IToolRegistrationCatalog catalog) => new(catalog, TimeProvider.System,
        NullLogger<ToolCatalogDiscovery>.Instance, NullLogger<ToolDiscoveryCapture>.Instance, NullLogger<ToolCatalogCapture>.Instance);

    private static CallbackToolProvider Provider(ToolProviderSnapshot snapshot, IToolProviderCapture capture, Action? called = null) => new(snapshot.SourceId)
    {
        Discover = (_, _) => { called?.Invoke(); return ValueTask.FromResult(capture); },
    };

    private static (ServiceProvider Host, ToolCatalogDiscovery Discovery, ToolDiscoveryRequest Request) Compose(ImmutableArray<ToolsetPublication> toolsets, ImmutableArray<IToolProvider> providers)
    {
        var services = new ServiceCollection();
        foreach (var toolset in toolsets) { _ = services.AddToolset(toolset); }
        foreach (var provider in providers) { _ = services.AddToolProvider(provider.SourceId, provider); }
        _ = services.AddToolRegistrationCatalog();
        _ = services.AddToolCatalogMerging();
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return (host, host.GetRequiredService<ToolCatalogDiscovery>(), ToolCatalogMergeTestData.Request(toolsets));
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
