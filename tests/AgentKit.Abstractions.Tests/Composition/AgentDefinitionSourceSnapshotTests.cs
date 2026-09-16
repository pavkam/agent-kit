// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentDefinitionSourceSnapshot behavior and contracts.</summary>
public sealed class AgentDefinitionSourceSnapshotTests
{
    [Fact]
    public void Constructor_WhenSourceIdIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentDefinitionSourceSnapshot(default, new AgentDefinitionSourceVersion(1), 0, [])).ParamName.ShouldBe("sourceId");

    [Fact]
    public void Constructor_WhenDefinitionsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 0, [null!])).ParamName.ShouldBe("definitions");

    [Fact]
    public void Constructor_WhenDefinitionsHaveDuplicateAgentId_ThrowsExactParameter()
    {
        var definition = CompositionTestData.Definition();
        Should.Throw<ArgumentException>(() => new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 0, [definition, definition])).ParamName.ShouldBe("definitions");
    }

    [Fact]
    public void Constructor_WhenDefinitionsAreEmpty_IsLocallyValid()
    {
        var snapshot = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 0, []);
        snapshot.Definitions.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var sourceId = new AgentDefinitionSourceId("source");
        var version = new AgentDefinitionSourceVersion(1);
        var definition = CompositionTestData.Definition();
        var snapshot = new AgentDefinitionSourceSnapshot(sourceId, version, 3, [definition]);
        snapshot.SourceId.ShouldBe(sourceId);
        snapshot.Version.ShouldBe(version);
        snapshot.Precedence.ShouldBe(3);
        snapshot.Definitions.ShouldBe([definition]);
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var definition = CompositionTestData.Definition();
        var left = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 0, [definition]);
        var right = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 0, [definition]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 0, [CompositionTestData.Definition()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
