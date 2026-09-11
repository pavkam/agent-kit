// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpToolInvocationException behavior and contracts.</summary>
public sealed class McpToolInvocationExceptionTests
{
    [Fact]
    public void ToolInvocationException_WhenToolNameIsUninitialized_ThrowsForToolName()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolInvocationException(default, "failed"));
        exception.ParamName.ShouldBe("toolName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToolInvocationException_WhenMessageIsMissing_ThrowsForMessage(string? message)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolInvocationException(new McpToolName("tool"), message!));
        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void ToolInvocationException_WhenValuesAreValid_PreservesToolAndMessage()
    {
        var exception = new McpToolInvocationException(new McpToolName("tool"), "failed");
        exception.ToolName.ShouldBe(new McpToolName("tool"));
        exception.Message.ShouldBe("failed");
    }
}
