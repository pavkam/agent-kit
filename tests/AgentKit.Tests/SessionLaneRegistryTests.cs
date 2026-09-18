// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Verifies the engine's process-local per-session lane gate.</summary>
public sealed class SessionLaneRegistryTests
{
    private static readonly AgentId _agent = new(Guid.NewGuid());
    private static readonly SessionId _session = new(Guid.NewGuid());

    [Fact]
    public async Task EnterAsync_WhenTheLaneIsFree_AcquiresImmediatelyAndReleasesOnDispose()
    {
        var registry = new SessionLaneRegistry();

        var first = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);
        first.Dispose();
        using var second = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);

        _ = second.ShouldNotBeNull();
    }

    [Fact]
    public async Task EnterAsync_WhenTheLaneIsHeldAndBehaviorIsReject_ThrowsNamingTheActiveRun()
    {
        var registry = new SessionLaneRegistry();
        var activeRun = new RunId(Guid.NewGuid());
        using var held = await registry.EnterAsync(_agent, _session, activeRun, SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<AgentSessionBusyException>(async () =>
            await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken));

        exception.ActiveRunId.ShouldBe(activeRun);
        exception.SessionId.ShouldBe(_session);
    }

    [Fact]
    public async Task EnterAsync_WhenTheLaneIsHeldAndBehaviorIsWait_WaitsUntilReleased()
    {
        var registry = new SessionLaneRegistry();
        var held = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Wait, TestContext.Current.CancellationToken);

        var pending = registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Wait, TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        pending.IsCompleted.ShouldBeFalse();
        held.Dispose();

        using var acquired = await pending;
        _ = acquired.ShouldNotBeNull();
    }

    [Fact]
    public async Task EnterAsync_WhenAWaitIsCancelled_ThrowsAndLeavesTheLaneForTheHolder()
    {
        var registry = new SessionLaneRegistry();
        using var held = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Wait, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        var pending = registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Wait, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
        _ = await Should.ThrowAsync<AgentSessionBusyException>(async () =>
            await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnterAsync_WhenSessionsDiffer_DoesNotContend()
    {
        var registry = new SessionLaneRegistry();
        using var first = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);

        using var second = await registry.EnterAsync(_agent, new SessionId(Guid.NewGuid()), new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);
        using var third = await registry.EnterAsync(new AgentId(Guid.NewGuid()), _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);

        _ = second.ShouldNotBeNull();
        _ = third.ShouldNotBeNull();
    }

    [Fact]
    public async Task Dispose_WhenCalledTwice_ReleasesOnce()
    {
        var registry = new SessionLaneRegistry();
        var lease = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);

        lease.Dispose();
        lease.Dispose();

        using var again = await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<AgentSessionBusyException>(async () =>
            await registry.EnterAsync(_agent, _session, new RunId(Guid.NewGuid()), SessionBusyBehavior.Reject, TestContext.Current.CancellationToken));
    }
}
