// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies PlanStateMissing behavior and contracts.</summary>
public sealed class PlanStateMissingTests
{
    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new PlanStateMissing();
        var second = new PlanStateMissing();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new PlanStateMissing();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
