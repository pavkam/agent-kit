// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="StdioMcpTransportFactory"/> blocked-open behavior.</summary>
public sealed class StdioMcpTransportFactoryTests
{
    [Fact]
    public async Task OpenAsync_WhenStdioProfileIsRequested_ReturnsExplicitTransportFailure()
    {
        var factory = new StdioMcpTransportFactory(
            processExecutors: null!,
            securityAuthorities: null!,
            grantStore: null!,
            audit: null!,
            securityRequestIds: null!,
            auditRecordIds: null!,
            timeProvider: TimeProvider.System);
        var endpoint = new McpEndpoint(
            new McpEndpointKey("local"),
            new McpEndpointRevision(1),
            new McpStdioTransportProfile("npx", ["-y", "example-mcp"]),
            authentication: null,
            new McpEndpointBounds(
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(10),
                maximumFrameBytes: 1_048_576,
                maximumMessageBytes: 4_194_304,
                maximumInFlightRequests: 16));
        var operation = CreateOperation();
        var profile = new McpCapabilityProfile(
            McpCapabilityIds.Client,
            new CapabilityProfileId("local"),
            new McpCapabilityProfileRevision(1),
            [endpoint.Key]);
        var sessionOpen = new McpClientOpenRequest(profile, endpoint, operation);
        var result = await factory.OpenAsync(
            new McpTransportOpenRequest(sessionOpen, endpoint),
            TestContext.Current.CancellationToken);
        var failure = result.ShouldBeOfType<McpTransportOpenFailed>();
        failure.SafeMessage.ShouldContain("stdin");
    }

    private static ProtectedSemanticOperationContext CreateOperation()
    {
        var agentId = new AgentId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            admissionId: null);
        return new ProtectedSemanticOperationContext(
            agentId,
            sessionId: null,
            conversationId: null,
            identity,
            correlation,
            TestSecurityEvidence.Authorization(agentId, sessionId: null, correlation, identity));
    }
}
