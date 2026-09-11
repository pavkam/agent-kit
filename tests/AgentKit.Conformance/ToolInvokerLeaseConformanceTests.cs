// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines invoker retention, access closure, and shared release completion for replaceable lease implementations.</summary>
public abstract class ToolInvokerLeaseConformanceTests
{
    /// <summary>Creates an isolated lease with an observable owner release operation.</summary>
    /// <param name="tool">The exact descriptor to retain.</param><param name="sourceVersion">The exact source publication.</param><param name="invoker">The borrowed instance, never disposed directly by the lease.</param><param name="release">The callback representing release of the owning acquisition.</param>
    /// <returns>The owned lease exercised through its public interface.</returns>
    protected abstract IToolInvokerLease CreateLease(ToolDescriptor tool, ToolSourceVersion sourceVersion, IToolInvoker invoker, Func<ValueTask> release);

    /// <summary>Released invoker access fails while captured metadata remains readable.</summary>
    [Fact]
    public async Task Invoker_WhenLeaseIsReleased_ClosesAccessWhilePreservingImmutableMetadata()
    {
        var tool = ToolCaptureTestData.Descriptor();
        var invoker = new CaptureTestToolInvoker();
        var owner = new CaptureLifetimeProbe();
        var version = new ToolSourceVersion("source-1");
        var lease = CreateLease(tool, version, invoker, owner.DisposeAsync);
        lease.Invoker.ShouldBeSameAs(invoker);

        await lease.DisposeAsync();

        var exception = Should.Throw<ObjectDisposedException>(() => lease.Invoker);
        exception.GetType().ShouldBe(typeof(ObjectDisposedException));
        lease.Tool.ShouldBeSameAs(tool);
        lease.SourceVersion.ShouldBe(version);
        owner.Calls.ShouldBe(1);
        invoker.Disposals.ShouldBe(0);
    }

    /// <summary>Concurrent release callers share one pending completion and one owner callback.</summary>
    [Fact]
    public async Task DisposeAsync_WhenConcurrentCallersWait_ReleasesOnceAndSharesCompletion()
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new CaptureLifetimeProbe(() => new ValueTask(finish.Task));
        var lease = CreateLease(ToolCaptureTestData.Descriptor(), new("source-1"), new CaptureTestToolInvoker(), owner.DisposeAsync);

        var tasks = Enumerable.Range(0, 48).Select(_ => Task.Run(async () => await lease.DisposeAsync(), TestContext.Current.CancellationToken)).ToArray();
        var first = lease.DisposeAsync().AsTask();
        first.IsCompleted.ShouldBeFalse();
        _ = Should.Throw<ObjectDisposedException>(() => lease.Invoker);
        finish.SetResult();
        await Task.WhenAll(tasks);
        await first;
        await lease.DisposeAsync();

        owner.Calls.ShouldBe(1);
    }

    /// <summary>Synchronous and asynchronous cleanup failures reach every waiter without a retry.</summary>
    /// <param name="asynchronous">Whether the owner fails after asynchronous suspension.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_WhenReleaseFails_AllWaitersObserveOneFailure(bool asynchronous)
    {
        var failure = new InvalidOperationException("controlled release failure");
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new CaptureLifetimeProbe(() => asynchronous ? new ValueTask(finish.Task) : throw failure);
        var lease = CreateLease(ToolCaptureTestData.Descriptor(), new("source-1"), new CaptureTestToolInvoker(), owner.DisposeAsync);

        var first = lease.DisposeAsync().AsTask();
        var second = lease.DisposeAsync().AsTask();
        if (asynchronous)
        {
            first.IsCompleted.ShouldBeFalse();
            second.IsCompleted.ShouldBeFalse();
            finish.SetException(failure);
        }

        (await Should.ThrowAsync<InvalidOperationException>(async () => await first)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await second)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await lease.DisposeAsync())).ShouldBeSameAs(failure);
        owner.Calls.ShouldBe(1);
    }

}
