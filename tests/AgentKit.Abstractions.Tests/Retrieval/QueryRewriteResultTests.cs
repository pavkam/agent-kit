// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="QueryRewriteResult"/> factories.</summary>
public sealed class QueryRewriteResultTests
{
    private static readonly QueryRewriterReference _rewriter = new(new QueryRewriterKey("r"), new QueryRewriterVersion("1"));

    [Fact]
    public void Rewritten_WhenContentRecordsItsRewriter_ReportsRewritten()
    {
        var content = new RetrievalQueryContent("a").Rewrite("b", _rewriter);

        var result = QueryRewriteResult.Rewritten(content);

        result.IsRewritten.ShouldBeTrue();
        result.Content.ShouldBe(content);
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void Rewritten_WhenContentIsOriginal_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => QueryRewriteResult.Rewritten(new RetrievalQueryContent("a"))).ParamName.ShouldBe("content");

    [Fact]
    public void Rewritten_WhenContentIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => QueryRewriteResult.Rewritten(null!)).ParamName.ShouldBe("content");

    [Fact]
    public void Rejected_WhenMessageIsSupplied_CarriesNoContent()
    {
        var result = QueryRewriteResult.Rejected("refused");

        result.IsRewritten.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.SafeMessage.ShouldBe("refused");
        Should.Throw<ArgumentException>(() => QueryRewriteResult.Rejected(" ")).ParamName.ShouldBe("safeMessage");
    }
}
