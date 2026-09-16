// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

/// <summary>Verifies GuidNetworkOperationIdGenerator behavior and contracts.</summary>
public sealed class GuidNetworkOperationIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledTwice_ReturnsDistinctNonDefaultIdentities()
    {
        var generator = new GuidNetworkOperationIdGenerator();
        var first = generator.Create();
        var second = generator.Create();
        first.ShouldNotBe(default);
        second.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
