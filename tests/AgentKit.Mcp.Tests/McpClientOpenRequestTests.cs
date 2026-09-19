// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpClientOpenRequest"/>.</summary>
public sealed class McpClientOpenRequestTests
{
    [Fact]
    public void Constructor_WhenEndpointIsNotListed_ThrowsArgumentException()
    {
        var profile = McpContractTestData.Profile(new McpEndpointKey("listed"));
        var endpoint = McpContractTestData.Endpoint(new McpEndpointKey("other"));
        var exception = Should.Throw<ArgumentException>(() =>
            new McpClientOpenRequest(profile, endpoint, McpContractTestData.Operation()));
        exception.ParamName.ShouldBe("endpoint");
    }

    [Theory]
    [InlineData(0, "capabilityProfile")]
    [InlineData(1, "endpoint")]
    [InlineData(2, "operation")]
    public void Constructor_WhenRequiredReferenceIsNull_ThrowsArgumentNullException(int invalidMember, string expectedParamName)
    {
        var endpoint = McpContractTestData.Endpoint();
        var profile = McpContractTestData.Profile(endpoint.Key);
        var exception = Should.Throw<ArgumentNullException>(() => new McpClientOpenRequest(
            invalidMember == 0 ? null! : profile,
            invalidMember == 1 ? null! : endpoint,
            invalidMember == 2 ? null! : McpContractTestData.Operation()));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void Constructor_WhenEndpointIsListed_CapturesTheOpenRequest()
    {
        var endpoint = McpContractTestData.Endpoint();
        var profile = McpContractTestData.Profile(endpoint.Key);
        var operation = McpContractTestData.Operation();
        var request = new McpClientOpenRequest(profile, endpoint, operation);
        request.CapabilityProfile.ShouldBe(profile);
        request.Endpoint.ShouldBe(endpoint);
        request.Operation.ShouldBeSameAs(operation);
    }
}
