// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookInvocationAttemptTests
{
    [Fact]
    public void Constructor_WhenPointIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationAttempt(
            default, new HookDispatchId(Guid.NewGuid()), HookReentrancyPolicy.Forbidden));

        exception.ParamName.ShouldBe("point");
    }

    [Fact]
    public void Constructor_WhenDispatchIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationAttempt(
            HookKernelTestData.Point, default, HookReentrancyPolicy.Forbidden));

        exception.ParamName.ShouldBe("dispatchId");
    }

    [Fact]
    public void Constructor_WhenReentrancyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationAttempt(
            HookKernelTestData.Point, new HookDispatchId(Guid.NewGuid()), (HookReentrancyPolicy) 99));

        exception.ParamName.ShouldBe("reentrancy");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var dispatchId = new HookDispatchId(Guid.NewGuid());

        var attempt = new HookInvocationAttempt(HookKernelTestData.Point, dispatchId, HookReentrancyPolicy.Bounded);

        attempt.Point.ShouldBe(HookKernelTestData.Point);
        attempt.DispatchId.ShouldBe(dispatchId);
        attempt.Reentrancy.ShouldBe(HookReentrancyPolicy.Bounded);
    }
}
