// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Exercises source identity, independent discovery acquisitions, cancellation, and retained bindings.</summary>
public abstract class ToolProviderConformanceTests
{
    /// <summary>Creates a source configured to publish the supplied deterministic data without external services.</summary>
    /// <param name="snapshot">The publication the fixture makes available for discovery.</param><param name="invokers">The borrowed invokers bound to that publication.</param>
    /// <returns>The provider under test, with any infrastructure owned by the concrete fixture.</returns>
    protected abstract IToolProvider CreateProvider(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers);

    /// <summary>Discovery preserves explicit source and tool versions and transfers an exact invoker binding.</summary>
    [Fact]
    public async Task DiscoverAsync_WhenPublicationAvailable_RetainsOrderedDescriptorsAndExactInvoker()
    {
        var first = ToolCaptureTestData.Descriptor(version: "v2");
        var second = ToolCaptureTestData.Descriptor("tool.second", "v1");
        var invoker = new CaptureTestToolInvoker();
        var snapshot = ToolCaptureTestData.Snapshot([second, first], "publication-v7");
        var provider = CreateProvider(snapshot, ToolCaptureTestData.Bindings(first, invoker).Add(new(second.Id, second.Version), new CaptureTestToolInvoker()));

        provider.SourceId.ShouldBe(snapshot.SourceId);
        await using var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        capture.Snapshot.ShouldBe(snapshot);
        capture.Snapshot.Tools.ShouldBe([second, first]);
        await using var lease = (await capture.AcquireInvokerAsync(new(first.Id, first.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        lease.Tool.ShouldBe(first);
        lease.SourceVersion.ShouldBe(snapshot.SourceVersion);
        lease.Invoker.ShouldBeSameAs(invoker);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>A valid empty publication still transfers an owned source-version capture.</summary>
    [Fact]
    public async Task DiscoverAsync_WhenSourceIsEmpty_PreservesItsExplicitVersion()
    {
        var snapshot = ToolCaptureTestData.Snapshot([], "empty-v3");
        var provider = CreateProvider(snapshot, []);

        await using var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);

        capture.Snapshot.ShouldBe(snapshot);
        capture.Snapshot.SourceId.ShouldBe(provider.SourceId);
        capture.Snapshot.Tools.ShouldBeEmpty();
    }

    /// <summary>Separate discoveries own separate close gates even when their immutable publication is shared.</summary>
    [Fact]
    public async Task DiscoverAsync_WhenCapturesOverlap_ClosingOnePreservesTheOthers()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var invoker = new CaptureTestToolInvoker();
        var provider = CreateProvider(snapshot, ToolCaptureTestData.Bindings(tool, invoker));
        var first = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        var second = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        first.ShouldNotBeSameAs(second);
        var lease = (await first.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var closing = first.DisposeAsync().AsTask();
        closing.IsCompleted.ShouldBeFalse();

        await using (var nextLease = (await second.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease)
        {
            nextLease.Invoker.ShouldBeSameAs(invoker);
            await lease.DisposeAsync();
            await closing;
            nextLease.Invoker.ShouldBeSameAs(invoker);
        }
        await second.DisposeAsync();
        await using var third = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        await using var finalLease = (await third.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        finalLease.Invoker.ShouldBeSameAs(invoker);
        provider.SourceId.ShouldBe(snapshot.SourceId);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>Pre-transfer cancellation preserves the caller token and does not poison the provider.</summary>
    [Fact]
    public async Task DiscoverAsync_WhenCancelled_PropagatesTokenAndAllowsLaterDiscovery()
    {
        var snapshot = ToolCaptureTestData.Snapshot([]);
        var provider = CreateProvider(snapshot, []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var error = await Should.ThrowAsync<OperationCanceledException>(async () => await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        await using var capture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        capture.Snapshot.ShouldBe(snapshot);
    }

    /// <summary>A null request is rejected before caller cancellation, without transferring a capture.</summary>
    [Fact]
    public async Task DiscoverAsync_WhenRequestIsNull_RejectsExactParameter()
    {
        var provider = CreateProvider(ToolCaptureTestData.Snapshot([]), []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var error = await Should.ThrowAsync<ArgumentNullException>(async () => await provider.DiscoverAsync(null!, cancellation.Token));

        error.GetType().ShouldBe(typeof(ArgumentNullException));
        error.ParamName.ShouldBe("request");
    }

    /// <summary>Concurrent discovery transfers independent captures with independently releasable leases.</summary>
    [Fact]
    public async Task DiscoverAsync_WhenRequestsAreConcurrent_PreservesIndependentOwnership()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var provider = CreateProvider(snapshot, ToolCaptureTestData.Bindings(tool, invoker));
        var captures = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
            await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        captures.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(captures.Length);

        await Task.WhenAll(captures.Select(async capture =>
        {
            var lease = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
            var closing = capture.DisposeAsync().AsTask();
            lease.Invoker.ShouldBeSameAs(invoker);
            await lease.DisposeAsync();
            await closing;
            await capture.DisposeAsync();
        }));

        invoker.Disposals.ShouldBe(0);
        invoker.Invocations.ShouldBe(0);
    }
}
