// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies ConversationToolCallEvent behavior and contracts.</summary>
public sealed class ConversationToolCallEventTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenToolNameIsBlank_ThrowsArgumentException(string? toolName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ConversationToolCallEvent(toolName!, "{}"));
        exception.ParamName.ShouldBe("toolName");
    }

    [Fact]
    public void Constructor_WhenArgumentsJsonIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ConversationToolCallEvent("tool", null!));
        exception.ParamName.ShouldBe("argumentsJson");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_SetsProperties()
    {
        var toolCallEvent = new ConversationToolCallEvent("tool", /*lang=json,strict*/ "{\"a\":1}");

        toolCallEvent.ToolName.ShouldBe("tool");
        toolCallEvent.ArgumentsJson.ShouldBe(/*lang=json,strict*/ "{\"a\":1}");
    }
}
