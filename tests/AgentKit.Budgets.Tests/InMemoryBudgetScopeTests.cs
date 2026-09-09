// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class InMemoryBudgetScopeTests
{
    [Fact]
    public void ThrowIfNullLock_WhenLockIsNull_ThrowsWithInferredParameterName()
    {
        Lock? hierarchyGate = null;

        var exception = Should.Throw<ArgumentNullException>(
            () => ArgumentNullException.ThrowIfNullLock(hierarchyGate));

        exception.ParamName.ShouldBe("hierarchyGate");
    }

    [Fact]
    public void Constructor_WhenHierarchyGateIsNull_ThrowsBeforeCreatingState()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemoryBudgetScope(
            new BudgetScopeId(Guid.NewGuid()),
            TestFactory.Address(),
            null,
            [],
            null!,
            new GuidIdentifierGenerator<BudgetReservationId>(static value => new BudgetReservationId(value)),
            TimeProvider.System,
            TestFactory.DefaultOptions()));

        exception.ParamName.ShouldBe("hierarchyGate");
    }

    [Fact]
    public async Task ReserveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => scope.ReserveAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ReserveAsync_WhenScopeIdDoesNotMatch_ThrowsArgumentException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var request = TestFactory.ReservationRequest(new BudgetScopeId(Guid.NewGuid()), TestFactory.TestDimension);

        _ = await Should.ThrowAsync<ArgumentException>(() => scope.ReserveAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task ReserveAsync_WhenUnitDoesNotMatchConfiguredLimit_ThrowsArgumentException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10, TestFactory.Count)]);

        var request = TestFactory.ReservationRequest(
            scope.Id, TestFactory.TestDimension, unit: new BudgetUnit("bytes"));

        _ = await Should.ThrowAsync<ArgumentException>(() => scope.ReserveAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task ReserveAsync_WhenNoLimitConfigured_Succeeds()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);

        var result = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1_000_000m), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task ReserveAsync_WhenWithinHardLimit_Succeeds()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var result = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task ReserveAsync_WhenExceedingHardLimit_ReturnsRejected()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var result = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 11m), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<BudgetRejected>();
        rejected.Failure.Dimension.ShouldBe(TestFactory.TestDimension);
        rejected.Failure.Kind.ShouldBe(BudgetLimitKind.Hard);
        rejected.Failure.ConfiguredValue.ShouldBe(10m);
        rejected.Failure.RequestedAmount.ShouldBe(Quantity(11m));
    }

    [Fact]
    public async Task ReserveAsync_WhenExceedingSoftLimit_StillSucceeds()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.SoftLimit(TestFactory.TestDimension, 10)]);

        var result = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 100m), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task ReserveAsync_WhenSecondReservationWouldExceedRemainingCapacity_IsRejected()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var first = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 6m), TestContext.Current.CancellationToken);
        var second = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = first.ShouldBeOfType<BudgetReserved>();
        _ = second.ShouldBeOfType<BudgetRejected>();
    }

    [Fact]
    public async Task ReserveAsync_WhenSameIdempotencyKeyReusedForOpenReservation_ReturnsSameReservation()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var request = TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m, idempotencyKey: "key-1");

        var first = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);
        var second = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);

        var firstReservation = first.ShouldBeOfType<BudgetReserved>().Reservation;
        var secondReservation = second.ShouldBeOfType<BudgetReserved>().Reservation;
        secondReservation.Id.ShouldBe(firstReservation.Id);
    }

    [Fact]
    public async Task ReserveAsync_WhenSameIdempotencyKeyReusedWithDifferentAmount_ThrowsInvalidOperationException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);

        _ = await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m, idempotencyKey: "key-1"), TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 9m, idempotencyKey: "key-1"), TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task ReserveAsync_WhenIdempotencyKeyReusedAfterOriginalDisposed_ReturnsOriginalReceipt()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var request = TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m, idempotencyKey: "key-1");

        var first = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);
        await first.ShouldBeOfType<BudgetReserved>().Reservation.DisposeAsync();

        var second = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);

        var secondReservation = second.ShouldBeOfType<BudgetReserved>().Reservation;
        secondReservation.Id.ShouldBe(((BudgetReserved) first).Reservation.Id);
    }

    [Fact]
    public async Task DisposeAsync_WhenReservationNeverCommitted_ReleasesCapacityBackToScope()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var first = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m), TestContext.Current.CancellationToken);
        await ((BudgetReserved) first).Reservation.DisposeAsync();

        var second = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m), TestContext.Current.CancellationToken);

        _ = second.ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_IsIdempotent()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        await reserved.Reservation.DisposeAsync();
        await reserved.Reservation.DisposeAsync();

        var snapshot = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension).Reserved.ShouldBe(Quantity(0m));
    }

    [Fact]
    public async Task DisposeAsync_AfterCommit_DoesNotDoubleRelease()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reserved.Reservation.CommitAsync(5m, TestContext.Current.CancellationToken);
        await reserved.Reservation.DisposeAsync();

        var snapshot = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var usage = snapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension);
        usage.Committed.ShouldBe(Quantity(5m));
        usage.Reserved.ShouldBe(Quantity(0m));
    }

    [Fact]
    public async Task CommitAsync_WhenActualIsLessThanReserved_ReleasesRemainder()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        var result = await reserved.Reservation.CommitAsync(4m, TestContext.Current.CancellationToken);

        result.Reserved.ShouldBe(10m);
        result.Actual.ShouldBe(4m);
        result.Released.ShouldBe(6m);
        result.Overrun.ShouldBe(0m);
    }

    [Fact]
    public async Task CommitAsync_WhenActualExceedsReserved_RecordsOverrun()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 4m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        var result = await reserved.Reservation.CommitAsync(10m, TestContext.Current.CancellationToken);

        result.Released.ShouldBe(0m);
        result.Overrun.ShouldBe(6m);
    }

    [Fact]
    public async Task CommitAsync_WhenOverrunsAHardLimit_BlocksFurtherReservationsForThatDimension()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reserved.Reservation.CommitAsync(20m, TestContext.Current.CancellationToken);

        var next = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m), TestContext.Current.CancellationToken);

        _ = next.ShouldBeOfType<BudgetRejected>();
    }

    [Fact]
    public async Task CommitAsync_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reserved.Reservation.CommitAsync(5m, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => reserved.Reservation.CommitAsync(5m, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task CommitAsync_WhenActualIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() => reserved.Reservation.CommitAsync(-1m, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task ReserveAsync_WhenMaximumOpenReservationsReached_RejectsFurtherReservations()
    {
        var authority = TestFactory.Authority(options: TestFactory.DefaultOptions(maximumOpenReservationsPerScope: 2));
        var scope = await TestFactory.CreateRootScopeAsync(authority);

        _ = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m), TestContext.Current.CancellationToken);
        _ = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m), TestContext.Current.CancellationToken);
        var third = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m), TestContext.Current.CancellationToken);

        _ = third.ShouldBeOfType<BudgetRejected>();
    }

    [Fact]
    public async Task ReserveAsync_WhenReservationHasExpired_IsSweptAndReleasedBeforeNextReservation()
    {
        var timeProvider = new FakeTimeProvider();
        var authority = TestFactory.Authority(
            options: TestFactory.DefaultOptions(),
            timeProvider: timeProvider);
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var expiresAt = timeProvider.GetUtcNow().AddSeconds(1);
        _ = await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m, expiresAt: expiresAt), TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        var result = await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task ReserveAsync_WhenParentHasNoRemainingCapacity_RejectsAndReleasesLocalReservation()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 5)]);
        var childResult = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;

        var result = await child.ReserveAsync(TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 6m), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetRejected>();

        var rootSnapshot = await root.GetSnapshotAsync(TestContext.Current.CancellationToken);
        rootSnapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension).Reserved.ShouldBe(Quantity(0m));
    }

    [Fact]
    public async Task ReserveAsync_WhenParentHasCapacity_ReservesAtBothLevels()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);
        var childResult = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;

        var result = await child.ReserveAsync(TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 6m), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetReserved>();

        var rootSnapshot = await root.GetSnapshotAsync(TestContext.Current.CancellationToken);
        rootSnapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension).Reserved.ShouldBe(Quantity(6m));
    }

    [Fact]
    public async Task CommitAsync_OnChildReservation_CascadesToParent()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(authority);
        var childResult = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;

        var reserved = (BudgetReserved) await child.ReserveAsync(
            TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 8m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reserved.Reservation.CommitAsync(3m, TestContext.Current.CancellationToken);

        var rootSnapshot = await root.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var usage = rootSnapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension);
        usage.Reserved.ShouldBe(Quantity(0m));
        usage.Committed.ShouldBe(Quantity(3m));
    }

    [Fact]
    public async Task DisposeAsync_OnChildReservation_CascadesReleaseToParent()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 5)]);
        var childResult = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;

        var reserved = (BudgetReserved) await child.ReserveAsync(
            TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);
        await reserved.Reservation.DisposeAsync();

        var next = await child.ReserveAsync(TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = next.ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task ReserveAsync_WhenConcurrentRequestsRaceForLastSlot_OnlyCapacityWorthSucceed()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 5)]);
        using var ready = new CountdownEvent(20);
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 20).Select(index => Task.Run(async () =>
        {
            _ = ready.Signal();
            _ = index;
            start.Wait(TestContext.Current.CancellationToken);
            return await scope.ReserveAsync(
                TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m),
                TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken)).ToArray();
        ready.Wait(TestContext.Current.CancellationToken);
        start.Set();

        var results = await Task.WhenAll(tasks);

        results.Count(static r => r is BudgetReserved).ShouldBe(5);
        results.Count(static r => r is BudgetRejected).ShouldBe(15);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => scope.GetSnapshotAsync(cts.Token).AsTask());
    }

    [Fact]
    public async Task GetSnapshotAsync_ForConfiguredButUnusedDimension_ReportsZeroUsage()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var snapshot = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var usage = snapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension);
        usage.Reserved.ShouldBe(Quantity(0m));
        usage.Committed.ShouldBe(Quantity(0m));
        _ = usage.Limit.ShouldNotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_WhenReservationWasStarted_RetainsUnresolvedCapacityUntilCommit()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 5)]);
        var reserved = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken)).Reservation;

        _ = await reserved.MarkStartedAsync(TestContext.Current.CancellationToken);
        await reserved.DisposeAsync();

        _ = (await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension), TestContext.Current.CancellationToken))
            .ShouldBeOfType<BudgetRejected>();

        _ = await reserved.CommitAsync(3m, TestContext.Current.CancellationToken);
        var usage = (await scope.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single();
        usage.Reserved.ShouldBe(Quantity(0m));
        usage.Committed.ShouldBe(Quantity(3m));
    }

    [Fact]
    public async Task ReserveAsync_WhenStartedReservationExpires_DoesNotRefundUnknownSpend()
    {
        var clock = new FakeTimeProvider();
        var authority = TestFactory.Authority(timeProvider: clock);
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 5)]);
        var reserved = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m,
                expiresAt: clock.GetUtcNow().AddSeconds(1)), TestContext.Current.CancellationToken)).Reservation;
        _ = await reserved.MarkStartedAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(2));

        _ = (await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension),
            TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetRejected>();
    }

    [Fact]
    public async Task MarkStartedAsync_WhenUnstartedReservationExpired_ReturnsExpiredAndStartsNoEffect()
    {
        var clock = new FakeTimeProvider();
        var authority = TestFactory.Authority(timeProvider: clock);
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var expiresAt = clock.GetUtcNow().AddSeconds(1);
        var reservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension,
                expiresAt: expiresAt), TestContext.Current.CancellationToken)).Reservation;
        clock.Advance(TimeSpan.FromSeconds(2));

        var expired = (await reservation.MarkStartedAsync(TestContext.Current.CancellationToken))
            .ShouldBeOfType<BudgetStartExpired>();
        expired.ReservationId.ShouldBe(reservation.Id);
        expired.EffectiveExpiry.ShouldBe(expiresAt);
        clock.Advance(TimeSpan.FromMinutes(1));
        var replay = (await reservation.MarkStartedAsync(TestContext.Current.CancellationToken))
            .ShouldBeOfType<BudgetStartExpired>();
        replay.ShouldBe(expired);
        replay.EffectiveExpiry.ShouldBe(expiresAt);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => reservation.CommitAsync(1m, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task MarkStartedAsync_WhenExplicitlyReleased_ThrowsInvalidOperationException()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension),
            TestContext.Current.CancellationToken)).Reservation;
        await reservation.DisposeAsync();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => reservation.MarkStartedAsync(TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task MarkStartedAsync_WhenCancellationWasRequested_DoesNotStartReservation()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension), TestContext.Current.CancellationToken)).Reservation;
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => reserved.MarkStartedAsync(source.Token).AsTask());
        await reserved.DisposeAsync();

        (await scope.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Reserved.ShouldBe(Quantity(0m));
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenLastDimensionExceedsLimit_ReservesNothingAtAnyAncestor()
    {
        var secondDimension = new BudgetDimension("test.second");
        var authority = TestFactory.Authority(dimensions: TestFactory.DefaultCatalog(
            new BudgetDimensionDescriptor(secondDimension, BudgetAggregationKind.Sum, [TestFactory.Count])));
        var root = await TestFactory.CreateRootScopeAsync(authority,
            [TestFactory.HardLimit(TestFactory.TestDimension, 10), TestFactory.HardLimit(secondDimension, 1)]);
        var childResult = await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(parentScopeId: root.Id), TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;
        var operation = new OperationId(Guid.NewGuid());
        ImmutableArray<BudgetReservationRequest> requests =
        [
            TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 5m, idempotencyKey: "batch-a", operationId: operation),
            TestFactory.ReservationRequest(child.Id, secondDimension, 2m, idempotencyKey: "batch-b", operationId: operation),
        ];

        _ = (await child.ReserveBatchAsync(requests, TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetBatchRejected>();

        (await root.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages
            .Aggregate(default(BudgetQuantity), static (total, usage) => total.Add(usage.Reserved))
            .ShouldBe(Quantity(0m));
        (await child.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages
            .Aggregate(default(BudgetQuantity), static (total, usage) => total.Add(usage.Reserved))
            .ShouldBe(Quantity(0m));
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenReplayed_ReturnsSameOrderedReservationsAndRejectsSplicing()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var operation = new OperationId(Guid.NewGuid());
        ImmutableArray<BudgetReservationRequest> requests =
        [
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, idempotencyKey: "batch-a", operationId: operation),
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, idempotencyKey: "batch-b", operationId: operation),
        ];

        var first = (BudgetBatchReserved) await scope.ReserveBatchAsync(requests, TestContext.Current.CancellationToken);
        var replay = (BudgetBatchReserved) await scope.ReserveBatchAsync(requests, TestContext.Current.CancellationToken);

        replay.Reservations.Select(static item => item.Id).ShouldBe(first.Reservations.Select(static item => item.Id));
        var changed = requests.SetItem(1,
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, idempotencyKey: "batch-c", operationId: operation));
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => scope.ReserveBatchAsync(changed, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenWorkersRaceForOneBatchOfCapacity_AdmitsExactlyOneWholeBatch()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 2)]);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 2).Select(worker => Task.Run(async () =>
        {
            var operationId = new OperationId(Guid.NewGuid());
            ImmutableArray<BudgetReservationRequest> requests =
            [
                TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension,
                    idempotencyKey: $"{worker}-one", operationId: operationId),
                TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension,
                    idempotencyKey: $"{worker}-two", operationId: operationId),
            ];
            _ = ready.Signal();
            start.Wait(TestContext.Current.CancellationToken);
            return await scope.ReserveBatchAsync(requests, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken)).ToArray();
        ready.Wait(TestContext.Current.CancellationToken);
        start.Set();

        var results = await Task.WhenAll(tasks);

        results.Count(static result => result is BudgetBatchReserved).ShouldBe(1);
        results.Count(static result => result is BudgetBatchRejected).ShouldBe(1);
        (await scope.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Reserved.ShouldBe(Quantity(2m));
    }

    [Fact]
    public async Task CorrectAsync_WhenRevisionAdvances_ReplacesActualOnceAndRejectsConflictingReplay()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);
        var reservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken)).Reservation;
        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reservation.CommitAsync(8m, TestContext.Current.CancellationToken);

        var correction = await reservation.CorrectAsync(4m, 1, TestContext.Current.CancellationToken);
        var replay = await reservation.CorrectAsync(4m, 1, TestContext.Current.CancellationToken);

        correction.PreviousActual.ShouldBe(8m);
        replay.PreviousActual.ShouldBe(8m);
        replay.CorrectedActual.ShouldBe(4m);
        (await scope.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Committed.ShouldBe(Quantity(4m));
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => reservation.CorrectAsync(3m, 1, TestContext.Current.CancellationToken).AsTask());
        _ = (await scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 6m),
            TestContext.Current.CancellationToken)).ShouldBeOfType<BudgetReserved>();
    }

    [Fact]
    public async Task CorrectAsync_OnChildReservation_ReplacesAccountingAtEveryAncestor()
    {
        var authority = TestFactory.Authority();
        var root = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);
        var childResult = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(
                parentScopeId: root.Id,
                limits: [TestFactory.HardLimit(TestFactory.TestDimension, 10)]),
            TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;
        var reservation = ((BudgetReserved) await child.ReserveAsync(
            TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, 5m),
            TestContext.Current.CancellationToken)).Reservation;
        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reservation.CommitAsync(5m, TestContext.Current.CancellationToken);

        _ = await reservation.CorrectAsync(2m, 1, TestContext.Current.CancellationToken);

        (await child.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Committed.ShouldBe(Quantity(2m));
        (await root.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Committed.ShouldBe(Quantity(2m));
    }

    [Fact]
    public async Task ReserveBatchAsync_WhenIdentityGenerationFails_AppliesNoAccountingOrBindings()
    {
        var generator = new ThrowingReservationIdGenerator(2);
        var authority = TestFactory.Authority(reservationIds: generator);
        var root = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);
        var childResult = await authority.CreateChildScopeAsync(
            TestFactory.ScopeRequest(
                parentScopeId: root.Id,
                limits: [TestFactory.HardLimit(TestFactory.TestDimension, 10)]),
            TestContext.Current.CancellationToken);
        var child = (InMemoryBudgetScope) ((BudgetScopeCreated) childResult).Scope;
        var operationId = new OperationId(Guid.NewGuid());
        ImmutableArray<BudgetReservationRequest> requests =
        [
            TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, idempotencyKey: "one", operationId: operationId),
            TestFactory.ReservationRequest(child.Id, TestFactory.TestDimension, idempotencyKey: "two", operationId: operationId),
        ];

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => child.ReserveBatchAsync(requests, TestContext.Current.CancellationToken).AsTask());

        (await child.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Reserved.ShouldBe(Quantity(0m));
        (await root.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Reserved.ShouldBe(Quantity(0m));
        var retry = await child.ReserveBatchAsync(requests, TestContext.Current.CancellationToken);
        _ = retry.ShouldBeOfType<BudgetBatchReserved>();
    }

    [Fact]
    public async Task ReserveAsync_WhenIdentityGeneratorReturnsDefault_ThrowsBeforeAccounting()
    {
        var authority = TestFactory.Authority(reservationIds: new DefaultReservationIdGenerator());
        var scope = await TestFactory.CreateRootScopeAsync(
            authority, [TestFactory.HardLimit(TestFactory.TestDimension, 10)]);

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => scope.ReserveAsync(
                TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension),
                TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("id");
        (await scope.GetSnapshotAsync(TestContext.Current.CancellationToken)).Usages.Single().Reserved.ShouldBe(Quantity(0m));
    }

    private static BudgetQuantity Quantity(decimal value) => BudgetQuantity.FromDecimal(value);

    private sealed class ThrowingReservationIdGenerator(int throwOnCall): IIdentifierGenerator<BudgetReservationId>
    {
        private int _calls;

        public BudgetReservationId Create()
        {
            return Interlocked.Increment(ref _calls) == throwOnCall
                ? throw new InvalidOperationException("Injected identity failure.")
                : new BudgetReservationId(Guid.NewGuid());
        }
    }

    private sealed class DefaultReservationIdGenerator: IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => default;
    }
}
