// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpEndpoint"/>.</summary>
public sealed class McpEndpointTests
{
    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new McpEndpoint(
            default,
            new McpEndpointRevision(1),
            McpContractTestData.Stdio(),
            authentication: null,
            McpContractTestData.Bounds()));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenRevisionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpEndpoint(
            new McpEndpointKey("docs"),
            default,
            McpContractTestData.Stdio(),
            authentication: null,
            McpContractTestData.Bounds()));
        exception.ParamName.ShouldBe("revision");
    }

    [Theory]
    [InlineData(0, "transport")]
    [InlineData(1, "bounds")]
    public void Constructor_WhenRequiredReferenceIsNull_ThrowsArgumentNullException(int invalidMember, string expectedParamName)
    {
        var exception = Should.Throw<ArgumentNullException>(() => new McpEndpoint(
            new McpEndpointKey("docs"),
            new McpEndpointRevision(1),
            invalidMember == 0 ? null! : McpContractTestData.Stdio(),
            authentication: null,
            invalidMember == 1 ? null! : McpContractTestData.Bounds()));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void Constructor_WhenValuesAreValid_PreservesTheCapturedEndpoint()
    {
        var authentication = new McpAuthenticationReference("oauth", "https://mcp.example");
        var endpoint = new McpEndpoint(
            new McpEndpointKey("docs"),
            new McpEndpointRevision(3),
            new McpHttpTransportProfile(new Uri("https://mcp.example/rpc")),
            authentication,
            McpContractTestData.Bounds());
        endpoint.Key.Value.ShouldBe("docs");
        endpoint.Revision.Value.ShouldBe(3);
        endpoint.Authentication.ShouldBe(authentication);
    }
}
