// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class HookDispatchScopeTests
{
    [Fact]
    public void Root_WhenAccessed_HasNoActiveDepth()
    {
        var point = new HookPointId("p");

        HookDispatchScope.Root.DepthOf(point).ShouldBe(0);
    }

    [Fact]
    public void Entering_WhenCalled_IncrementsDepthForThatPointOnly()
    {
        var point = new HookPointId("p");
        var other = new HookPointId("other");

        var entered = HookDispatchScope.Root.Entering(point);

        entered.DepthOf(point).ShouldBe(1);
        entered.DepthOf(other).ShouldBe(0);
    }

    [Fact]
    public void Entering_WhenCalledTwice_AccumulatesDepth()
    {
        var point = new HookPointId("p");

        var entered = HookDispatchScope.Root.Entering(point).Entering(point);

        entered.DepthOf(point).ShouldBe(2);
    }

    [Fact]
    public void Entering_WhenCalled_DoesNotMutateOriginalScope()
    {
        var point = new HookPointId("p");
        var original = HookDispatchScope.Root;

        _ = original.Entering(point);

        original.DepthOf(point).ShouldBe(0);
    }
}
