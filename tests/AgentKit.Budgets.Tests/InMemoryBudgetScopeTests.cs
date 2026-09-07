// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class InMemoryBudgetScopeTests
{
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
        rejected.Failure.RequestedAmount.ShouldBe(11m);
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
    public async Task ReserveAsync_WhenIdempotencyKeyReusedAfterOriginalDisposed_CreatesNewReservation()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var request = TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m, idempotencyKey: "key-1");

        var first = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);
        await first.ShouldBeOfType<BudgetReserved>().Reservation.DisposeAsync();

        var second = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);

        var secondReservation = second.ShouldBeOfType<BudgetReserved>().Reservation;
        secondReservation.Id.ShouldNotBe(((BudgetReserved) first).Reservation.Id);
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
        snapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension).Reserved.ShouldBe(0m);
    }

    [Fact]
    public async Task DisposeAsync_AfterCommit_DoesNotDoubleRelease()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 5m), TestContext.Current.CancellationToken);

        _ = await reserved.Reservation.CommitAsync(5m, TestContext.Current.CancellationToken);
        await reserved.Reservation.DisposeAsync();

        var snapshot = await scope.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var usage = snapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension);
        usage.Committed.ShouldBe(5m);
        usage.Reserved.ShouldBe(0m);
    }

    [Fact]
    public async Task CommitAsync_WhenActualIsLessThanReserved_ReleasesRemainder()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reserved = (BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 10m), TestContext.Current.CancellationToken);

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
        rootSnapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension).Reserved.ShouldBe(0m);
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
        rootSnapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension).Reserved.ShouldBe(6m);
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

        _ = await reserved.Reservation.CommitAsync(3m, TestContext.Current.CancellationToken);

        var rootSnapshot = await root.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var usage = rootSnapshot.Usages.Single(u => u.Dimension == TestFactory.TestDimension);
        usage.Reserved.ShouldBe(0m);
        usage.Committed.ShouldBe(3m);
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

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => scope.ReserveAsync(TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m), TestContext.Current.CancellationToken).AsTask())
            .ToArray();

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
        usage.Reserved.ShouldBe(0m);
        usage.Committed.ShouldBe(0m);
        _ = usage.Limit.ShouldNotBeNull();
    }
}
