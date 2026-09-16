// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies ResolvedAgentDefinition behavior and contracts.</summary>
public sealed class ResolvedAgentDefinitionTests
{
    [Fact]
    public void Constructor_WhenDefinitionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ResolvedAgentDefinition(null!, new AgentCatalogVersion(1))).ParamName.ShouldBe("definition");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var definition = CompositionTestData.Definition();
        var version = new AgentCatalogVersion(2);
        var resolved = new ResolvedAgentDefinition(definition, version);
        resolved.Definition.ShouldBe(definition);
        resolved.CatalogVersion.ShouldBe(version);
    }

    [Fact]
    public void With_WhenDefinitionIsNull_ThrowsExactParameter()
    {
        var resolved = new ResolvedAgentDefinition(CompositionTestData.Definition(), new AgentCatalogVersion(1));
        Should.Throw<ArgumentNullException>(() => _ = resolved with { Definition = null! }).ParamName.ShouldBe("Definition");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ResolvedAgentDefinition(CompositionTestData.Definition(), new AgentCatalogVersion(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenDefinitionIsValid_UpdatesDefinition()
    {
        var resolved = new ResolvedAgentDefinition(CompositionTestData.Definition(), new AgentCatalogVersion(1));
        var changed = CompositionTestData.Definition(new SecurityProfileKey("other-security"));
        var updated = resolved with { Definition = changed };
        updated.Definition.ShouldBeSameAs(changed);
    }
}
