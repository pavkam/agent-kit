// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="McpCapabilityProfileSource"/> behavior.</summary>
public sealed class McpCapabilityProfileSourceTests
{
    [Fact]
    public void CapabilityId_WhenRead_IsTheMcpClientCapability() =>
        new McpCapabilityProfileSource([]).CapabilityId.ShouldBe(McpCapabilityIds.Client);

    [Fact]
    public void Contains_WhenProfileWasRegistered_ReturnsTrue()
    {
        var source = new McpCapabilityProfileSource([new McpCapabilityProfileRegistration(new CapabilityProfileId("docs"))]);

        source.Contains(new CapabilityProfileId("docs")).ShouldBeTrue();
        source.Contains(new CapabilityProfileId("other")).ShouldBeFalse();
    }

    [Fact]
    public void Contains_WhenProfileIdIsDefault_ThrowsExactParameter()
    {
        var source = new McpCapabilityProfileSource([]);

        Should.Throw<ArgumentException>(() => source.Contains(default)).ParamName.ShouldBe("profileId");
    }
}
