// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines portable ownership, fencing, renewal, expiry, and release behavior for <see cref="IDurableLeaseManager"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>
/// Every case here is required contract behavior for all lease-manager adapters. Mutual exclusion is asserted only
/// within the scope the adapter claims: the suite never assumes cross-process coordination, because a process-local
/// manager legitimately provides none.
/// </remarks>
public abstract class DurableLeaseManagerConformanceTests<TFixture>
    where TFixture : IDurableLeaseManagerConformanceFixture, new()
{
    /// <summary>Verifies a free address grants ownership with a real generation and the requesting worker.</summary>
    [Fact]
    public async Task AcquireAsync_WhenNoLeaseExists_GrantsOwnershipToTheRequestingWorker()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken);

        var lease = result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        await using var owned = lease;
        owned.OwnerWorkerId.ShouldBe(DurabilityConformanceData.Worker);
        owned.Address.ShouldBe(DurabilityConformanceData.Address());
        owned.FencingToken.ShouldNotBe(default);
        owned.LeaseId.ShouldNotBe(default);
    }

    /// <summary>Verifies a held address excludes another worker and discloses the current owner.</summary>
    [Fact]
    public async Task AcquireAsync_WhenAnotherWorkerHoldsAnUnexpiredLease_ReportsTheCurrentOwner()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        await using var held = await AcquireAsync(manager, DurabilityConformanceData.Worker);

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);

        var busy = result.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>();
        busy.CurrentOwnerWorkerId.ShouldBe(DurabilityConformanceData.Worker);
        busy.CurrentToken.ShouldBe(held.FencingToken);
        busy.ExpiresAt.ShouldBe(held.ExpiresAt);
    }

    /// <summary>Verifies an owner cannot silently reacquire its own lease instead of renewing it.</summary>
    [Fact]
    public async Task AcquireAsync_WhenTheSameWorkerAlreadyHoldsTheLease_StillReportsTheLeaseAsHeld()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        await using var held = await AcquireAsync(manager, DurabilityConformanceData.Worker);

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>().CurrentToken.ShouldBe(held.FencingToken);
    }

    /// <summary>Verifies an expired lease is taken over under a strictly newer generation.</summary>
    [Fact]
    public async Task AcquireAsync_WhenThePriorLeaseExpired_GrantsAStrictlyNewerGeneration()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var first = await AcquireAsync(manager, DurabilityConformanceData.Worker);

        fixture.Advance(TimeSpan.FromMinutes(5));
        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);

        await using var second = result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        second.FencingToken.Value.ShouldBeGreaterThan(first.FencingToken.Value);
        second.OwnerWorkerId.ShouldBe(DurabilityConformanceData.OtherWorker);
    }

    /// <summary>Verifies renewal extends expiry without allocating a new ownership generation.</summary>
    [Fact]
    public async Task RenewAsync_WhileStillCurrent_ExtendsExpiryWithoutChangingTheToken()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        await using var lease = await AcquireAsync(manager, DurabilityConformanceData.Worker);
        var originalExpiry = lease.ExpiresAt;
        var originalToken = lease.FencingToken;

        fixture.Advance(TimeSpan.FromSeconds(10));
        var result = await lease.RenewAsync(TestContext.Current.CancellationToken);

        var renewed = result.ShouldBeOfType<LeaseRenewed>();
        renewed.ExpiresAt.ShouldBeGreaterThan(originalExpiry);
        lease.FencingToken.ShouldBe(originalToken);
        lease.ExpiresAt.ShouldBe(renewed.ExpiresAt);
    }

    /// <summary>Verifies a superseded owner learns it lost ownership instead of extending a dead lease.</summary>
    [Fact]
    public async Task RenewAsync_AfterAnotherWorkerTookOver_ReportsTheLeaseLost()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var first = await AcquireAsync(manager, DurabilityConformanceData.Worker);
        fixture.Advance(TimeSpan.FromMinutes(5));
        await using var second = await AcquireAsync(manager, DurabilityConformanceData.OtherWorker);

        var result = await first.RenewAsync(TestContext.Current.CancellationToken);

        result.ShouldBeOfType<LeaseLost>().CurrentToken.ShouldBe(second.FencingToken);
        await first.DisposeAsync();
    }

    /// <summary>Verifies releasing a current lease hands the address over without waiting for expiry.</summary>
    [Fact]
    public async Task DisposeAsync_WhenTheLeaseIsStillCurrent_ReleasesTheAddressImmediately()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var lease = await AcquireAsync(manager, DurabilityConformanceData.Worker);

        await lease.DisposeAsync();

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);
        await using var second = result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        second.FencingToken.Value.ShouldBeGreaterThan(lease.FencingToken.Value);
    }

    /// <summary>Verifies a superseded lease releases nothing, so it cannot revoke the current owner.</summary>
    [Fact]
    public async Task DisposeAsync_WhenTheLeaseAlreadyLostOwnership_DoesNotReleaseTheCurrentOwner()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var first = await AcquireAsync(manager, DurabilityConformanceData.Worker);
        fixture.Advance(TimeSpan.FromMinutes(5));
        await using var second = await AcquireAsync(manager, DurabilityConformanceData.OtherWorker);

        await first.DisposeAsync();

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>().CurrentToken.ShouldBe(second.FencingToken);
    }

    /// <summary>Verifies repeated release is a harmless no-op rather than a second release.</summary>
    [Fact]
    public async Task DisposeAsync_WhenRepeated_IsAHarmlessNoOp()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var lease = await AcquireAsync(manager, DurabilityConformanceData.Worker);

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);
        await using var second = result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        second.OwnerWorkerId.ShouldBe(DurabilityConformanceData.OtherWorker);
    }

    /// <summary>Verifies a released lease cannot be renewed back into ownership.</summary>
    [Fact]
    public async Task RenewAsync_AfterRelease_ThrowsObjectDisposedException()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var lease = await AcquireAsync(manager, DurabilityConformanceData.Worker);
        await lease.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            _ = await lease.RenewAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies ownership of one operation never excludes a different operation.</summary>
    [Fact]
    public async Task AcquireAsync_ForDifferentOperations_GrantsBothIndependently()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        await using var first = await AcquireAsync(manager, DurabilityConformanceData.Worker);

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(
                DurabilityConformanceData.OtherWorker, DurabilityConformanceData.OtherOperation),
            TestContext.Current.CancellationToken);

        await using var second = result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        second.Address.ShouldBe(DurabilityConformanceData.Address(DurabilityConformanceData.OtherOperation));
    }

    /// <summary>Verifies ownership generations never repeat or decrease across successive takeovers.</summary>
    [Fact]
    public async Task AcquireAsync_AcrossSuccessiveTakeovers_AllocatesStrictlyIncreasingTokens()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        var tokens = new List<long>();

        for (var generation = 0; generation < 3; generation++)
        {
            var lease = await AcquireAsync(manager, DurabilityConformanceData.Worker);
            tokens.Add(lease.FencingToken.Value);
            await lease.DisposeAsync();
        }

        tokens.ShouldBe([.. tokens.Order()]);
        tokens.Distinct().Count().ShouldBe(tokens.Count);
    }

    /// <summary>Verifies acquisition cancelled before it completes grants no ownership.</summary>
    [Fact]
    public async Task AcquireAsync_WhenCancelled_GrantsNoOwnership()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await manager.AcquireAsync(DurabilityConformanceData.LeaseRequest(), cancellation.Token));

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);
        await using var lease = result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        lease.OwnerWorkerId.ShouldBe(DurabilityConformanceData.OtherWorker);
    }

    /// <summary>Verifies renewal cancelled before it completes neither extends nor loses the lease.</summary>
    [Fact]
    public async Task RenewAsync_WhenCancelled_LeavesTheLeaseUnchanged()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();
        await using var lease = await AcquireAsync(manager, DurabilityConformanceData.Worker);
        var expiry = lease.ExpiresAt;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await lease.RenewAsync(cancellation.Token));

        lease.ExpiresAt.ShouldBe(expiry);
    }

    /// <summary>Verifies the request guard names its own parameter.</summary>
    [Fact]
    public async Task AcquireAsync_WhenTheRequestIsNull_RejectsTheExactArgument()
    {
        var fixture = new TFixture();
        var manager = fixture.CreateLeaseManager();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await manager.AcquireAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    private static async Task<IExecutionLease> AcquireAsync(IDurableLeaseManager manager, WorkerId workerId)
    {
        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(workerId), TestContext.Current.CancellationToken);
        return result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
    }
}
