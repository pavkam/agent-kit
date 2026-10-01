// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies the default rewriter's identity-preserving behavior.</summary>
public sealed class NoRewriteQueryRewriterTests
{
    [Fact]
    public async Task RewriteAsync_WhenQueryIsValid_ReturnsTheSameTextAttributedToTheRewriter()
    {
        var rewriter = new NoRewriteQueryRewriter();
        var query = MemoryTestData.Query(MemoryTestData.NewOwner(), "original words");

        var result = await rewriter.RewriteAsync(query, TestContext.Current.CancellationToken);

        result.IsRewritten.ShouldBeTrue();
        result.Content.Text.ShouldBe("original words");
        result.Content.OriginalText.ShouldBe("original words");
        _ = result.Content.RewrittenBy.ShouldNotBeNull();
        result.Content.RewrittenBy.Matches(rewriter.Descriptor).ShouldBeTrue();
    }

    [Fact]
    public async Task RewriteAsync_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await new NoRewriteQueryRewriter().RewriteAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public async Task RewriteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var query = MemoryTestData.Query(MemoryTestData.NewOwner());

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await new NoRewriteQueryRewriter().RewriteAsync(query, source.Token));
    }
}
