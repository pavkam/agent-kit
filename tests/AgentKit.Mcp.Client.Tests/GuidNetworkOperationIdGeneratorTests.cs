// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="GuidNetworkOperationIdGenerator"/> produces fresh typed identities.</summary>
public sealed class GuidNetworkOperationIdGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonEmptyIdentities()
    {
        var generator = new GuidNetworkOperationIdGenerator();

        var first = generator.Create();
        var second = generator.Create();

        first.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
