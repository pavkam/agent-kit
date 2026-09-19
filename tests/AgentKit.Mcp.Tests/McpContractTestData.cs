// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Builds internally consistent MCP values for contract tests.</summary>
internal static class McpContractTestData
{
    internal static McpEndpointBounds Bounds() => new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(10),
        1_048_576,
        4_194_304,
        16);

    internal static McpStdioTransportProfile Stdio() =>
        new("mcp-server", ["--stdio"]);

    internal static McpEndpoint Endpoint(McpEndpointKey? key = null) => new(
        key ?? new McpEndpointKey("docs"),
        new McpEndpointRevision(1),
        Stdio(),
        authentication: null,
        Bounds());

    internal static McpCapabilityProfile Profile(params McpEndpointKey[] keys) => new(
        McpCapabilityIds.Client,
        new CapabilityProfileId("local"),
        new McpCapabilityProfileRevision(1),
        keys.Length == 0 ? [new McpEndpointKey("docs")] : [.. keys]);

    internal static ProtectedSemanticOperationContext Operation()
    {
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
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
