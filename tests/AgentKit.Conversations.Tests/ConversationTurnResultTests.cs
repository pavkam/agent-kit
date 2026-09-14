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
}
