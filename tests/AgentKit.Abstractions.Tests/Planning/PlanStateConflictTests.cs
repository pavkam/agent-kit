// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies PlanStateConflict behavior and contracts.</summary>
public sealed class PlanStateConflictTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var conflict = new PlanStateConflict(new PlanRevision(2));
        conflict.CurrentRevision.ShouldBe(new PlanRevision(2));
    }

    [Fact]
    public void Constructor_WhenCurrentRevisionIsNull_InitializesNullRevision()
    {
        var conflict = new PlanStateConflict(null);
        conflict.CurrentRevision.ShouldBeNull();
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new PlanStateConflict(new PlanRevision(2));
        var second = new PlanStateConflict(new PlanRevision(2));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new PlanStateConflict(new PlanRevision(2));
        var copy = original with { CurrentRevision = new PlanRevision(3) };
        copy.CurrentRevision.ShouldBe(new PlanRevision(3));
        original.CurrentRevision.ShouldBe(new PlanRevision(2));
    }
}
