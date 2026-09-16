// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolPresentationPart behavior and contracts.</summary>
public sealed class ToolPresentationPartTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolPresentationPart((ToolPresentationPartKind) 99, "text")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenTextIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolPresentationPart(ToolPresentationPartKind.Text, null!)).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenLanguageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolPresentationPart(ToolPresentationPartKind.Code, "text", language: " ")).ParamName.ShouldBe("language");

    [Fact]
    public void Constructor_WhenPathIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolPresentationPart(ToolPresentationPartKind.Diff, "text", path: " ")).ParamName.ShouldBe("path");

    [Fact]
    public void Constructor_WhenLanguageSuppliedForNonCodeKind_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolPresentationPart(ToolPresentationPartKind.Text, "text", language: "csharp")).ParamName.ShouldBe("language");

    [Fact]
    public void Constructor_WhenPathSuppliedForNonDiffKind_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolPresentationPart(ToolPresentationPartKind.Text, "text", path: "file.txt")).ParamName.ShouldBe("path");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var part = new ToolPresentationPart(ToolPresentationPartKind.Code, "text", "csharp", null);
        part.Kind.ShouldBe(ToolPresentationPartKind.Code);
        part.Text.ShouldBe("text");
        part.Language.ShouldBe("csharp");
        part.Path.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenDiffPathSupplied_RoundTripsPath()
    {
        var part = new ToolPresentationPart(ToolPresentationPartKind.Diff, "text", null, "file.txt");
        part.Path.ShouldBe("file.txt");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolPresentationPart(ToolPresentationPartKind.Text, "text");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
