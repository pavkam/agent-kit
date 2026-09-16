// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

public sealed class GuidWebSearchRequestIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonEmptyIdentities()
    {
        var generator = new GuidWebSearchRequestIdGenerator();

        var first = generator.Create();
        var second = generator.Create();

        first.Value.ShouldNotBe(Guid.Empty);
        second.Value.ShouldNotBe(Guid.Empty);
        first.ShouldNotBe(second);
    }
}
