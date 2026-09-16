// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentCatalogSnapshot behavior and contracts.</summary>
public sealed class AgentCatalogSnapshotTests
{
    [Fact]
    public void Constructor_WhenDefinitionsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentCatalogSnapshot(new AgentCatalogVersion(1), [null!])).ParamName.ShouldBe("definitions");

    [Fact]
    public void Constructor_WhenDefinitionsHaveDuplicateAgentId_ThrowsExactParameter()
    {
        var definition = CompositionTestData.Definition();
        Should.Throw<ArgumentException>(() => new AgentCatalogSnapshot(new AgentCatalogVersion(1), [definition, definition])).ParamName.ShouldBe("definitions");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var definition = CompositionTestData.Definition();
        var version = new AgentCatalogVersion(1);
        var snapshot = new AgentCatalogSnapshot(version, [definition]);
        snapshot.Version.ShouldBe(version);
        snapshot.Definitions.ShouldBe([definition]);
    }

    [Fact]
    public void FindDefinition_WhenAgentIdMatches_ReturnsDefinition()
    {
        var definition = CompositionTestData.Definition();
        var snapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(1), [definition]);
        snapshot.FindDefinition(CompositionTestData.AgentId).ShouldBe(definition);
    }

    [Fact]
    public void FindDefinition_WhenAgentIdDoesNotMatch_ReturnsNull()
    {
        var snapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(1), [CompositionTestData.Definition()]);
        snapshot.FindDefinition(new AgentId(Guid.Parse("b0000000-0000-0000-0000-000000000002"))).ShouldBeNull();
    }

    [Fact]
    public void Definitions_WhenSetToArrayContainingNull_ThrowsExactParameter()
    {
        var snapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(1), []);
        Should.Throw<ArgumentException>(() => _ = snapshot with { Definitions = [null!] }).ParamName.ShouldBe("Definitions");
    }

    [Fact]
    public void Definitions_WhenSetToArrayWithDuplicateAgentId_ThrowsExactParameter()
    {
        var definition = CompositionTestData.Definition();
        var snapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(1), []);
        Should.Throw<ArgumentException>(() => _ = snapshot with { Definitions = [definition, definition] }).ParamName.ShouldBe("Definitions");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentCatalogSnapshot(new AgentCatalogVersion(1), [CompositionTestData.Definition()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Definitions_WhenSetToValidArray_UpdatesDefinitions()
    {
        var snapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(1), []);
        var definition = CompositionTestData.Definition();
        var updated = snapshot with { Definitions = [definition] };
        updated.Definitions.ShouldBe([definition]);
    }
}
