// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

/// <summary>Verifies <see cref="GuidIdentifierGenerator{TId}"/> produces fresh typed identities.</summary>
public sealed class GuidIdentifierGeneratorTests
{
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsDistinctNonEmptyIdentities()
    {
        var generator = new GuidIdentifierGenerator<SecurityRequestId>(static value => new SecurityRequestId(value));

        var first = generator.Create();
        var second = generator.Create();

        first.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
