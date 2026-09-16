// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class RunSucceededTests
{
    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new RunSucceeded();
        var second = new RunSucceeded();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunSucceeded();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
