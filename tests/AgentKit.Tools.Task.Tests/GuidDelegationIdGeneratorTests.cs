// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;

public sealed class GuidDelegationIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonEmptyIdentities()
    {
        var generator = new GuidDelegationIdGenerator();

        var first = generator.Create();
        var second = generator.Create();

        first.Value.ShouldNotBe(Guid.Empty);
        second.Value.ShouldNotBe(Guid.Empty);
        first.ShouldNotBe(second);
    }
}
