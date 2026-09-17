// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationTurnCompletedEvent behavior and contracts.</summary>
public sealed class ConversationTurnCompletedEventTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenOutcomeIsBlank_ThrowsArgumentException(string? outcome)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationTurnCompletedEvent(true, outcome!));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_SetsProperties()
    {
        var turnCompletedEvent = new ConversationTurnCompletedEvent(true, "completed");

        turnCompletedEvent.Succeeded.ShouldBeTrue();
        turnCompletedEvent.Outcome.ShouldBe("completed");
    }

    [Fact]
    public void Equals_WhenSucceededAndOutcomeMatch_ReturnsTrueWithMatchingHashCode()
    {
        var first = new ConversationTurnCompletedEvent(true, "completed");
        var second = new ConversationTurnCompletedEvent(true, "completed");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenOutcomeDiffers_ReturnsFalse()
    {
        var first = new ConversationTurnCompletedEvent(true, "completed");
        var second = new ConversationTurnCompletedEvent(true, "cancelled");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndOutcome()
    {
        var turnCompletedEvent = new ConversationTurnCompletedEvent(true, "completed");

        var text = turnCompletedEvent.ToString();

        text.ShouldContain(nameof(ConversationTurnCompletedEvent));
        text.ShouldContain("completed");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationTurnCompletedEvent(true, "completed");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
