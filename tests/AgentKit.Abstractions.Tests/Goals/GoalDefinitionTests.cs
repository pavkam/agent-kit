// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalDefinition constraints and value semantics.</summary>
public sealed class GoalDefinitionTests
{
    [Fact]
    public void Constructor_WhenObjectiveIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new GoalDefinition(" ", [], ExtensionData.Empty)).ParamName.ShouldBe("objective");

    [Fact]
    public void Constructor_WhenObjectiveIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new GoalDefinition(null!, [], ExtensionData.Empty)).ParamName.ShouldBe("objective");

    [Fact]
    public void Constructor_WhenReferencesAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new GoalDefinition("o", default, ExtensionData.Empty)).ParamName.ShouldBe("contextReferences");

    [Fact]
    public void Constructor_WhenAReferenceIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new GoalDefinition("o", ["ok", " "], ExtensionData.Empty)).ParamName.ShouldBe("contextReferences");

    [Fact]
    public void Constructor_WhenExtensionsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new GoalDefinition("o", [], null!)).ParamName.ShouldBe("extensions");

    [Fact]
    public void Equality_WhenReferencesMatchByContent_IsStructural() =>
        new GoalDefinition("o", ["a", "b"], ExtensionData.Empty).ShouldBe(new GoalDefinition("o", ["a", "b"], ExtensionData.Empty));

    [Fact]
    public void Equality_WhenReferencesDiffer_IsNotEqual() =>
        new GoalDefinition("o", ["a"], ExtensionData.Empty).ShouldNotBe(new GoalDefinition("o", ["b"], ExtensionData.Empty));
}
