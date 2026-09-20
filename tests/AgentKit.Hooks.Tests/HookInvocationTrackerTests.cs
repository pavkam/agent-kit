// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>Verifies <see cref="HookInvocationTracker"/> depth, rejection, and leave pairing.</summary>
public sealed class HookInvocationTrackerTests
{
    private static readonly HookPointId Point = new("agent.before-tool-invocation");

    private static readonly HookPointId OtherPoint = new("agent.before-model-request");

    [Fact]
    public void Constructor_WhenDepthNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var zero = Should.Throw<ArgumentOutOfRangeException>(() => _ = new HookInvocationTracker(0));
        var negative = Should.Throw<ArgumentOutOfRangeException>(() => _ = new HookInvocationTracker(-1));

        zero.ParamName.ShouldBe("maximumInvocationDepth");
        negative.ParamName.ShouldBe("maximumInvocationDepth");
        _ = new HookInvocationTracker(1);
    }

    [Fact]
    public void TryEnter_WhenAttemptNull_ThrowsArgumentNullException()
    {
        var tracker = new HookInvocationTracker(8);

        var exception = Should.Throw<ArgumentNullException>(() => _ = tracker.TryEnter(null!));

        exception.ParamName.ShouldBe("attempt");
    }

    [Fact]
    public void Leave_WhenDispatchDefault_ThrowsArgumentOutOfRangeException()
    {
        var tracker = new HookInvocationTracker(8);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => tracker.Leave(default));

        exception.ParamName.ShouldBe("dispatchId");
    }

    [Fact]
    public void TryEnter_WhenFirstEntry_ReturnsDepthOne()
    {
        var tracker = new HookInvocationTracker(8);

        var entered = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden)).ShouldBeOfType<HookInvocationTrackingEntered>();

        entered.Depth.ShouldBe(1);
    }

    [Fact]
    public void TryEnter_WhenForbiddenAndAlreadyActive_RejectsWithoutIncrement()
    {
        var tracker = new HookInvocationTracker(8);
        var first = Dispatch();
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden, first)).ShouldBeOfType<HookInvocationTrackingEntered>();

        var rejected = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden)).ShouldBeOfType<HookInvocationTrackingRejected>();

        rejected.ActiveDepth.ShouldBe(1);
        rejected.PermittedDepth.ShouldBe(1);
        tracker.Leave(first);
        tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden)).ShouldBeOfType<HookInvocationTrackingEntered>().Depth.ShouldBe(1);
    }

    [Fact]
    public void TryEnter_WhenBoundedWithinCeiling_IncreasesDepth()
    {
        var tracker = new HookInvocationTracker(2);

        tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded)).ShouldBeOfType<HookInvocationTrackingEntered>().Depth.ShouldBe(1);
        tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded)).ShouldBeOfType<HookInvocationTrackingEntered>().Depth.ShouldBe(2);
    }

    [Fact]
    public void TryEnter_WhenBoundedAtCeiling_Rejects()
    {
        var tracker = new HookInvocationTracker(2);
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded));
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded));

        var rejected = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded)).ShouldBeOfType<HookInvocationTrackingRejected>();

        rejected.ActiveDepth.ShouldBe(2);
        rejected.PermittedDepth.ShouldBe(2);
    }

    [Fact]
    public void Leave_WhenMatched_ReleasesDepth()
    {
        var tracker = new HookInvocationTracker(2);
        var first = Dispatch();
        var second = Dispatch();
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded, first));
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded, second));

        tracker.Leave(first);

        tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded)).ShouldBeOfType<HookInvocationTrackingEntered>().Depth.ShouldBe(2);
    }

    [Fact]
    public void TryEnter_WhenDifferentPoints_TracksDepthIndependently()
    {
        var tracker = new HookInvocationTracker(1);
        var blocking = Dispatch();
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden, blocking, Point));

        var other = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden, point: OtherPoint)).ShouldBeOfType<HookInvocationTrackingEntered>();
        var rejected = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded, point: Point)).ShouldBeOfType<HookInvocationTrackingRejected>();

        other.Depth.ShouldBe(1);
        rejected.ActiveDepth.ShouldBe(1);
        rejected.PermittedDepth.ShouldBe(1);
    }

    [Fact]
    public void Leave_WhenUnknownDispatch_ThrowsInvalidOperationException()
    {
        var tracker = new HookInvocationTracker(8);

        _ = Should.Throw<InvalidOperationException>(() => tracker.Leave(Dispatch()));
    }

    [Fact]
    public void Leave_WhenRepeated_ThrowsInvalidOperationException()
    {
        var tracker = new HookInvocationTracker(8);
        var dispatch = Dispatch();
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden, dispatch));
        tracker.Leave(dispatch);

        _ = Should.Throw<InvalidOperationException>(() => tracker.Leave(dispatch));
    }

    [Fact]
    public void TryEnter_WhenDispatchAlreadyActive_ThrowsInvalidOperationException()
    {
        var tracker = new HookInvocationTracker(8);
        var dispatch = Dispatch();
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded, dispatch));

        _ = Should.Throw<InvalidOperationException>(() => tracker.TryEnter(Attempt(HookReentrancyPolicy.Bounded, dispatch, OtherPoint)));
    }

    [Fact]
    public void TryEnter_WhenConcurrentOnOneForbiddenPoint_AdmitsOne()
    {
        var tracker = new HookInvocationTracker(8);
        var admitted = 0;

        _ = Parallel.For(0, 32, _ =>
        {
            if (tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden)) is HookInvocationTrackingEntered)
            {
                _ = Interlocked.Increment(ref admitted);
            }
        });

        admitted.ShouldBe(1);
    }

    [Fact]
    public void TryEnter_WhenDispatchLeft_AllowsSameDispatchAgain()
    {
        var tracker = new HookInvocationTracker(8);
        var dispatch = Dispatch();
        _ = tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden, dispatch));
        tracker.Leave(dispatch);

        tracker.TryEnter(Attempt(HookReentrancyPolicy.Forbidden, dispatch)).ShouldBeOfType<HookInvocationTrackingEntered>().Depth.ShouldBe(1);
    }

    private static HookInvocationAttempt Attempt(
        HookReentrancyPolicy reentrancy,
        HookDispatchId? dispatchId = null,
        HookPointId? point = null) =>
        new(point ?? Point, dispatchId ?? Dispatch(), reentrancy);

    private static HookDispatchId Dispatch() => new(Guid.NewGuid());
}
