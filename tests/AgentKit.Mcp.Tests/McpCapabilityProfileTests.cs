// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpCapabilityProfile"/>.</summary>
public sealed class McpCapabilityProfileTests
{
    [Fact]
    public void Constructor_WhenCapabilityIsNotTheMcpClient_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpCapabilityProfile(
            new CapabilityId("agentkit.other"),
            new CapabilityProfileId("local"),
            new McpCapabilityProfileRevision(1),
            []));
        exception.ParamName.ShouldBe("capabilityId");
    }

    [Fact]
    public void Constructor_WhenEndpointKeysAreDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpCapabilityProfile(
            McpCapabilityIds.Client,
            new CapabilityProfileId("local"),
            new McpCapabilityProfileRevision(1),
            default));
        exception.ParamName.ShouldBe("endpointKeys");
    }

    [Fact]
    public void Constructor_WhenEndpointKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpCapabilityProfile(
            McpCapabilityIds.Client,
            new CapabilityProfileId("local"),
            new McpCapabilityProfileRevision(1),
            [default]));
        exception.ParamName.ShouldBe("endpointKeys");
    }

    [Fact]
    public void Equals_WhenEndpointKeysAreSeparateArrays_ComparesTheSequence()
    {
        var first = McpContractTestData.Profile(new McpEndpointKey("a"), new McpEndpointKey("b"));
        var second = McpContractTestData.Profile(new McpEndpointKey("a"), new McpEndpointKey("b"));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(McpContractTestData.Profile(new McpEndpointKey("b"), new McpEndpointKey("a")));
    }
}
