// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies DefaultToolCallIdGenerator behavior and contracts.</summary>
public sealed class DefaultToolCallIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledTwice_ProducesDistinctNonDefaultIdentities()
    {
        var generator = new DefaultToolCallIdGenerator();
        var first = generator.Create();
        var second = generator.Create();
        first.ShouldNotBe(default);
        second.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
