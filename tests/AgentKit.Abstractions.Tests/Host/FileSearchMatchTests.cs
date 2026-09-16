// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies FileSearchMatch behavior and contracts.</summary>
public sealed class FileSearchMatchTests
{
    [Fact]
    public void Constructor_WhenContentFingerprintIsBlank_ThrowsArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => Match(fingerprint: new ContentHash()));

    [Fact]
    public void Constructor_WhenLineNumberIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Match(lineNumber: 0)).ParamName.ShouldBe("lineNumber");

    [Fact]
    public void Constructor_WhenLineByteOffsetIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Match(lineByteOffset: -1)).ParamName.ShouldBe("lineByteOffset");

    [Fact]
    public void Constructor_WhenMatchByteOffsetIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Match(matchByteOffset: -1)).ParamName.ShouldBe("matchByteOffset");

    [Fact]
    public void Constructor_WhenMatchByteLengthIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Match(matchByteLength: -1)).ParamName.ShouldBe("matchByteLength");

    [Fact]
    public void Constructor_WhenLineProjectionByteOffsetIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Match(lineProjectionByteOffset: -1)).ParamName.ShouldBe("lineProjectionByteOffset");

    [Fact]
    public void Constructor_WhenLineTextIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Match(lineText: null!)).ParamName.ShouldBe("lineText");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var match = Match();
        match.Path.ShouldBe(new FileSystemPath("a.txt"));
        match.ContentFingerprint.ShouldBe(new ContentHash("sha256:content"));
        match.LineNumber.ShouldBe(1);
        match.LineByteOffset.ShouldBe(0);
        match.MatchByteOffset.ShouldBe(2);
        match.MatchByteLength.ShouldBe(4);
        match.LineProjectionByteOffset.ShouldBe(0);
        match.LineText.ShouldBe("line text");
        match.LineTextTruncated.ShouldBeFalse();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Match();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static FileSearchMatch Match(
        ContentHash? fingerprint = null,
        int lineNumber = 1,
        long lineByteOffset = 0,
        int matchByteOffset = 2,
        int matchByteLength = 4,
        int lineProjectionByteOffset = 0,
        string lineText = "line text") =>
        new(
            new FileSystemPath("a.txt"),
            fingerprint ?? new ContentHash("sha256:content"),
            lineNumber,
            lineByteOffset,
            matchByteOffset,
            matchByteLength,
            lineProjectionByteOffset,
            lineText,
            false);
}
