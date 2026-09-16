// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputDefinitionNotFound behavior and contracts.</summary>
public sealed class OutputDefinitionNotFoundTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsId()
    {
        var id = new OutputDefinitionId("output");
        var notFound = new OutputDefinitionNotFound(id);
        notFound.Id.ShouldBe(id);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputDefinitionNotFound(new OutputDefinitionId("output"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
