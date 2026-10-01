// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryContent"/> constraints and value semantics.</summary>
public sealed class MemoryContentTests
{
    [Fact]
    public void Constructor_WhenTextIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryContent(null!)).ParamName.ShouldBe("text");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenTextIsBlank_ThrowsArgumentException(string text) =>
        Should.Throw<ArgumentException>(() => new MemoryContent(text)).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenTextExceedsTheMaximum_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryContent(new string('x', MemoryContent.MaximumTextLength + 1))).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenTextIsExactlyTheMaximum_Accepts() =>
        new MemoryContent(new string('x', MemoryContent.MaximumTextLength)).Text.Length.ShouldBe(MemoryContent.MaximumTextLength);

    [Fact]
    public void Constructor_WhenExtensionsAreOmitted_UsesEmptyExtensions() =>
        new MemoryContent("text").Extensions.ShouldBe(ExtensionData.Empty);

    [Fact]
    public void Purged_WhenRead_NeverCarriesPurgedText() =>
        MemoryContent.Purged.Text.ShouldBe("[purged]");

    [Fact]
    public void Equality_WhenTextMatches_IsStructural() =>
        new MemoryContent("a").ShouldBe(new MemoryContent("a"));
}
