// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class GuidModelRequestIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonDefaultIdentities()
    {
        var generator = new GuidModelRequestIdGenerator();

        var identities = Enumerable.Range(0, 32).Select(_ => generator.Create()).ToArray();

        identities.ShouldAllBe(static id => id != default);
        identities.Distinct().Count().ShouldBe(32);
    }
}
