// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;



/// <summary>Verifies WebSearchItem behavior and contracts.</summary>
public sealed class WebSearchItemTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var url = new Uri("https://example.com/page");
        var publishedAt = DateTimeOffset.UnixEpoch;
        var item = new WebSearchItem("Title", url, "Snippet", publishedAt);
        item.Title.ShouldBe("Title");
        item.Url.ShouldBe(url);
        item.Snippet.ShouldBe("Snippet");
        item.PublishedAt.ShouldBe(publishedAt);
    }

    [Fact]
    public void Constructor_WhenPublishedAtIsNull_InitializesNull()
    {
        var item = new WebSearchItem("Title", new Uri("https://example.com/page"), "Snippet", null);
        item.PublishedAt.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenTitleIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchItem(" ", new Uri("https://example.com/page"), "Snippet", null));
        exception.ParamName.ShouldBe("title");
    }

    [Fact]
    public void Constructor_WhenUrlIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WebSearchItem("Title", null!, "Snippet", null));
        exception.ParamName.ShouldBe("url");
    }

    [Fact]
    public void Constructor_WhenUrlIsRelative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchItem("Title", new Uri("/relative", UriKind.Relative), "Snippet", null));
        exception.ParamName.ShouldBe("url");
    }

    [Fact]
    public void Constructor_WhenSnippetIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchItem("Title", new Uri("https://example.com/page"), " ", null));
        exception.ParamName.ShouldBe("snippet");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var url = new Uri("https://example.com/page");
        var first = new WebSearchItem("Title", url, "Snippet", null);
        var second = new WebSearchItem("Title", url, "Snippet", null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new WebSearchItem("Title", new Uri("https://example.com/page"), "Snippet", null);
        var copy = original with { Title = "Changed" };
        copy.Title.ShouldBe("Changed");
        original.Title.ShouldBe("Title");
    }
}
