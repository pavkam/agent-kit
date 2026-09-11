// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpToolClientFactory behavior and contracts.</summary>
public sealed class McpToolClientFactoryTests
{
    [Fact]
    public void ToolClientFactory_WhenContractIsNull_ThrowsForContract()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new McpToolClientFactory<SimpleTools>(null!));
        exception.ParamName.ShouldBe("contract");
    }

    private abstract class SimpleTools
    {
        [McpTool("simple", "1")]
        public abstract Task<SimpleResponse> ExecuteAsync(SimpleRequest request);
    }

    private sealed record SimpleRequest(string Value);
    private sealed record SimpleResponse(string Value);
}
