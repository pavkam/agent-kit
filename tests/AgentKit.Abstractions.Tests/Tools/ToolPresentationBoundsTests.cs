// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolPresentationBounds behavior and contracts.</summary>
public sealed class ToolPresentationBoundsTests
{
    [Fact]
    public void Constructor_WhenMaximumInputBytesIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolPresentationBounds(maximumInputBytes: 0)).ParamName.ShouldBe("maximumInputBytes");

    [Fact]
    public void Constructor_WhenMaximumOutputCharactersIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolPresentationBounds(maximumOutputCharacters: 0)).ParamName.ShouldBe("maximumOutputCharacters");

    [Fact]
    public void Constructor_WhenMaximumPartsIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolPresentationBounds(maximumParts: 0)).ParamName.ShouldBe("maximumParts");

    [Fact]
    public void Constructor_WhenArgumentsOmitted_UsesDefaults()
    {
        var bounds = new ToolPresentationBounds();
        bounds.MaximumInputBytes.ShouldBe(262_144);
        bounds.MaximumOutputCharacters.ShouldBe(32_768);
        bounds.MaximumParts.ShouldBe(16);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var bounds = new ToolPresentationBounds(1, 2, 3);
        bounds.MaximumInputBytes.ShouldBe(1);
        bounds.MaximumOutputCharacters.ShouldBe(2);
        bounds.MaximumParts.ShouldBe(3);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolPresentationBounds(1, 2, 3);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
