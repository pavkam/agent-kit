// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using System.Collections.Concurrent;

using AgentKit.TestSupport;

/// <summary>Exercises retained catalog selection, asynchronous acquisition, and source ownership through typed contracts.</summary>
public abstract class ToolCatalogCaptureConformanceTests
{
    /// <summary>Creates a fresh catalog capture owning the supplied source acquisitions after validation.</summary>
    /// <param name="snapshot">The exact catalog selection.</param><param name="sources">The corresponding source owners.</param>
    /// <returns>The independent owned catalog capture.</returns>
    protected abstract IToolCatalogCapture CreateCapture(ToolCatalogSnapshot snapshot, ImmutableDictionary<ToolSourceId, IToolProviderCapture> sources);

    /// <summary>Selection filters descriptors while retaining the original exact binding.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenSelectionFiltersSource_RetainsExactBindingAndRejectsUnselectedTools()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var excluded = ToolCaptureTestData.Descriptor("excluded");
        var publication = ToolCaptureTestData.Snapshot([tool, excluded]);
        var invoker = new CaptureTestToolInvoker();
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, invoker);
        var source = new CallbackToolProviderCapture(publication)
        {
            Acquire = (identity, _) =>
        {
            identity.ShouldBe(new ToolIdentity(tool.Id, tool.Version));
            return ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner));
        }
        };
        var snapshot = ToolCaptureTestData.Catalog([publication], [tool]);
        await using var capture = CreateCapture(snapshot, Sources(source));
        source.ReadSnapshot = () => throw new InvalidOperationException("Live metadata must not be reread.");

        var result = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>();
        await using (var lease = result.Lease)
        {
            lease.ShouldNotBeSameAs(inner);
            lease.Invoker.ShouldBeSameAs(invoker);
            lease.Tool.ShouldBe(tool);
            lease.SourceVersion.ShouldBe(publication.SourceVersion);
            capture.Snapshot.ShouldBeSameAs(snapshot);
            foreach (var identity in new ToolIdentity[] { new(excluded.Id, excluded.Version), new(new ToolId("TOOL.READ"), tool.Version), new(tool.Id, new ToolVersion("2")), new(new ToolId("read"), tool.Version) })
            {
                (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(identity);
            }
        }

        source.Acquisitions.ShouldBe(1);
        inner.Disposals.ShouldBe(1);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>Verifies the fixture's unconfigured default acquisition behavior reports unavailable rather than fabricating a lease.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenSourceHasNoConfiguredBehavior_ReturnsUnavailableFromTheDefaultCallback()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var source = new CallbackToolProviderCapture(publication);

        var result = await source.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolInvokerUnavailable>();
    }

    /// <summary>Published empty sources remain retained and owned.</summary>
    [Fact]
    public async Task DisposeAsync_WhenCatalogContainsEmptySources_ReleasesAllOwnersOnceAndKeepsEvidence()
    {
        var publication = ToolCaptureTestData.Snapshot([]);
        var source = new CallbackToolProviderCapture(publication);
        var snapshot = ToolCaptureTestData.Catalog([publication], []);
        var capture = CreateCapture(snapshot, Sources(source));

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () => await capture.DisposeAsync(), TestContext.Current.CancellationToken)));
        await capture.DisposeAsync();

        source.Disposals.ShouldBe(1);
        source.Acquisitions.ShouldBe(0);
        capture.Snapshot.ShouldBeSameAs(snapshot);
        capture.Snapshot.SourceVersions[publication.SourceId].ShouldBe(publication.SourceVersion);
    }

    /// <summary>Closure waits for leases and closes new acquisition before owned source cleanup.</summary>
    [Fact]
    public async Task DisposeAsync_WhenLeasesRemain_DrainsBeforeSourceCleanup()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var first = new CallbackToolInvokerLease(tool, publication.SourceVersion, invoker);
        var second = new CallbackToolInvokerLease(tool, publication.SourceVersion, invoker);
        var next = 0;
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(++next == 1 ? first : second)) };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(source));
        var a = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var b = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var close = capture.DisposeAsync().AsTask();
        close.IsCompleted.ShouldBeFalse();
        source.Disposals.ShouldBe(0);
        _ = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();
        await a.DisposeAsync();
        close.IsCompleted.ShouldBeFalse();
        b.Invoker.ShouldBeSameAs(invoker);
        await b.DisposeAsync();
        await close;
        await b.DisposeAsync();

        first.Disposals.ShouldBe(1);
        second.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(1);
        source.Acquisitions.ShouldBe(2);
        Should.Throw<ObjectDisposedException>(() => b.Invoker).GetType().ShouldBe(typeof(ObjectDisposedException));
        b.Tool.ShouldBe(tool);
    }

    /// <summary>Pending source acquisition cannot transfer a lease after catalog closure or cancellation.</summary>
    /// <param name="cancel">Whether cancellation wins in addition to closure.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquireInvokerAsync_WhenClosingDuringSourceAwait_ReleasesLateLeaseBeforeClosing(bool cancel)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker());
        var reply = new TaskCompletionSource<ToolInvokerLeaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        inner.Release = () => { released.SetResult(); return new ValueTask(finishRelease.Task); };
        using var cancellation = new CancellationTokenSource();
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, token) => { token.ShouldBe(cancellation.Token); return new ValueTask<ToolInvokerLeaseResult>(reply.Task); } };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(source));
        var acquiring = capture.AcquireInvokerAsync(new(tool.Id, tool.Version), cancellation.Token).AsTask();
        source.Acquisitions.ShouldBe(1);

        var close = capture.DisposeAsync().AsTask();
        if (cancel) { cancellation.Cancel(); }
        reply.SetResult(new ToolInvokerAcquired(inner));
        await released.Task.WaitAsync(TestContext.Current.CancellationToken);
        acquiring.IsCompleted.ShouldBeFalse();
        close.IsCompleted.ShouldBeFalse();
        source.Disposals.ShouldBe(0);
        finishRelease.SetResult();
        if (cancel)
        {
            (await Should.ThrowAsync<OperationCanceledException>(async () => await acquiring)).CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            (await acquiring).ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(new ToolIdentity(tool.Id, tool.Version));
        }
        await close;
        inner.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(1);
    }

    /// <summary>Source acquisition failure does not leave a retention preventing closure.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenSourceThrows_PropagatesFailureAndReleasesRetention()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var failure = new InvalidOperationException("controlled source failure");
        var reply = new TaskCompletionSource<ToolInvokerLeaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => new(reply.Task) };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(source));
        var acquiring = capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken).AsTask();
        var close = capture.DisposeAsync().AsTask();

        reply.SetException(failure);

        (await Should.ThrowAsync<InvalidOperationException>(async () => await acquiring)).ShouldBeSameAs(failure);
        await close;
        source.Disposals.ShouldBe(1);
    }

    /// <summary>Cancellation and invalid identity are rejected before contacting a source.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenCancelledOrIdentityDefault_RejectsBeforeSourceAcquisition()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var source = new CallbackToolProviderCapture(publication);
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(source));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var invalid = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await capture.AcquireInvokerAsync(default, cancellation.Token));
        invalid.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        invalid.ParamName.ShouldBe("identity");
        foreach (var identity in new ToolIdentity[] { new(tool.Id, tool.Version), new(new ToolId("missing"), tool.Version) })
        {
            (await Should.ThrowAsync<OperationCanceledException>(async () => await capture.AcquireInvokerAsync(identity, cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
        }
        await capture.DisposeAsync();
        (await Should.ThrowAsync<OperationCanceledException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
        source.Acquisitions.ShouldBe(0);
    }


    /// <summary>Concurrent acquisitions and closure never double-release a source lease or close its source early.</summary>
    [Fact]
    public async Task DisposeAsync_WhenAcquisitionsRaceClosure_ReleasesEachOwnedLeaseOnce()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var innerLeases = new ConcurrentBag<CallbackToolInvokerLease>();
        var source = new CallbackToolProviderCapture(publication)
        {
            Acquire = (_, _) =>
            {
                var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, invoker);
                innerLeases.Add(inner);
                return ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(inner));
            },
            Release = () => { innerLeases.ShouldAllBe(lease => lease.Disposals == 1); return ValueTask.CompletedTask; },
        };
        var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(source));
        var keeper = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            var result = await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken);
            if (result is ToolInvokerAcquired acquired)
            {
                acquired.Lease.Invoker.ShouldBeSameAs(invoker);
                await acquired.Lease.DisposeAsync();
                await acquired.Lease.DisposeAsync();
            }
            else { result.ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(new ToolIdentity(tool.Id, tool.Version)); }
        }, TestContext.Current.CancellationToken)).ToArray();
        var closing = Task.Run(async () => { await start.Task; await capture.DisposeAsync(); }, TestContext.Current.CancellationToken);

        start.SetResult();
        await Task.WhenAll(calls);
        source.Disposals.ShouldBe(0);
        await keeper.DisposeAsync();
        await closing;

        source.Disposals.ShouldBe(1);
        innerLeases.Count.ShouldBe(source.Acquisitions);
        innerLeases.ShouldAllBe(lease => lease.Disposals == 1);
    }

    /// <summary>Cancellation of one pending acquisition does not close the catalog or affect a later acquisition.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenCancelledDuringSourceAwait_ReleasesLateLeaseAndKeepsCatalogUsable()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var inner = new CallbackToolInvokerLease(tool, publication.SourceVersion, new CaptureTestToolInvoker());
        var reply = new TaskCompletionSource<ToolInvokerLeaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new CallbackToolProviderCapture(publication) { Acquire = (_, _) => new(reply.Task) };
        await using var capture = CreateCapture(ToolCaptureTestData.Catalog([publication], [tool]), Sources(source));
        using var cancellation = new CancellationTokenSource();
        var acquiring = capture.AcquireInvokerAsync(new(tool.Id, tool.Version), cancellation.Token).AsTask();

        cancellation.Cancel();
        reply.SetResult(new ToolInvokerAcquired(inner));
        (await Should.ThrowAsync<OperationCanceledException>(async () => await acquiring)).CancellationToken.ShouldBe(cancellation.Token);
        inner.Disposals.ShouldBe(1);
        source.Disposals.ShouldBe(0);
        source.Acquire = (identity, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerUnavailable(identity, "temporarily unavailable"));
        _ = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();
        source.Acquisitions.ShouldBe(2);
    }

    private static ImmutableDictionary<ToolSourceId, IToolProviderCapture> Sources(CallbackToolProviderCapture source) =>
        ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(source.Snapshot.SourceId, source);
}
