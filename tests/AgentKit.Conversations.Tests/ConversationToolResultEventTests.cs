// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationToolResultEvent behavior and contracts.</summary>
public sealed class ConversationToolResultEventTests
{
    private static readonly ToolCallId CallId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenToolNameIsBlank_ThrowsArgumentException(string? toolName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationToolResultEvent(CallId, toolName!, true, "ok"));
        exception.ParamName.ShouldBe("toolName");
    }

    [Fact]
    public void Constructor_WhenSummaryIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationToolResultEvent(CallId, "tool", true, null!));
        exception.ParamName.ShouldBe("summary");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_SetsProperties()
    {
        var toolResultEvent = new ConversationToolResultEvent(CallId, "tool", false, "denied");

        toolResultEvent.CallId.ShouldBe(CallId);
        toolResultEvent.ToolName.ShouldBe("tool");
        toolResultEvent.Succeeded.ShouldBeFalse();
        toolResultEvent.Summary.ShouldBe("denied");
    }

    [Fact]
    public void Constructor_WhenCallIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ConversationToolResultEvent(default, "tool", true, "ok"));
        exception.ParamName.ShouldBe("callId");
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
