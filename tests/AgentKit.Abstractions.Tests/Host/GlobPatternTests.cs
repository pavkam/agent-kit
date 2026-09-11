// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies GlobPattern behavior and contracts.</summary>
public sealed class GlobPatternTests: Conformance.StringIdentityConformanceTests<GlobPattern>
{
    [Theory]
    [InlineData("")]
    [InlineData("/src/*.cs")]
    [InlineData("src\\*.cs")]
    [InlineData("src//*.cs")]
    [InlineData("../*.cs")]
    [InlineData("src/**foo.cs")]
    [InlineData("src/[ab].cs")]
    public void GlobPattern_WhenSyntaxIsNotInPinnedDialect_ThrowsForValue(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new GlobPattern(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void GlobPattern_WhenSegmentCountExceedsComplexityBound_ThrowsForValue()
    {
        var value = string.Join('/', Enumerable.Repeat("x", GlobPattern.MaximumSegments + 1));
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new GlobPattern(value));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override GlobPattern Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(GlobPattern subject) => subject.Value;
}
