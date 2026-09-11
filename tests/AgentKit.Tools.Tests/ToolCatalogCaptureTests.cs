// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolCatalogCaptureTests: ToolCatalogCaptureConformanceTests
{
    protected override IToolCatalogCapture CreateCapture(ToolCatalogSnapshot snapshot, ImmutableDictionary<ToolSourceId, IToolProviderCapture> sources) =>
        new ToolCatalogCapture(snapshot, sources, TimeProvider.System, NullLogger<ToolCatalogCapture>.Instance);

    [Fact]
    public void Constructor_WhenRetainedPublicationMapInvalid_RejectsBeforeLiveMetadataOrOwnership()
    {
        var publication = ToolCaptureTestData.Snapshot([]);
        var source = new CallbackToolProviderCapture(publication);
        var snapshot = ToolCaptureTestData.Catalog([publication], []);
        var sources = Sources(publication, source);
        var retained = ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(publication.SourceId, publication);
        var clock = TimeProvider.System;
        var logger = NullLogger<ToolCatalogCapture>.Instance;
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(null!, sources, retained, clock, logger), "snapshot");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, null!, retained, clock, logger), "sources");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, sources, null!, clock, logger), "sourceSnapshots");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, sources, retained, null!, logger), "timeProvider");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, sources, retained, clock, null!), "logger");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, sources, retained.SetItem(publication.SourceId, null!), clock, logger), "sourceSnapshots");
        AssertExact<ArgumentOutOfRangeException>(() => _ = new ToolCatalogCapture(snapshot, sources, retained.Add(default, publication), clock, logger), "sourceSnapshots");
        AssertExact<ArgumentException>(() => _ = new ToolCatalogCapture(snapshot, sources, [], clock, logger), "sourceSnapshots");
        AssertExact<ArgumentException>(() => _ = new ToolCatalogCapture(snapshot, sources, retained.Add(new("extra"), publication), clock, logger), "sourceSnapshots");
        var other = ToolCatalogMergeTestData.Source("other", []);
        AssertExact<ArgumentException>(() => _ = new ToolCatalogCapture(snapshot, sources, retained.SetItem(publication.SourceId, other), clock, logger), "sources");
        source.SnapshotReads.ShouldBe(0);
        source.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task Constructor_WhenTwoSourcesReuseOneOwner_RejectsBeforeMetadataOrOwnershipTransfer()
    {
        var first = ToolCatalogMergeTestData.Source("first", []);
        var second = ToolCatalogMergeTestData.Source("second", []);
        var reads = 0;
        var source = new CallbackToolProviderCapture(first) { ReadSnapshot = () => ++reads == 1 ? first : second };
        var sources = ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(first.SourceId, source).Add(second.SourceId, source);
        ToolCatalogCapture? unexpected = null;
        try
        {
            AssertExact<ArgumentException>(() => unexpected = new ToolCatalogCapture(ToolCaptureTestData.Catalog([first, second], []), sources,
                TimeProvider.System, NullLogger<ToolCatalogCapture>.Instance), "sources");
            source.SnapshotReads.ShouldBe(0);
            source.Disposals.ShouldBe(0);
        }
        finally { if (unexpected is not null) { await unexpected.DisposeAsync(); } }
    }

    [Fact]
    public void Constructor_WhenLocalArgumentsInvalid_RejectsBeforeReadingSourceOrTakingOwnership()
    {
        var publication = ToolCaptureTestData.Snapshot([]);
        var source = new CallbackToolProviderCapture(publication);
        var snapshot = ToolCaptureTestData.Catalog([publication], []);
        var sources = Sources(publication, source);
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<ToolCatalogCapture>();

        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(null!, sources, clock, logger), "snapshot");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, null!, clock, logger), "sources");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, sources, null!, logger), "timeProvider");
        AssertExact<ArgumentNullException>(() => _ = new ToolCatalogCapture(snapshot, sources, clock, null!), "logger");
        AssertExact<ArgumentNullException>(() => CreateCapture(snapshot, sources.SetItem(publication.SourceId, null!)), "sources");
        AssertExact<ArgumentOutOfRangeException>(() => CreateCapture(snapshot, ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(default, source)), "sources");
        AssertExact<ArgumentException>(() => CreateCapture(snapshot, []), "sources");
        AssertExact<ArgumentException>(() => CreateCapture(ToolCaptureTestData.Catalog([], []), sources), "sources");
        AssertExact<ArgumentException>(() => CreateCapture(snapshot, sources.Add(new("extra"), source)), "sources");
        AssertExact<ArgumentException>(() => CreateCapture(snapshot, ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(new("wrong"), source)), "sources");

        source.SnapshotReads.ShouldBe(0);
        source.Disposals.ShouldBe(0);
        reads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenSourceMapComparerChangesIdentity_NormalizesBeforeReadingMetadata()
    {
        var publication = ToolCaptureTestData.Snapshot([]);
        var source = new CallbackToolProviderCapture(publication);
        var snapshot = ToolCaptureTestData.Catalog([publication], []);
        var insensitive = EqualityComparer<ToolSourceId>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Equals(a.Value, b.Value), static value => StringComparer.OrdinalIgnoreCase.GetHashCode(value.Value));
        var unequal = EqualityComparer<ToolSourceId>.Create(static (_, _) => false, static value => value.GetHashCode());

        AssertExact<ArgumentException>(() => CreateCapture(snapshot, ImmutableDictionary.Create<ToolSourceId, IToolProviderCapture>(insensitive).Add(new("SOURCE.TESTS"), source)), "sources");
        AssertExact<ArgumentException>(() => CreateCapture(snapshot, ImmutableDictionary.Create<ToolSourceId, IToolProviderCapture>(unequal).Add(publication.SourceId, source).Add(publication.SourceId, source)), "sources");
        source.SnapshotReads.ShouldBe(0);
        source.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("missing")]
    [InlineData("descriptor")]
    [InlineData("throw")]
    public void Constructor_WhenPublicationDisagreesOrThrows_LeavesAllOwnershipWithCaller(string fault)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var snapshot = ToolCaptureTestData.Catalog([publication], [tool]);
        var failure = new InvalidOperationException("controlled metadata failure");
        var source = new CallbackToolProviderCapture(publication)
        {
            ReadSnapshot = () => fault switch
            {
                "null" => null!,
                "source" => new ToolProviderSnapshot(new("other"), publication.SourceVersion, []),
                "version" => ToolCaptureTestData.Snapshot([tool], "new-version"),
                "missing" => ToolCaptureTestData.Snapshot([]),
                "descriptor" => ToolCaptureTestData.Snapshot([ToolCaptureTestData.Descriptor(description: "changed content")]),
                _ => throw failure,
            },
        };

        if (fault is "throw")
        {
            Should.Throw<InvalidOperationException>(() => CreateCapture(snapshot, Sources(publication, source))).ShouldBeSameAs(failure);
        }
        else if (fault is "null")
        {
            AssertExact<ArgumentNullException>(() => CreateCapture(snapshot, Sources(publication, source)), "sources");
        }
        else
        {
            AssertExact<ArgumentException>(() => CreateCapture(snapshot, Sources(publication, source)), "sources");
        }
        source.SnapshotReads.ShouldBe(1);
        source.Acquisitions.ShouldBe(0);
        source.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("content")]
    [InlineData("null-tool")]
    [InlineData("null-invoker")]
    [InlineData("throw-tool")]
    [InlineData("throw-invoker")]
    public async Task AcquireInvokerAsync_WhenLeaseViolatesCapturedBinding_ReleasesItBeforeRejecting(string fault)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var returnedTool = fault switch
        {
            "tool" => ToolCaptureTestData.Descriptor("other"),
            "source" => ToolCaptureTestData.Descriptor(source: "other"),
            "content" => ToolCaptureTestData.Descriptor(description: "changed descriptor content"),
            "null-tool" => null!,
            _ => tool,
        };
        var inner = new CallbackToolInvokerLease(returnedTool, fault == "version" ? new("changed") : publication.SourceVersion,
            fault == "null-invoker" ? null! : new CaptureTestToolInvoker());
        var getterFailure = new InvalidOperationException("controlled getter failure");
        if (fault is "throw-tool") { inner.ReadTool = () => throw getterFailure; }
        if (fault is "throw-invoker") { inner.ReadInvoker = () => throw getterFailure; }
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner)) };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source));

        var error = await Should.ThrowAsync<InvalidOperationException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken));

        error.GetType().ShouldBe(typeof(InvalidOperationException));
        if (fault.StartsWith("throw", StringComparison.Ordinal)) { error.ShouldBeSameAs(getterFailure); }
        inner.Disposals.ShouldBe(1);
        await capture.DisposeAsync();
        source.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquireInvokerAsync_WhenSourceResultIsInvalid_RejectsAndAllowsClosure(bool nullResult)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(nullResult ? null! : new ToolInvokerUnavailable(new(new ToolId("other"), tool.Version), "unavailable")) };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source));

        var error = await Should.ThrowAsync<InvalidOperationException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken));

        error.GetType().ShouldBe(typeof(InvalidOperationException));
        await capture.DisposeAsync();
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenRejectionCleanupFails_RetainsBothFailuresWithoutRetry()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var cleanup = new InvalidOperationException("controlled release failure");
        var inner = new CallbackToolInvokerLease(tool, new("wrong-version"), new CaptureTestToolInvoker()) { Release = () => throw cleanup };
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner)) };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source));

        var error = await Should.ThrowAsync<AggregateException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken));

        error.InnerExceptions.Count.ShouldBe(2);
        _ = error.InnerExceptions[0].ShouldBeOfType<InvalidOperationException>();
        error.InnerExceptions[1].ShouldBeSameAs(cleanup);
        await capture.DisposeAsync();
        inner.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenMultipleSourcesFail_StartsEveryCleanupAndSharesOrderedFailures()
    {
        var a = new ToolProviderSnapshot(new("a-source"), new("1"), []);
        var z = new ToolProviderSnapshot(new("z-source"), new("2"), []);
        var firstFailure = new InvalidOperationException("first failure");
        var lastFailure = new OperationCanceledException("last failure");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new CallbackToolProviderCapture(a) { Release = () => new(finish.Task) };
        var last = new CallbackToolProviderCapture(z) { Release = () => throw lastFailure };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([z, a], []), Sources(z, last).Add(a.SourceId, first));

        var close = capture.DisposeAsync().AsTask();
        var repeated = capture.DisposeAsync().AsTask();
        close.IsCompleted.ShouldBeFalse();
        repeated.IsCompleted.ShouldBeFalse();
        first.Disposals.ShouldBe(1);
        last.Disposals.ShouldBe(1);
        finish.SetException(firstFailure);
        var error = await Should.ThrowAsync<AggregateException>(async () => await close);

        error.InnerExceptions.ShouldBe([firstFailure, lastFailure]);
        (await Should.ThrowAsync<AggregateException>(async () => await repeated)).ShouldBeSameAs(error);
        (await Should.ThrowAsync<AggregateException>(async () => await capture.DisposeAsync())).ShouldBeSameAs(error);
        first.Disposals.ShouldBe(1);
        last.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenLeaseAndSourceCleanupFail_ReleasesAllOwnersAndPreservesFailures()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var leaseFailure = new InvalidOperationException("lease failure");
        var sourceFailure = new InvalidOperationException("source failure");
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker()) { Release = () => throw leaseFailure };
        var source = new CallbackToolProviderCapture(publication)
        {
            Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner)),
            Release = () => throw sourceFailure,
        };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source));
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var close = capture.DisposeAsync().AsTask();

        var error = await Should.ThrowAsync<AggregateException>(async () => await lease.DisposeAsync());

        error.InnerExceptions.ShouldBe([leaseFailure, sourceFailure]);
        (await Should.ThrowAsync<AggregateException>(async () => await lease.DisposeAsync())).ShouldBeSameAs(error);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await close)).ShouldBeSameAs(sourceFailure);
        inner.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenSourceReentersClosure_ReturnsUnavailableWithoutDeadlock()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker());
        IToolCatalogCapture? capture = null;
        Task? close = null;
        var source = new CallbackToolProviderCapture(publication)
        {
            Acquire = (_, _) =>
        {
            close = capture!.DisposeAsync().AsTask();
            return ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner));
        }
        };
        capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source));

        _ = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();
        await close!;

        inner.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_WhenComposedWithRealSourceScopes_PreservesOwnedAndBorrowedLifetimes(bool owned)
    {
        var services = new ServiceCollection();
        _ = services.AddScoped<CaptureTestToolInvoker>();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = host.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<CaptureTestToolInvoker>();
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var source = new ToolProviderCapture(publication, ToolCaptureTestData.Bindings(tool, invoker), owned ? scope : null, TimeProvider.System, NullLogger<ToolProviderCapture>.Instance);
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source));
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var close = capture.DisposeAsync().AsTask();
        invoker.Disposals.ShouldBe(0);
        lease.Invoker.ShouldBeSameAs(invoker);
        await lease.DisposeAsync();
        await close;
        invoker.Disposals.ShouldBe(owned ? 1 : 0);
        if (!owned) { await scope.DisposeAsync(); }
        invoker.Disposals.ShouldBe(1);
        invoker.Invocations.ShouldBe(0);
    }


    [Theory]
    [InlineData("acquired", LogLevel.Debug)]
    [InlineData("unavailable", LogLevel.Warning)]
    [InlineData("cancelled", LogLevel.Information)]
    [InlineData("failed", LogLevel.Error)]
    public async Task AcquireInvokerAsync_WhenObserved_ReportsSafeCorrelatedOutcome(string outcome, LogLevel level)
    {
        const string secret = "descriptor-and-source-error-content";
        var tool = ToolCaptureTestData.Descriptor(description: secret);
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker());
        var source = new CallbackToolProviderCapture(publication)
        {
            Acquire = (identity, _) => outcome switch
            {
                "acquired" => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner)),
                "failed" => throw new InvalidOperationException(secret),
                _ => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerUnavailable(identity, secret)),
            },
        };
        var snapshot = ToolCaptureTestData.Catalog([publication], [tool]);
        var logger = new RecordingLogger<ToolCatalogCapture>();
        long ticks = 0;
        var capture = new ToolCatalogCapture(snapshot, Sources(publication, source), new CallbackTimestampTimeProvider(() => Interlocked.Add(ref ticks, 125)), logger);
        using var parent = new Activity("catalog-acquisition-observation").Start();
        Activity? observed = null;
        using var activities = Listen(parent, activity => observed = activity, AgentKitActivityNames.ToolCatalogInvokerAcquire);
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measured = [];
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Add((name, value, tags)), AgentKitActivityNames.ToolCatalogInvokerAcquire);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled") { cancellation.Cancel(); }
        IToolInvokerLease? lease = null;
        try
        {
            if (outcome == "cancelled")
            {
                (await Should.ThrowAsync<OperationCanceledException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
            }
            else if (outcome == "failed")
            {
                _ = await Should.ThrowAsync<InvalidOperationException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken));
            }
            else
            {
                var result = await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken);
                lease = (result as ToolInvokerAcquired)?.Lease;
                (lease is not null).ShouldBe(outcome == "acquired");
                if (result is ToolInvokerUnavailable unavailable) { unavailable.SafeReason.ShouldNotContain(secret); }
            }
            var activity = observed.ShouldNotBeNull();
            activity.ParentId.ShouldBe(parent.Id);
            activity.Status.ShouldBe(outcome == "acquired" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
            activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(snapshot.Identity.TenantId.Value);
            activity.GetTagItem(AgentKitTagNames.PrincipalId).ShouldBe(snapshot.Identity.PrincipalId.Value);
            activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(snapshot.AgentId.ToString());
            activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(snapshot.SessionId.ToString());
            activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(snapshot.RunId.ToString());
            activity.GetTagItem(AgentKitTagNames.ToolCatalogVersion).ShouldBe(snapshot.Version.Value);
            activity.GetTagItem(AgentKitTagNames.ToolId).ShouldBe(tool.Id.Value);
            activity.GetTagItem(AgentKitTagNames.ToolVersion).ShouldBe(tool.Version.Value);
            if (outcome != "acquired") { activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(outcome == "failed" ? typeof(InvalidOperationException).FullName : outcome); }
            var logs = logger.Snapshot();
            logs.Select(static entry => entry.EventId.Id).ShouldBe([4040, 4041]);
            logs[^1].Level.ShouldBe(level);
            logs.ShouldAllBe(entry => entry.Category == typeof(ToolCatalogCapture).FullName);
            foreach (var log in logs)
            {
                log.State["TenantId"].ShouldBe(snapshot.Identity.TenantId);
                log.State["PrincipalId"].ShouldBe(snapshot.Identity.PrincipalId);
                log.State["AgentId"].ShouldBe(snapshot.AgentId);
                log.State["SessionId"].ShouldBe(snapshot.SessionId);
                log.State["RunId"].ShouldBe(snapshot.RunId);
                log.State["CatalogVersion"].ShouldBe(snapshot.Version);
            }
            AssertSafe(logs, [activity], secret);
            measured.Count.ShouldBe(2);
            measured.Single(item => item.Name == AgentKitMetricNames.ToolCatalogCaptureOperationCount).Value.ShouldBe(1);
            measured.Single(item => item.Name == AgentKitMetricNames.ToolCatalogCaptureOperationDuration).Value.ShouldBe(0.125);
            foreach (var (Name, Value, Tags) in measured) { AssertMetricTags(Tags, AgentKitActivityNames.ToolCatalogInvokerAcquire, outcome); }
        }
        finally
        {
            if (lease is not null) { await lease.DisposeAsync(); }
            await capture.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_WhenObserved_ReportsEachStageAndContainsCleanupContent(bool fails)
    {
        const string secret = "source-cleanup-secret";
        var tool = ToolCaptureTestData.Descriptor(description: secret);
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var failure = new InvalidOperationException(secret);
        var source = new CallbackToolProviderCapture(publication)
        {
            Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker()))),
            Release = () => fails ? ValueTask.FromException(failure) : ValueTask.CompletedTask,
        };
        var logger = new RecordingLogger<ToolCatalogCapture>();
        var capture = new ToolCatalogCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source), TimeProvider.System, logger);
        using var parent = new Activity("catalog-closure-observation").Start();
        var observed = new ConcurrentQueue<Activity>();
        using var activities = Listen(parent, observed.Enqueue);
        var measured = new ConcurrentQueue<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var metrics = ListenMetrics(parent, (name, value, tags) => measured.Enqueue((name, value, tags)));
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var close = capture.DisposeAsync().AsTask();
        if (fails)
        {
            (await Should.ThrowAsync<InvalidOperationException>(async () => await lease.DisposeAsync())).ShouldBeSameAs(failure);
            (await Should.ThrowAsync<InvalidOperationException>(async () => await close)).ShouldBeSameAs(failure);
        }
        else { await lease.DisposeAsync(); await close; }

        var spans = observed.ToArray();
        spans.Length.ShouldBe(4);
        var release = spans.Single(activity => activity.OperationName == AgentKitActivityNames.ToolCatalogInvokerRelease);
        foreach (var activity in spans)
        {
            activity.ParentId.ShouldBe(activity.OperationName == AgentKitActivityNames.ToolCatalogCaptureDisposeSources ? release.Id : parent.Id);
            var failed = fails && activity.OperationName != AgentKitActivityNames.ToolCatalogInvokerAcquire;
            activity.Status.ShouldBe(failed ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            if (failed)
            {
                activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("failed");
                activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(typeof(InvalidOperationException).FullName);
            }
            var pair = measured.Where(item => (string?) item.Tags[0].Value == activity.OperationName).ToArray();
            pair.Length.ShouldBe(2);
            foreach (var (Name, Value, Tags) in pair) { AssertMetricTags(Tags, activity.OperationName, (string) activity.GetTagItem(AgentKitTagNames.Outcome)!); }
        }
        var logs = logger.Snapshot();
        logs.Count(entry => entry.EventId.Id == 4040).ShouldBe(4);
        logs.Count(entry => entry.EventId.Id == 4041).ShouldBe(4);
        logs.Count(entry => entry.Level == LogLevel.Error).ShouldBe(fails ? 3 : 0);
        AssertSafe(logs, spans, secret);
        source.Disposals.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Theory]
    [InlineData("logger")]
    [InlineData("sample")]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("metric")]
    public async Task DisposeAsync_WhenObserversThrow_PreservesOwnershipAndParent(string stage)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker());
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner)) };
        var logger = new RecordingLogger<ToolCatalogCapture> { ThrowOnWrite = stage == "logger" };
        var capture = new ToolCatalogCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(publication, source), TimeProvider.System, logger);
        using var parent = new Activity("catalog-throwing-observer").Start();
        var callbacks = 0;
        using var activities = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => stage == "sample" && IsCaptureOperation(options.Name) && options.Parent.TraceId == parent.TraceId
                ? throw new InvalidOperationException("observer") : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Fail("start", activity),
            ActivityStopped = activity => Fail("stop", activity),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = ListenMetrics(parent, (_, _, _) =>
        {
            if (stage == "metric") { _ = Interlocked.Increment(ref callbacks); throw new InvalidOperationException("observer"); }
        });
        var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var close = capture.DisposeAsync().AsTask();
        await lease.DisposeAsync();
        await close;

        inner.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(parent);
        if (stage == "metric") { callbacks.ShouldBe(8); }
        return;
        void Fail(string callback, Activity activity)
        {
            if (stage == callback && activity.TraceId == parent.TraceId && IsCaptureOperation(activity.OperationName)) { throw new InvalidOperationException("observer"); }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AcquireInvokerAsync_WhenClockFailsOrReverses_OmitsUnknownDuration(int failure)
    {
        var reads = 0;
        var clock = new CallbackTimestampTimeProvider(() => { reads++; return reads == failure ? throw new InvalidOperationException("clock") : failure == 3 ? -reads : reads; });
        await using var capture = new ToolCatalogCapture(ToolCaptureTestData.Catalog([], []), [], clock, NullLogger<ToolCatalogCapture>.Instance);
        using var parent = new Activity("catalog-clock-failure").Start();
        using var activities = Listen(parent, _ => { });
        List<string> names = [];
        using var metrics = ListenMetrics(parent, (name, _, _) => names.Add(name), AgentKitActivityNames.ToolCatalogInvokerAcquire);

        _ = (await capture.AcquireInvokerAsync(new(new ToolId("missing"), new ToolVersion("1")), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();

        names.ShouldBe([AgentKitMetricNames.ToolCatalogCaptureOperationCount]);
        reads.ShouldBe(failure == 1 ? 1 : 2);
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenIdentityInvalid_RejectsBeforeDiagnostics()
    {
        var logger = new RecordingLogger<ToolCatalogCapture>();
        var reads = 0;
        var capture = new ToolCatalogCapture(ToolCaptureTestData.Catalog([], []), [], new CallbackTimestampTimeProvider(() => ++reads), logger);
        using var parent = new Activity("invalid-catalog-identity").Start();
        var count = 0;
        using var activities = Listen(parent, _ => count++);

        var error = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await capture.AcquireInvokerAsync(default, TestContext.Current.CancellationToken));

        error.ParamName.ShouldBe("identity");
        error.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        reads.ShouldBe(0);
        count.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
        await capture.DisposeAsync();
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

    private static bool IsCaptureOperation(string name) => name is AgentKitActivityNames.ToolCatalogInvokerAcquire
        or AgentKitActivityNames.ToolCatalogInvokerRelease or AgentKitActivityNames.ToolCatalogCaptureClose or AgentKitActivityNames.ToolCatalogCaptureDisposeSources;

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
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.ToolCatalogCaptureOperationCount or AgentKitMetricNames.ToolCatalogCaptureOperationDuration)
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
    private static ImmutableDictionary<ToolSourceId, IToolProviderCapture> Sources(ToolProviderSnapshot publication, IToolProviderCapture source) =>
        ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(publication.SourceId, source);

    private static void AssertExact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
