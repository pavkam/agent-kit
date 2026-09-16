// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolPresentation behavior and contracts.</summary>
public sealed class ToolPresentationTests
{
    [Fact]
    public void Constructor_WhenPartsIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolPresentation(default, ToolPresentationDisposition.Formatted)).ParamName.ShouldBe("parts");

    [Fact]
    public void Constructor_WhenPartsContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolPresentation([null!], ToolPresentationDisposition.Formatted)).ParamName.ShouldBe("parts");

    [Fact]
    public void Constructor_WhenDispositionIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolPresentation([], (ToolPresentationDisposition) 99)).ParamName.ShouldBe("disposition");

    [Fact]
    public void Constructor_WhenOmittedCharactersIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolPresentation([], ToolPresentationDisposition.Formatted, -1)).ParamName.ShouldBe("omittedCharacters");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var part = new ToolPresentationPart(ToolPresentationPartKind.Text, "text");
        var presentation = new ToolPresentation([part], ToolPresentationDisposition.Truncated, 5);
        presentation.Parts.ShouldBe([part]);
        presentation.Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.OmittedCharacters.ShouldBe(5);
    }

    [Fact]
    public void Constructor_WhenOmittedCharactersOmitted_DefaultsToZero()
    {
        var presentation = new ToolPresentation([], ToolPresentationDisposition.Formatted);
        presentation.OmittedCharacters.ShouldBe(0);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolPresentation([], ToolPresentationDisposition.Formatted);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
