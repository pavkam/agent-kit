// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies WorkPlan behavior and contracts.</summary>
public sealed class WorkPlanTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var plan = Plan();
        plan.Id.ShouldBe(PlanId());
        plan.Revision.ShouldBe(new PlanRevision(1));
        plan.Title.ShouldBe("Plan");
        plan.Items.ShouldBe(Items());
        plan.Author.ShouldBe(Identity());
        plan.UpdatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenTitleIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WorkPlan(PlanId(), new PlanRevision(1), " ", Items(), Identity(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("title");
    }

    [Fact]
    public void Constructor_WhenItemsContainNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WorkPlan(PlanId(), new PlanRevision(1), "Plan", [null!], Identity(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void Constructor_WhenItemCountIsBelowMinimum_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WorkPlan(PlanId(), new PlanRevision(1), "Plan", [], Identity(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("items.Length");
    }

    [Fact]
    public void Constructor_WhenItemCountExceedsMaximum_ThrowsExactParameter()
    {
        var items = Enumerable.Range(0, 51).Select(i => new WorkPlanItem(new PlanItemId($"item-{i}"), "Text.", PlanItemStatus.Pending)).ToImmutableArray();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WorkPlan(PlanId(), new PlanRevision(1), "Plan", items, Identity(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("items.Length");
    }

    [Fact]
    public void Constructor_WhenItemsRepeatId_ThrowsExactParameter()
    {
        ImmutableArray<WorkPlanItem> items = [new(new PlanItemId("one"), "First.", PlanItemStatus.Pending), new(new PlanItemId("one"), "Second.", PlanItemStatus.Pending)];
        var exception = Should.Throw<ArgumentException>(() => new WorkPlan(PlanId(), new PlanRevision(1), "Plan", items, Identity(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void Constructor_WhenMultipleItemsInProgress_ThrowsExactParameter()
    {
        ImmutableArray<WorkPlanItem> items = [new(new PlanItemId("one"), "First.", PlanItemStatus.InProgress), new(new PlanItemId("two"), "Second.", PlanItemStatus.InProgress)];
        var exception = Should.Throw<ArgumentException>(() => new WorkPlan(PlanId(), new PlanRevision(1), "Plan", items, Identity(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void Constructor_WhenAuthorIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WorkPlan(PlanId(), new PlanRevision(1), "Plan", Items(), null!, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("author");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = Plan();
        var second = Plan();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenItemsDiffer_IsNotEqual()
    {
        var first = Plan();
        var second = Plan() with { Items = [new WorkPlanItem(new PlanItemId("one"), "Changed.", PlanItemStatus.Pending)] };
        first.ShouldNotBe(second);
    }

    private static PlanId PlanId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static ImmutableArray<WorkPlanItem> Items() => [new(new PlanItemId("one"), "First.", PlanItemStatus.Pending)];
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static WorkPlan Plan() => new(PlanId(), new PlanRevision(1), "Plan", Items(), Identity(), DateTimeOffset.UnixEpoch);
}
