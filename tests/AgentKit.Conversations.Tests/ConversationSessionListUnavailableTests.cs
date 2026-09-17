// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies content-safe unavailable session-discovery result construction.</summary>
public sealed class ConversationSessionListUnavailableTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationSessionListUnavailable(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsNonBlank_SetsSafeMessage()
    {
        var result = new ConversationSessionListUnavailable("discovery unavailable");

        result.SafeMessage.ShouldBe("discovery unavailable");
    }

    [Fact]
    public void Equals_WhenSafeMessageMatches_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationSessionListUnavailable("discovery unavailable");
        var second = new ConversationSessionListUnavailable("discovery unavailable");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenSafeMessageDiffers_ReturnsFalse()
    {
        var first = new ConversationSessionListUnavailable("discovery unavailable");
        var second = new ConversationSessionListUnavailable("a different reason");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndSafeMessage()
    {
        var result = new ConversationSessionListUnavailable("discovery unavailable");

        var text = result.ToString();

        text.ShouldContain(nameof(ConversationSessionListUnavailable));
        text.ShouldContain("discovery unavailable");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationSessionListUnavailable("discovery unavailable");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
