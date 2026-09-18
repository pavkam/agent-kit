// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web.Tests;

public sealed class WebContentProjectorTests
{
    [Fact]
    public void Project_WhenUtf8Invalid_ThrowsTypedContentFailure()
    {
        var action = () => WebContentProjector.Project([0xff], "text/plain; charset=utf-8", 100);

        _ = action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Project_WhenBinaryNulPresent_RejectsBeforeDecoding()
    {
        var action = () => WebContentProjector.Project([0x41, 0x00, 0x42], "text/plain", 100);

        _ = action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Project_WhenBomPresent_UsesBomAndRemovesItFromText()
    {
        var projection = WebContentProjector.Project(
            [0xef, 0xbb, 0xbf, 0x68, 0x69],
            "text/plain",
            100);

        projection.Text.ShouldBe("hi");
        projection.Encoding.ShouldBe("utf-8");
    }

    [Fact]
    public void Project_WhenMaximumCharactersNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => WebContentProjector.Project([0x41], "text/plain", 0));

    [Fact]
    public void Project_WhenTruncationBoundaryLandsInsideASurrogatePair_BacksOffInsteadOfEmittingALoneSurrogate()
    {
        // text[..maximumCharacters] slices on UTF-16 code units. "hi\U0001F600" is ['h','i',HighSurrogate,
        // LowSurrogate] (4 code units); a maximumCharacters of 3 lands exactly between the high and low
        // surrogate. Cutting there must back off to 2 instead of emitting a lone high surrogate that
        // JsonSerializer would render as replacement or escaped garbage.
        var bytes = Encoding.UTF8.GetBytes("hi\U0001F600");

        var projection = WebContentProjector.Project(bytes, "text/plain; charset=utf-8", 3);

        projection.Text.ShouldBe("hi");
        projection.Truncated.ShouldBeTrue();
    }

    [Fact]
    public void Project_WhenMediaTypeUnsupported_ThrowsTypedContentFailure()
    {
        var action = () => WebContentProjector.Project(Encoding.UTF8.GetBytes("binary-ish"), "image/png", 100);

        action.ShouldThrow<InvalidDataException>().Message.ShouldContain("image/png");
    }

    [Fact]
    public void Project_WhenContentTypeMissing_SniffsPlainTextByDefault()
    {
        var projection = WebContentProjector.Project(Encoding.UTF8.GetBytes("hello"), null, 100);

        projection.DeclaredMediaType.ShouldBeNull();
        projection.MediaType.ShouldBe("text/plain");
        projection.Text.ShouldBe("hello");
    }

    [Fact]
    public void Project_WhenContentTypeIsWhitespace_SniffsPlainTextByDefault()
    {
        var projection = WebContentProjector.Project(Encoding.UTF8.GetBytes("hello"), "   ", 100);

        projection.DeclaredMediaType.ShouldBeNull();
        projection.MediaType.ShouldBe("text/plain");
    }

    [Fact]
    public void Project_WhenContentTypeHeaderIsMalformed_ThrowsTypedContentFailure()
    {
        var action = () => WebContentProjector.Project(Encoding.UTF8.GetBytes("hello"), "\"", 100);

        action.ShouldThrow<InvalidDataException>().Message.ShouldContain("Content-Type");
    }

    [Fact]
    public void Project_WhenContentTypeMissingButBodySniffsAsHtml_UsesHtmlToTextTransform()
    {
        var projection = WebContentProjector.Project(Encoding.UTF8.GetBytes("<html><body>hi</body></html>"), null, 100);

        projection.SniffedMediaType.ShouldBe("text/html");
        projection.MediaType.ShouldBe("text/html");
        projection.Transform.ShouldBe("html_to_text_v1");
    }

    [Fact]
    public void Project_WhenContentTypeMissingButBodySniffsAsJson_ReportsSniffedMediaType()
    {
        var projection = WebContentProjector.Project(Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"a\":1}"), null, 100);

        projection.SniffedMediaType.ShouldBe("application/json");
        projection.MediaType.ShouldBe("application/json");
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-16le")]
    [InlineData("unicode")]
    public void Project_WhenLittleEndianUtf16CharsetDeclared_DecodesText(string charset)
    {
        byte[] bytes = [0x68, 0x00, 0x69, 0x00];

        var projection = WebContentProjector.Project(bytes, $"text/plain; charset={charset}", 100);

        projection.Text.ShouldBe("hi");
    }

    [Fact]
    public void Project_WhenUtf16BigEndianCharsetDeclared_DecodesText()
    {
        byte[] bytes = [0x00, 0x68, 0x00, 0x69];

        var projection = WebContentProjector.Project(bytes, "text/plain; charset=utf-16be", 100);

        projection.Text.ShouldBe("hi");
    }

    [Theory]
    [InlineData("iso-8859-1")]
    [InlineData("latin1")]
    public void Project_WhenLatin1CharsetDeclared_DecodesText(string charset)
    {
        var projection = WebContentProjector.Project([0x68, 0x69], $"text/plain; charset={charset}", 100);

        projection.Text.ShouldBe("hi");
    }

    [Theory]
    [InlineData("us-ascii")]
    [InlineData("ascii")]
    public void Project_WhenAsciiCharsetDeclared_DecodesText(string charset)
    {
        var projection = WebContentProjector.Project([0x68, 0x69], $"text/plain; charset={charset}", 100);

        projection.Text.ShouldBe("hi");
    }

    [Fact]
    public void Project_WhenCharsetUnsupported_ThrowsTypedContentFailure()
    {
        var action = () => WebContentProjector.Project([0x68], "text/plain; charset=shift-jis", 100);

        action.ShouldThrow<InvalidDataException>().Message.ShouldContain("shift-jis");
    }

    [Fact]
    public void Project_WhenUtf16LeBomPresent_RemovesBomFromText()
    {
        byte[] bytes = [0xFF, 0xFE, 0x68, 0x00, 0x69, 0x00];

        var projection = WebContentProjector.Project(bytes, "text/plain", 100);

        projection.Text.ShouldBe("hi");
    }

    [Fact]
    public void Project_WhenUtf16BeBomPresent_RemovesBomFromText()
    {
        byte[] bytes = [0xFE, 0xFF, 0x00, 0x68, 0x00, 0x69];

        var projection = WebContentProjector.Project(bytes, "text/plain", 100);

        projection.Text.ShouldBe("hi");
    }

    [Fact]
    public void Project_WhenHtmlTagIsUnclosed_AppendsTheRemainderVerbatimAndStops()
    {
        var projection = WebContentProjector.Project(Encoding.UTF8.GetBytes("<html><body>before<div"), "text/html", 100);

        projection.Text.ShouldBe("before<div");
    }

    [Fact]
    public void Project_WhenTrailingSpacesPrecedeNewline_TrimsThemBeforeInsertingTheNewline()
    {
        var projection = WebContentProjector.Project(Encoding.UTF8.GetBytes("<html><body>line1   <br>line2</body></html>"), "text/html", 200);

        projection.Text.ShouldBe("line1\nline2");
    }

    [Fact]
    public void Project_WhenCalledTwiceWithEquivalentInput_ProducesEqualProjections()
    {
        var first = WebContentProjector.Project(Encoding.UTF8.GetBytes("hello"), "text/plain", 100);
        var second = WebContentProjector.Project(Encoding.UTF8.GetBytes("hello"), "text/plain", 100);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
