// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

public sealed class DelegationIntentQueueTests
{
    private static DelegationIntent Intent()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var parent = GoalTestData.Goal(agent, session, run, id: RunRootGoal.GoalIdFor(run));
        var request = GoalTestData.Delegation(parent, RunRootGoal.AttemptIdFor(run), run, GoalTestData.NewAgent(), "k", GoalTestData.Authorization(agent, session, run));
        var child = GoalTestData.Goal(agent, session, run, parentId: parent.Id, status: GoalStatus.Ready);
        return new DelegationIntent(request, new GoalRecord(child, [], [], request, 1, 1, null));
    }

    private static DelegationIntentQueue Queue(int capacity) => new(Options.Create(new GoalWorkerOptions { QueueCapacity = capacity }));

    [Fact]
    public void Constructor_WhenOptionsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegationIntentQueue(null!)).ParamName.ShouldBe("options");

    [Fact]
    public async Task SignalAsync_WhenIntentIsNull_ThrowsArgumentNullException() =>
        (await Should.ThrowAsync<ArgumentNullException>(async () => await Queue(1).SignalAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("intent");

    [Fact]
    public async Task SignalAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Queue(1).SignalAsync(Intent(), cancel.Token));
    }

    [Fact]
    public async Task SignalAsync_WhenSignalled_DeliversTheIntentToTheReader()
    {
        var queue = Queue(2);
        var intent = Intent();

        await queue.SignalAsync(intent, TestContext.Current.CancellationToken);

        queue.Reader.TryRead(out var read).ShouldBeTrue();
        read.ShouldBeSameAs(intent);
    }

    [Fact]
    public async Task SignalAsync_WhenQueueIsFull_DropsTheHintWithoutBlockingOrThrowing()
    {
        var queue = Queue(1);
        var first = Intent();

        await queue.SignalAsync(first, TestContext.Current.CancellationToken);
        await queue.SignalAsync(Intent(), TestContext.Current.CancellationToken);

        queue.Reader.TryRead(out var read).ShouldBeTrue();
        read.ShouldBeSameAs(first);
        queue.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SignalAsync_WhenAnActivationCallbackIsConfigured_InvokesItAfterQueueing()
    {
        var calls = 0;
        var queue = new DelegationIntentQueue(Options.Create(new GoalWorkerOptions()), () =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await queue.SignalAsync(Intent(), TestContext.Current.CancellationToken);

        calls.ShouldBe(1);
        queue.Reader.TryRead(out _).ShouldBeTrue();
    }

    [Fact]
    public async Task SignalAsync_WhenActivationFails_StillQueuesTheIntentAndSurfacesTheFailure()
    {
        var queue = new DelegationIntentQueue(Options.Create(new GoalWorkerOptions()), static () => throw new InvalidOperationException("start failed"));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await queue.SignalAsync(Intent(), TestContext.Current.CancellationToken));

        queue.Reader.TryRead(out _).ShouldBeTrue();
    }
}
