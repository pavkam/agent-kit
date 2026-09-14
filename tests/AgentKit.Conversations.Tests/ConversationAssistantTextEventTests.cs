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
}
