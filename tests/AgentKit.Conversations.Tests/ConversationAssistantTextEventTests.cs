// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationAssistantTextEvent behavior and contracts.</summary>
public sealed class ConversationAssistantTextEventTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenTextIsBlank_ThrowsArgumentException(string? text)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationAssistantTextEvent(text!));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void Constructor_WhenTextIsNonBlank_SetsText()
    {
        var assistantTextEvent = new ConversationAssistantTextEvent("hello");

        assistantTextEvent.Text.ShouldBe("hello");
    }

    [Fact]
    public void Equals_WhenTextMatches_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationAssistantTextEvent("hello");
        var second = new ConversationAssistantTextEvent("hello");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenTextDiffers_ReturnsFalse()
    {
        var first = new ConversationAssistantTextEvent("hello");
        var second = new ConversationAssistantTextEvent("goodbye");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndText()
    {
        var assistantTextEvent = new ConversationAssistantTextEvent("hello");

        var text = assistantTextEvent.ToString();

        text.ShouldContain(nameof(ConversationAssistantTextEvent));
        text.ShouldContain("hello");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationAssistantTextEvent("hello");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
