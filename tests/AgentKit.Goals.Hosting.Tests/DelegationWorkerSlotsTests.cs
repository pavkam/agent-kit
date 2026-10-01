// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

public sealed class DelegationWorkerSlotsTests
{
    private static DelegationWorkerSlots Slots(int count) => new(Options.Create(new GoalWorkerOptions { MaximumConcurrentChildren = count }));

    [Fact]
    public void Constructor_WhenOptionsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegationWorkerSlots(null!)).ParamName.ShouldBe("options");

    [Fact]
    public async Task AcquireAsync_WhenAllSlotsAreHeld_WaitsUntilOneIsReleased()
    {
        using var slots = Slots(1);
        await using var first = await slots.AcquireAsync(TestContext.Current.CancellationToken);

        var second = slots.AcquireAsync(TestContext.Current.CancellationToken).AsTask();

        second.IsCompleted.ShouldBeFalse();
        await first.DisposeAsync();
        await using var lease = await second;
        slots.Available.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelledWhileWaiting_ThrowsOperationCanceledException()
    {
        using var slots = Slots(1);
        await using var held = await slots.AcquireAsync(TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource();
        var waiting = slots.AcquireAsync(cancel.Token).AsTask();

        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task ParkAsync_WhenRunIsNotBound_IsANoOp()
    {
        using var slots = Slots(1);
        await using var held = await slots.AcquireAsync(TestContext.Current.CancellationToken);

        await using (await slots.ParkAsync(new SessionId(Guid.NewGuid()), TestContext.Current.CancellationToken))
        {
            slots.Available.ShouldBe(0);
        }

        slots.Available.ShouldBe(0);
    }

    [Fact]
    public async Task ParkAsync_WhenBoundRunWaits_ReleasesItsSlotForTheWaitAndReacquiresAfterward()
    {
        using var slots = Slots(1);
        var run = new SessionId(Guid.NewGuid());
        await using var held = await slots.AcquireAsync(TestContext.Current.CancellationToken);
        held.Bind(run);

        var parked = await slots.ParkAsync(run, TestContext.Current.CancellationToken);
        slots.Available.ShouldBe(1);
        await using (var nested = await slots.AcquireAsync(TestContext.Current.CancellationToken))
        {
            slots.Available.ShouldBe(0);
        }

        await parked.DisposeAsync();

        slots.Available.ShouldBe(0);
        await held.DisposeAsync();
        slots.Available.ShouldBe(1);
    }

    [Fact]
    public async Task ParkAsync_WhenAlreadyParked_DoesNotReleaseASecondPermit()
    {
        using var slots = Slots(2);
        var run = new SessionId(Guid.NewGuid());
        await using var held = await slots.AcquireAsync(TestContext.Current.CancellationToken);
        held.Bind(run);

        await using var first = await slots.ParkAsync(run, TestContext.Current.CancellationToken);
        await using var second = await slots.ParkAsync(run, TestContext.Current.CancellationToken);

        slots.Available.ShouldBe(2);
    }

    [Fact]
    public async Task DisposeAsync_WhenLeaseIsDisposedWhileParked_DoesNotReleaseTwice()
    {
        using var slots = Slots(1);
        var run = new SessionId(Guid.NewGuid());
        var held = await slots.AcquireAsync(TestContext.Current.CancellationToken);
        held.Bind(run);
        var parked = await slots.ParkAsync(run, TestContext.Current.CancellationToken);

        await held.DisposeAsync();
        await parked.DisposeAsync();

        slots.Available.ShouldBe(1);
        await using var park = await slots.ParkAsync(run, TestContext.Current.CancellationToken);
        slots.Available.ShouldBe(1);
    }

    [Fact]
    public async Task ParkAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var slots = Slots(1);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await slots.ParkAsync(new SessionId(Guid.NewGuid()), cancel.Token));
    }
}
