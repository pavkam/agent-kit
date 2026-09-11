// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpToolContractMismatchException behavior and contracts.</summary>
public sealed class McpToolContractMismatchExceptionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ContractMismatchException_WhenMessageIsMissing_ThrowsForMessage(string? message)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolContractMismatchException(message!));
        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void ContractMismatchException_WhenMessageIsValid_PreservesMessage()
    {
        var exception = new McpToolContractMismatchException("catalog mismatch");
        exception.Message.ShouldBe("catalog mismatch");
    }
}
