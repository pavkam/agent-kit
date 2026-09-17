// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationAssistantTextDeltaEvent behavior and contracts.</summary>
public sealed class ConversationAssistantTextDeltaEventTests
{
    [Fact]
    public void Constructor_WhenTextIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationAssistantTextDeltaEvent(null!));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void Constructor_WhenTextIsNonNull_SetsText()
    {
        var deltaEvent = new ConversationAssistantTextDeltaEvent("hello ");

        deltaEvent.Text.ShouldBe("hello ");
    }

    [Fact]
    public void Equals_WhenTextMatches_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationAssistantTextDeltaEvent("hello ");
        var second = new ConversationAssistantTextDeltaEvent("hello ");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenTextDiffers_ReturnsFalse()
    {
        var first = new ConversationAssistantTextDeltaEvent("hello ");
        var second = new ConversationAssistantTextDeltaEvent("goodbye ");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndText()
    {
        var deltaEvent = new ConversationAssistantTextDeltaEvent("hello ");

        var text = deltaEvent.ToString();

        text.ShouldContain(nameof(ConversationAssistantTextDeltaEvent));
        text.ShouldContain("hello");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationAssistantTextDeltaEvent("hello ");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
