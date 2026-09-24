// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpToolsCallRequest"/>.</summary>
public sealed class McpToolsCallRequestTests
{
    [Fact]
    public void Constructor_WhenToolNameIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolsCallRequest(
            new McpRequestId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            McpContractTestData.Operation(),
            new ToolCallId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            ""));

        exception.ParamName.ShouldBe("toolName");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        using var arguments = JsonDocument.Parse("""{"city":"Lisbon"}""");
        var request = new McpToolsCallRequest(
            new McpRequestId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            McpContractTestData.Operation(),
            new ToolCallId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            "weather.get",
            arguments);

        request.ToolName.ShouldBe("weather.get");
        request.Arguments.ShouldBeSameAs(arguments);
    }
}
