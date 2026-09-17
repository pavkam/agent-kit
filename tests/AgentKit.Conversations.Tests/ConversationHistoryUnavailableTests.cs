// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies content-safe unavailable history result construction.</summary>
public sealed class ConversationHistoryUnavailableTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationHistoryUnavailable(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Equals_WhenSafeMessageMatches_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationHistoryUnavailable("unavailable");
        var second = new ConversationHistoryUnavailable("unavailable");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenSafeMessageDiffers_ReturnsFalse()
    {
        var first = new ConversationHistoryUnavailable("unavailable");
        var second = new ConversationHistoryUnavailable("a different reason");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndSafeMessage()
    {
        var result = new ConversationHistoryUnavailable("unavailable");

        var text = result.ToString();

        text.ShouldContain(nameof(ConversationHistoryUnavailable));
        text.ShouldContain("unavailable");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationHistoryUnavailable("unavailable");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
