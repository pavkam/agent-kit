// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies WorkPlanItem behavior and contracts.</summary>
public sealed class WorkPlanItemTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var item = new WorkPlanItem(new PlanItemId("one"), "First.", PlanItemStatus.Pending);
        item.Id.ShouldBe(new PlanItemId("one"));
        item.Text.ShouldBe("First.");
        item.Status.ShouldBe(PlanItemStatus.Pending);
    }

    [Fact]
    public void Constructor_WhenTextIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WorkPlanItem(new PlanItemId("one"), " ", PlanItemStatus.Pending));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WorkPlanItem(new PlanItemId("one"), "First.", (PlanItemStatus) 999));
        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new WorkPlanItem(new PlanItemId("one"), "First.", PlanItemStatus.Pending);
        var second = new WorkPlanItem(new PlanItemId("one"), "First.", PlanItemStatus.Pending);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new WorkPlanItem(new PlanItemId("one"), "First.", PlanItemStatus.Pending);
        var copy = original with { Status = PlanItemStatus.Completed };
        copy.Status.ShouldBe(PlanItemStatus.Completed);
        original.Status.ShouldBe(PlanItemStatus.Pending);
    }
}
