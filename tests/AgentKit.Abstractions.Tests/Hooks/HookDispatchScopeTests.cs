// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;



/// <summary>Verifies HookDispatchScope behavior and contracts.</summary>
public sealed class HookDispatchScopeTests
{
    [Fact]
    public void Root_WhenAccessed_HasNoActiveDepths()
    {
        var root = HookDispatchScope.Root;
        root.ActiveDepths.ShouldBeEmpty();
        root.DepthOf(new HookPointId("session.creating")).ShouldBe(0);
    }

    [Fact]
    public void DepthOf_WhenPointIsUnknown_ReturnsZero() =>
        HookDispatchScope.Root.DepthOf(new HookPointId("unregistered")).ShouldBe(0);

    [Fact]
    public void Entering_WhenCalled_IncrementsRecordedDepth()
    {
        var point = new HookPointId("session.creating");
        var scope = HookDispatchScope.Root.Entering(point);
        scope.DepthOf(point).ShouldBe(1);
        var nested = scope.Entering(point);
        nested.DepthOf(point).ShouldBe(2);
    }

    [Fact]
    public void Entering_WhenCalled_DoesNotMutateOriginalScope()
    {
        var point = new HookPointId("session.creating");
        var original = HookDispatchScope.Root;
        _ = original.Entering(point);
        original.DepthOf(point).ShouldBe(0);
    }

    [Fact]
    public void Equals_WhenActiveDepthsMatch_InstancesAreEqual()
    {
        var point = new HookPointId("session.creating");
        var first = HookDispatchScope.Root.Entering(point);
        var second = HookDispatchScope.Root.Entering(point);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenComparedToNull_ReturnsFalse() =>
        HookDispatchScope.Root.Equals(null).ShouldBeFalse();

    [Fact]
    public void Equals_WhenActiveDepthCountsDiffer_ReturnsFalse()
    {
        var point = new HookPointId("session.creating");
        var withOne = HookDispatchScope.Root.Entering(point);
        var withTwo = withOne.Entering(new HookPointId("session.created"));
        withOne.ShouldNotBe(withTwo);
    }

    [Fact]
    public void Equals_WhenSamePointHasDifferentDepth_ReturnsFalse()
    {
        var point = new HookPointId("session.creating");
        var shallow = HookDispatchScope.Root.Entering(point);
        var deep = shallow.Entering(point);
        shallow.ShouldNotBe(deep);
    }

    [Fact]
    public void Equals_WhenDifferentPointsAreActive_ReturnsFalse()
    {
        var first = HookDispatchScope.Root.Entering(new HookPointId("session.creating"));
        var second = HookDispatchScope.Root.Entering(new HookPointId("session.created"));
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = HookDispatchScope.Root.Entering(new HookPointId("session.creating"));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void GetHashCode_WhenMultiplePointsAreActive_IsOrderIndependent()
    {
        var first = HookDispatchScope.Root.Entering(new HookPointId("a.point")).Entering(new HookPointId("b.point"));
        var second = HookDispatchScope.Root.Entering(new HookPointId("b.point")).Entering(new HookPointId("a.point"));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
