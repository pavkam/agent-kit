// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputDefinitionResolved behavior and contracts.</summary>
public sealed class OutputDefinitionResolvedTests
{
    [Fact]
    public void Constructor_WhenDefinitionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputDefinitionResolved(null!)).ParamName.ShouldBe("definition");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsDefinition()
    {
        var definition = OutputTestData.Definition();
        var resolved = new OutputDefinitionResolved(definition);
        resolved.Definition.ShouldBe(definition);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputDefinitionResolved(OutputTestData.Definition());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
