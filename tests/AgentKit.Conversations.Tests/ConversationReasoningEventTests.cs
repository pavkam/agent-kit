// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationReasoningEvent behavior and contracts.</summary>
public sealed class ConversationReasoningEventTests
{
    [Fact]
    public void Constructor_WhenTextIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationReasoningEvent(null!));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void Constructor_WhenTextIsNonNull_SetsText()
    {
        var reasoningEvent = new ConversationReasoningEvent("thinking it through");

        reasoningEvent.Text.ShouldBe("thinking it through");
    }

    [Fact]
    public void Equals_WhenTextMatches_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationReasoningEvent("thinking it through");
        var second = new ConversationReasoningEvent("thinking it through");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenTextDiffers_ReturnsFalse()
    {
        var first = new ConversationReasoningEvent("thinking it through");
        var second = new ConversationReasoningEvent("a different thought");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndText()
    {
        var reasoningEvent = new ConversationReasoningEvent("thinking it through");

        var text = reasoningEvent.ToString();

        text.ShouldContain(nameof(ConversationReasoningEvent));
        text.ShouldContain("thinking it through");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationReasoningEvent("thinking it through");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
