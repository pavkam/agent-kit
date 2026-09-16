// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit.Tests;

public sealed class GuidWorkspaceMutationIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonEmptyIdentities()
    {
        var generator = new GuidWorkspaceMutationIdGenerator();

        var first = generator.Create();
        var second = generator.Create();

        first.Value.ShouldNotBe(Guid.Empty);
        second.Value.ShouldNotBe(Guid.Empty);
        first.ShouldNotBe(second);
    }
}
