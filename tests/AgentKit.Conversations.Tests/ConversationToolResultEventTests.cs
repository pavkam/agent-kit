// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationToolResultEvent behavior and contracts.</summary>
public sealed class ConversationToolResultEventTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenToolNameIsBlank_ThrowsArgumentException(string? toolName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationToolResultEvent(toolName!, true, "ok"));
        exception.ParamName.ShouldBe("toolName");
    }

    [Fact]
    public void Constructor_WhenSummaryIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationToolResultEvent("tool", true, null!));
        exception.ParamName.ShouldBe("summary");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_SetsProperties()
    {
        var toolResultEvent = new ConversationToolResultEvent("tool", false, "denied");

        toolResultEvent.ToolName.ShouldBe("tool");
        toolResultEvent.Succeeded.ShouldBeFalse();
        toolResultEvent.Summary.ShouldBe("denied");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationToolResultEvent(new ToolCallId(Guid.NewGuid()), "tool", true, "ok");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
