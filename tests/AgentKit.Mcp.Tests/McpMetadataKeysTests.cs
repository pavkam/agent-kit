// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;



/// <summary>Verifies McpMetadataKeys behavior and contracts.</summary>
public sealed class McpMetadataKeysTests
{
    [Fact]
    public void MetadataKey_WhenRead_IsNamespacedAndStable() => McpMetadataKeys.ToolContractVersion.ShouldBe("com.agentkit/toolContractVersion");
}
