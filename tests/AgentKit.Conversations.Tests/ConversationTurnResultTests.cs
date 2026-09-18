// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationTurnResult behavior and contracts.</summary>
public sealed class ConversationTurnResultTests
{
    [Fact]
    public void Constructor_WhenEventsIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationTurnResult(true, default));
        exception.ParamName.ShouldBe("events");
    }

    [Fact]
    public void Constructor_WhenEventsIsEmpty_SucceedsWithEmptyEvents()
    {
        var result = new ConversationTurnResult(true, []);

        result.Succeeded.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenSucceededIsFalse_PreservesSuppliedEvents()
    {
        var events = ImmutableArray.Create<ConversationEvent>(new ConversationAssistantTextEvent("hi"));

        var result = new ConversationTurnResult(false, events);

        result.Succeeded.ShouldBeFalse();
        result.Events.ShouldBe(events);
    }

    [Fact]
    public void Constructor_WhenIdentitiesAreNotSupplied_LeavesSessionIdAndRunIdNull()
    {
        var result = new ConversationTurnResult(true, []);

        result.SessionId.ShouldBeNull();
        result.RunId.ShouldBeNull();
    }

    [Fact]
    public void Init_WhenIdentitiesAreSupplied_PreservesThemAndParticipatesInEquality()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());

        var result = new ConversationTurnResult(true, []) { SessionId = sessionId, RunId = runId };
        var same = new ConversationTurnResult(true, []) { SessionId = sessionId, RunId = runId };
        var other = new ConversationTurnResult(true, []) { SessionId = sessionId, RunId = new RunId(Guid.NewGuid()) };

        result.SessionId.ShouldBe(sessionId);
        result.RunId.ShouldBe(runId);
        result.ShouldBe(same);
        result.ShouldNotBe(other);
    }

    [Fact]
    public void Equals_WhenSucceededAndEventsMatch_ReturnsTrueWithMatchingHashCode()
    {
        var events = ImmutableArray.Create<ConversationEvent>(new ConversationAssistantTextEvent("hi"));
        var first = new ConversationTurnResult(true, events);
        var second = new ConversationTurnResult(true, events);

        first.Equals(second).ShouldBeTrue();
        first.Equals((object) second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenSucceededDiffers_ReturnsFalse()
    {
        var events = ImmutableArray.Create<ConversationEvent>(new ConversationAssistantTextEvent("hi"));
        var first = new ConversationTurnResult(true, events);
        var second = new ConversationTurnResult(false, events);

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void ToString_WhenCalled_IncludesTypeNameAndSucceeded()
    {
        var result = new ConversationTurnResult(true, []);

        var text = result.ToString();

        text.ShouldContain(nameof(ConversationTurnResult));
        text.ShouldContain("Succeeded");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationTurnResult(true, []);

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
