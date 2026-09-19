// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpCapabilityIds"/>.</summary>
public sealed class McpCapabilityIdsTests
{
    [Fact]
    public void Client_WhenReadTwice_ReturnsTheSameStableId()
    {
        McpCapabilityIds.Client.Value.ShouldBe("agentkit.mcp.client");
        McpCapabilityIds.Client.ShouldBe(new CapabilityId("agentkit.mcp.client"));
    }
}
