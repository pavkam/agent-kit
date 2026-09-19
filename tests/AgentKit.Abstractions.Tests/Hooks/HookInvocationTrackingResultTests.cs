// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookInvocationTrackingResultTests
{
    [Fact]
    public void EnteredConstructor_WhenDepthIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationTrackingEntered(0));
        exception.ParamName.ShouldBe("depth");
    }

    [Fact]
    public void EnteredConstructor_WhenValid_RoundTripsDepth() => new HookInvocationTrackingEntered(1).Depth.ShouldBe(1);

    [Fact]
    public void RejectedConstructor_WhenActiveDepthIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationTrackingRejected(-1, 8));
        exception.ParamName.ShouldBe("activeDepth");
    }

    [Fact]
    public void RejectedConstructor_WhenPermittedDepthIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationTrackingRejected(8, -1));
        exception.ParamName.ShouldBe("permittedDepth");
    }

    [Fact]
    public void RejectedConstructor_WhenValid_RoundTripsEveryProperty()
    {
        var rejected = new HookInvocationTrackingRejected(8, 8);

        rejected.ActiveDepth.ShouldBe(8);
        rejected.PermittedDepth.ShouldBe(8);
    }

    [Fact]
    public void Hierarchy_IsClosedToEnteredAndRejected()
    {
        HookInvocationTrackingResult entered = new HookInvocationTrackingEntered(1);
        HookInvocationTrackingResult rejected = new HookInvocationTrackingRejected(8, 8);

        _ = entered.ShouldBeOfType<HookInvocationTrackingEntered>();
        _ = rejected.ShouldBeOfType<HookInvocationTrackingRejected>();
    }
}
