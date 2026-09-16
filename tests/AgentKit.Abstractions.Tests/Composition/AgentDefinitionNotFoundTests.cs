// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentDefinitionNotFound behavior and contracts.</summary>
public sealed class AgentDefinitionNotFoundTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsAgentId()
    {
        var id = new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var notFound = new AgentDefinitionNotFound(id);
        notFound.AgentId.ShouldBe(id);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentDefinitionNotFound(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
