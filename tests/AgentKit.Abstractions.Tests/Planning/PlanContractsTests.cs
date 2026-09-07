// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;

using AgentKit;

using Shouldly;

public sealed class PlanContractsTests
{
    [Fact]
    public void ThrowIfDuplicatePlanItemIds_WhenIdsDuplicate_InfersParameterName()
    {
        var item = new WorkPlanItem(new PlanItemId("same"), "Work.", PlanItemStatus.Pending);
        ImmutableArray<WorkPlanItem> items = [item, item];

        var action = () => ArgumentException.ThrowIfDuplicatePlanItemIds(items);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(items));
    }

    [Fact]
    public void ThrowIfMultipleInProgressPlanItems_WhenOneActive_DoesNotThrow()
    {
        ImmutableArray<WorkPlanItem> items =
        [
            new(new PlanItemId("one"), "First.", PlanItemStatus.InProgress),
            new(new PlanItemId("two"), "Second.", PlanItemStatus.Pending),
        ];

        var action = () => ArgumentException.ThrowIfMultipleInProgressPlanItems(items);

        action.ShouldNotThrow();
    }

    [Fact]
    public void ThrowIfMultipleInProgressPlanItems_WhenTwoActive_InfersParameterName()
    {
        ImmutableArray<WorkPlanItem> items =
        [
            new(new PlanItemId("one"), "First.", PlanItemStatus.InProgress),
            new(new PlanItemId("two"), "Second.", PlanItemStatus.InProgress),
        ];

        var action = () => ArgumentException.ThrowIfMultipleInProgressPlanItems(items);

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(items));
    }

    [Fact]
    public void ReplaceFingerprint_WhenOrderedItemStateChanges_ChangesEvidence()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        ImmutableArray<WorkPlanItem> original =
        [new(new PlanItemId("one"), "First.", PlanItemStatus.Pending)];
        ImmutableArray<WorkPlanItem> changed =
        [new(new PlanItemId("one"), "First.", PlanItemStatus.Completed)];

        var first = PlanSecurityBinding.ReplaceFingerprint(address, "Plan", original, null);
        var second = PlanSecurityBinding.ReplaceFingerprint(address, "Plan", changed, null);

        second.ShouldNotBe(first);
    }
}
