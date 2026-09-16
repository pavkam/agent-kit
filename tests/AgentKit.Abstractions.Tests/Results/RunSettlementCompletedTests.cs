// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class RunSettlementCompletedTests
{
    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new RunSettlementCompleted();
        var second = new RunSettlementCompleted();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunSettlementCompleted();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
