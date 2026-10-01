// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalQueryContent"/> constraints and rewrite provenance.</summary>
public sealed class RetrievalQueryContentTests
{
    private static readonly QueryRewriterReference _rewriter = new(new QueryRewriterKey("r"), new QueryRewriterVersion("1"));

    [Fact]
    public void Constructor_WhenOriginal_IsNotRewritten()
    {
        var content = new RetrievalQueryContent("find it");

        content.Text.ShouldBe("find it");
        content.OriginalText.ShouldBe("find it");
        content.RewrittenBy.ShouldBeNull();
        content.IsRewritten.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenTextIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalQueryContent(null!)).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenTextIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalQueryContent("  ")).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenTextExceedsTheMaximum_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalQueryContent(new string('q', RetrievalQueryContent.MaximumTextLength + 1))).ParamName.ShouldBe("text");

    [Fact]
    public void Rewrite_WhenRewritten_KeepsTheOriginalTextAndNamesTheRewriter()
    {
        var rewritten = new RetrievalQueryContent("find it").Rewrite("locate it", _rewriter);

        rewritten.Text.ShouldBe("locate it");
        rewritten.OriginalText.ShouldBe("find it");
        rewritten.RewrittenBy.ShouldBe(_rewriter);
        rewritten.IsRewritten.ShouldBeTrue();
    }

    [Fact]
    public void Rewrite_WhenRewrittenTwice_KeepsTheFirstOriginal() =>
        new RetrievalQueryContent("a").Rewrite("b", _rewriter).Rewrite("c", _rewriter).OriginalText.ShouldBe("a");

    [Fact]
    public void Rewrite_WhenArgumentsAreInvalid_Throws()
    {
        var original = new RetrievalQueryContent("a");

        Should.Throw<ArgumentNullException>(() => original.Rewrite("b", null!)).ParamName.ShouldBe("rewriter");
        Should.Throw<ArgumentException>(() => original.Rewrite(" ", _rewriter)).ParamName.ShouldBe("text");
    }
}
