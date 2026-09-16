// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;



/// <summary>Verifies WebSearchSucceeded behavior and contracts.</summary>
public sealed class WebSearchSucceededTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = RequestId();
        var items = Items();
        var succeeded = new WebSearchSucceeded(id, items, true);
        succeeded.RequestId.ShouldBe(id);
        succeeded.Items.ShouldBe(items);
        succeeded.Complete.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenItemsContainNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchSucceeded(RequestId(), [null!], true));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var items = Items();
        var first = new WebSearchSucceeded(RequestId(), items, true);
        var second = new WebSearchSucceeded(RequestId(), items, true);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenItemsDiffer_IsNotEqual()
    {
        var first = new WebSearchSucceeded(RequestId(), Items(), true);
        var second = new WebSearchSucceeded(RequestId(), [], true);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new WebSearchSucceeded(RequestId(), Items(), true);
        var copy = original with { Complete = false };
        copy.Complete.ShouldBeFalse();
        original.Complete.ShouldBeTrue();
    }

    private static WebSearchRequestId RequestId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static ImmutableArray<WebSearchItem> Items() => [new("Title", new Uri("https://example.com/page"), "Snippet", null)];
}
