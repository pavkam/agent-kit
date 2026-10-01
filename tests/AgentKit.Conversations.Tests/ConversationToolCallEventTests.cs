// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationToolCallEvent behavior and contracts.</summary>
public sealed class ConversationToolCallEventTests
{
    private static readonly ToolCallId CallId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenToolNameIsBlank_ThrowsArgumentException(string? toolName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationToolCallEvent(CallId, toolName!, "{}"));
        exception.ParamName.ShouldBe("toolName");
    }

    [Fact]
    public void Constructor_WhenArgumentsJsonIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationToolCallEvent(CallId, "tool", null!));
        exception.ParamName.ShouldBe("argumentsJson");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_SetsProperties()
    {
        var toolCallEvent = new ConversationToolCallEvent(CallId, "tool", /*lang=json,strict*/ "{\"a\":1}");

        toolCallEvent.CallId.ShouldBe(CallId);
        toolCallEvent.ToolName.ShouldBe("tool");
        toolCallEvent.ArgumentsJson.ShouldBe(/*lang=json,strict*/ "{\"a\":1}");
    }

    [Fact]
    public void Constructor_WhenCallIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ConversationToolCallEvent(default, "tool", "{}"));
        exception.ParamName.ShouldBe("callId");
    }

    [Fact]
    public void WithExpression_WhenCalled_ProducesAnEqualClone()
    {
        var original = new ConversationToolCallEvent(
            new ToolCallId(Guid.NewGuid()), "tool", /*lang=json,strict*/ "{\"a\":1}");

        var clone = original with { };

        clone.ShouldNotBeSameAs(original);
        clone.ShouldBe(original);
    }
}
