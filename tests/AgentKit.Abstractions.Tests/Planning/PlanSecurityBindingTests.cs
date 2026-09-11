// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;

using AgentKit;

using Shouldly;

/// <summary>Verifies PlanSecurityBinding behavior and contracts.</summary>
public sealed class PlanSecurityBindingTests
{
    [Fact]
    public void ReplaceFingerprint_WhenOrderedItemStateChanges_ChangesEvidence()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        ImmutableArray<WorkPlanItem> original = [new(new PlanItemId("one"), "First.", PlanItemStatus.Pending)];
        ImmutableArray<WorkPlanItem> changed = [new(new PlanItemId("one"), "First.", PlanItemStatus.Completed)];
        var first = PlanSecurityBinding.ReplaceFingerprint(address, "Plan", original, null);
        var second = PlanSecurityBinding.ReplaceFingerprint(address, "Plan", changed, null);
        second.ShouldNotBe(first);
    }
}
