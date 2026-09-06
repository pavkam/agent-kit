// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using Microsoft.Extensions.Options;

public sealed class DefaultSessionRunCoordinatorTests
{
    private static DefaultSessionRunCoordinator CreateCoordinator(AgentSessionOptions? options = null) => new(
        new GuidIdentifierGenerator<SessionLeaseId>(static v => new SessionLeaseId(v)),
        Options.Create(options ?? new AgentSessionOptions()));

    private static SessionRunLeaseRequest Request(SessionAddress address, RunId? runId = null) =>
        new(address.AgentId, address.SessionId, runId ?? new RunId(Guid.NewGuid()));

    [Fact]
    public void Constructor_WhenLeaseIdsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultSessionRunCoordinator(null!, Options.Create(new AgentSessionOptions())));

        exception.ParamName.ShouldBe("leaseIds");
    }

    [Fact]
    public async Task AcquireAsync_WhenSessionIsFree_ReturnsAcquiredLease()
    {
        var coordinator = CreateCoordinator();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var runId = new RunId(Guid.NewGuid());

        var result = await coordinator.AcquireAsync(Request(address, runId), TestContext.Current.CancellationToken);

        var acquired = result.ShouldBeOfType<SessionRunLeaseAcquired>();
        acquired.Lease.RunId.ShouldBe(runId);
        acquired.Lease.AgentId.ShouldBe(address.AgentId);
        acquired.Lease.SessionId.ShouldBe(address.SessionId);
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenAlreadyHeldAndBehaviorIsReject_ReturnsBusyImmediately()
    {
        var coordinator = CreateCoordinator();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var firstRun = new RunId(Guid.NewGuid());
        var acquired = (SessionRunLeaseAcquired) await coordinator.AcquireAsync(
            Request(address, firstRun), TestContext.Current.CancellationToken);

        var second = await coordinator.AcquireAsync(
            Request(address, new RunId(Guid.NewGuid())), TestContext.Current.CancellationToken);

        var busy = second.ShouldBeOfType<SessionRunBusy>();
        busy.ActiveRunId.ShouldBe(firstRun);

        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_AfterLeaseDisposed_CanBeAcquiredAgain()
    {
        var coordinator = CreateCoordinator();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var acquired = (SessionRunLeaseAcquired) await coordinator.AcquireAsync(
            Request(address), TestContext.Current.CancellationToken);

        await acquired.Lease.DisposeAsync();
        var second = await coordinator.AcquireAsync(Request(address), TestContext.Current.CancellationToken);

        _ = second.ShouldBeOfType<SessionRunLeaseAcquired>();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_DoesNotReleaseTwice()
    {
        var coordinator = CreateCoordinator();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var acquired = (SessionRunLeaseAcquired) await coordinator.AcquireAsync(
            Request(address), TestContext.Current.CancellationToken);

        await acquired.Lease.DisposeAsync();
        await acquired.Lease.DisposeAsync();

        // A single extra acquire should succeed (proving the semaphore was
        // only released once, not permanently over-released).
        var reacquired = (SessionRunLeaseAcquired) await coordinator.AcquireAsync(
            Request(address), TestContext.Current.CancellationToken);
        var busyCheck = await coordinator.AcquireAsync(Request(address), TestContext.Current.CancellationToken);

        _ = busyCheck.ShouldBeOfType<SessionRunBusy>();
        await reacquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_ForDifferentSessions_BothSucceedConcurrently()
    {
        var coordinator = CreateCoordinator();
        var addressA = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var addressB = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));

        var resultA = await coordinator.AcquireAsync(Request(addressA), TestContext.Current.CancellationToken);
        var resultB = await coordinator.AcquireAsync(Request(addressB), TestContext.Current.CancellationToken);

        _ = resultA.ShouldBeOfType<SessionRunLeaseAcquired>();
        _ = resultB.ShouldBeOfType<SessionRunLeaseAcquired>();
    }

    [Fact]
    public async Task AcquireAsync_WhenBehaviorIsWaitAndReleasedBeforeTimeout_AcquiresAfterRelease()
    {
        var coordinator = CreateCoordinator(new AgentSessionOptions
        {
            BusyBehavior = SessionBusyBehavior.Wait,
            BusyWaitTimeout = TimeSpan.FromSeconds(5),
        });
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var acquired = (SessionRunLeaseAcquired) await coordinator.AcquireAsync(
            Request(address), TestContext.Current.CancellationToken);

        var waitingTask = coordinator.AcquireAsync(Request(address), TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        await acquired.Lease.DisposeAsync();

        var result = await waitingTask;
        _ = result.ShouldBeOfType<SessionRunLeaseAcquired>();
    }

    [Fact]
    public async Task AcquireAsync_WhenBehaviorIsWaitAndTimeoutExpires_ReturnsBusy()
    {
        var coordinator = CreateCoordinator(new AgentSessionOptions
        {
            BusyBehavior = SessionBusyBehavior.Wait,
            BusyWaitTimeout = TimeSpan.FromMilliseconds(50),
        });
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var acquired = (SessionRunLeaseAcquired) await coordinator.AcquireAsync(
            Request(address), TestContext.Current.CancellationToken);

        var result = await coordinator.AcquireAsync(Request(address), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunBusy>();
        await acquired.Lease.DisposeAsync();
    }
}
