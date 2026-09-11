// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines exact binding, concurrent closure, cancellation, and ownership requirements for retained source captures.</summary>
public abstract class ToolProviderCaptureConformanceTests
{
    /// <summary>Creates an isolated capture that takes ownership of the supplied optional source lifetime.</summary>
    /// <param name="snapshot">The immutable publication.</param><param name="invokers">Its complete exact binding graph.</param><param name="lifetime">The optional owned cleanup resource; invokers remain borrowed.</param>
    /// <returns>The capture exercised through its public contract.</returns>
    protected abstract IToolProviderCapture CreateCapture(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers, IAsyncDisposable? lifetime);

    /// <summary>Verifies stable evidence and exact invoker references without invoking or disposing borrowed tools.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenExactBindingExists_RetainsDescriptorVersionAndBorrowedInstance()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var snapshot = ToolCaptureTestData.Snapshot([tool]);
        var capture = CreateCapture(snapshot, ToolCaptureTestData.Bindings(tool, invoker), null);

        var acquired = (await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>();
        await using (var lease = acquired.Lease)
        {
            lease.Tool.ShouldBeSameAs(tool);
            lease.SourceVersion.ShouldBe(snapshot.SourceVersion);
            lease.Invoker.ShouldBeSameAs(invoker);
            capture.Snapshot.ShouldBeSameAs(snapshot);
        }
        await capture.DisposeAsync();

        capture.Snapshot.ShouldBeSameAs(snapshot);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>Verifies no version, case, or textual fallback when a binding is absent.</summary>
    [Theory]
    [InlineData("tool.read", "2")]
    [InlineData("Tool.read", "1")]
    [InlineData("read", "1")]
    public async Task AcquireInvokerAsync_WhenIdentityDiffers_RetainsTheUnavailableRequest(string id, string version)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        await using var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), null);
        var requested = new ToolIdentity(new ToolId(id), new ToolVersion(version));

        var result = (await capture.AcquireInvokerAsync(requested, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();

        result.Identity.ShouldBe(requested);
        result.SafeReason.ShouldNotBeNullOrWhiteSpace();
        invoker.Invocations.ShouldBe(0);
    }

    /// <summary>Verifies a selected empty publication remains readable and acquires nothing.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenPublicationIsEmpty_DoesNotInventBindings()
    {
        var snapshot = ToolCaptureTestData.Snapshot([]);
        await using var capture = CreateCapture(snapshot, [], null);
        var requested = new ToolIdentity(new ToolId("missing"), new ToolVersion("1"));

        (await capture.AcquireInvokerAsync(requested, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(requested);
        capture.Snapshot.SourceVersion.ShouldBe(snapshot.SourceVersion);
    }

    /// <summary>Verifies argument guards through the public typed boundary before acquiring any lifetime.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenIdentityIsDefault_RejectsExactParameter()
    {
        await using var capture = CreateCapture(ToolCaptureTestData.Snapshot([]), [], null);

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await capture.AcquireInvokerAsync(default, TestContext.Current.CancellationToken));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("identity");
    }

    /// <summary>Verifies cancellation transfers no lease, including when the capture is closed or the identity is absent.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task AcquireInvokerAsync_WhenCallerIsCancelled_PropagatesExactTokenWithoutAcquiring(bool present, bool closed)
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        await using var capture = CreateCapture(ToolCaptureTestData.Snapshot(present ? [tool] : []), present ? ToolCaptureTestData.Bindings(tool, invoker) : [], owner);
        if (closed)
        {
            await capture.DisposeAsync();
        }
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await capture.AcquireInvokerAsync(new(tool.Id, tool.Version), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        await capture.DisposeAsync();
        owner.Calls.ShouldBe(1);
        invoker.Invocations.ShouldBe(0);
    }

    /// <summary>Verifies closure rejects new work while independent outstanding leases retain the original binding.</summary>
    [Fact]
    public async Task DisposeAsync_WhenLeasesRemain_DrainsThemBeforeOwnedCleanup()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), owner);
        var identity = new ToolIdentity(tool.Id, tool.Version);
        var first = (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var second = (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var closing = capture.DisposeAsync().AsTask();

        closing.IsCompleted.ShouldBeFalse();
        owner.Calls.ShouldBe(0);
        first.ShouldNotBeSameAs(second);
        first.Invoker.ShouldBeSameAs(invoker);
        second.Invoker.ShouldBeSameAs(invoker);
        (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(identity);
        await first.DisposeAsync();
        closing.IsCompleted.ShouldBeFalse();
        owner.Calls.ShouldBe(0);
        await second.DisposeAsync();
        await closing;
        await capture.DisposeAsync();
        owner.Calls.ShouldBe(1);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>Verifies a newer publication cannot redirect an already captured invoker.</summary>
    [Fact]
    public async Task AcquireInvokerAsync_WhenAnotherPublicationIsCaptured_PreservesBothExactBindings()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var older = new CaptureTestToolInvoker();
        var newer = new CaptureTestToolInvoker();
        await using var first = CreateCapture(ToolCaptureTestData.Snapshot([tool], "old"), ToolCaptureTestData.Bindings(tool, older), null);
        await using var second = CreateCapture(ToolCaptureTestData.Snapshot([tool], "new"), ToolCaptureTestData.Bindings(tool, newer), null);
        var identity = new ToolIdentity(tool.Id, tool.Version);

        await using var oldLease = (await first.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        await using var newLease = (await second.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        oldLease.Invoker.ShouldBeSameAs(older);
        oldLease.SourceVersion.ShouldBe(new ToolSourceVersion("old"));
        newLease.Invoker.ShouldBeSameAs(newer);
        newLease.SourceVersion.ShouldBe(new ToolSourceVersion("new"));
    }

    /// <summary>Verifies acquisition and closure race without leaking a lease or disposing a borrowed invoker.</summary>
    [Fact]
    public async Task DisposeAsync_WhenAcquisitionRacesClosure_ReleasesOwnedResourcesOnce()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), owner);
        var identity = new ToolIdentity(tool.Id, tool.Version);
        var held = (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = Enumerable.Range(0, 48).Select(_ => Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            var result = await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken);
            if (result is ToolInvokerAcquired acquired)
            {
                acquired.Lease.Invoker.ShouldBeSameAs(invoker);
                await acquired.Lease.DisposeAsync();
            }
            else
            {
                result.ShouldBeOfType<ToolInvokerUnavailable>().Identity.ShouldBe(identity);
            }
        }, TestContext.Current.CancellationToken)).ToArray();
        var closing = Task.Run(async () =>
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            await capture.DisposeAsync();
        }, TestContext.Current.CancellationToken);

        start.SetResult();
        await Task.WhenAll(readers);
        await held.DisposeAsync();
        await closing;

        owner.Calls.ShouldBe(1);
        invoker.Invocations.ShouldBe(0);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>Verifies owned cleanup failure is shared by the final lease and every closure waiter without retrying cleanup.</summary>
    [Fact]
    public async Task DisposeAsync_WhenOwnedCleanupFails_PropagatesTheSameFailureWithoutRetry()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var failure = new InvalidOperationException("controlled cleanup failure");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new CaptureLifetimeProbe(() => new ValueTask(finish.Task));
        var capture = CreateCapture(ToolCaptureTestData.Snapshot([tool]), ToolCaptureTestData.Bindings(tool, invoker), owner);
        var identity = new ToolIdentity(tool.Id, tool.Version);
        var lease = (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;

        var closing = capture.DisposeAsync().AsTask();
        var releasing = lease.DisposeAsync().AsTask();
        owner.Calls.ShouldBe(1);
        closing.IsCompleted.ShouldBeFalse();
        releasing.IsCompleted.ShouldBeFalse();
        finish.SetException(failure);

        (await Should.ThrowAsync<InvalidOperationException>(async () => await closing)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await releasing)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await lease.DisposeAsync())).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await capture.DisposeAsync())).ShouldBeSameAs(failure);
        _ = (await capture.AcquireInvokerAsync(identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerUnavailable>();
        owner.Calls.ShouldBe(1);
        invoker.Disposals.ShouldBe(0);
    }
}
