// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

/// <summary>Builds synthetic protected operation contexts for inbound MCP peer requests.</summary>
internal static class McpServerOperationFactory
{
    internal static ProtectedSemanticOperationContext CreateDefault()
    {
        var agentId = new AgentId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var sessionId = new SessionId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        var runId = new RunId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var identity = new ExecutionIdentity(
            new TenantId("mcp-server"),
            new PrincipalId("peer"),
            ExecutionSubjectKind.Service,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("mcp-server-evidence"),
                new IdentityIssuerId("mcp-server"),
                "mcp-server",
                DateTimeOffset.UnixEpoch,
                null,
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:mcp-server-peer"))),
            [],
            [],
            IdentityAssuranceLevel.Basic,
            new IdentityVersion(1));
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")),
            runId,
            new TurnId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        var policySnapshot = new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.NewGuid()),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:mcp-server-host"));
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("mcp-server"),
            new SecurityProfileVersion(1),
            policySnapshot,
            new ComponentKey<ISecurityAuthority>("mcp-server"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
        return new ProtectedSemanticOperationContext(
            agentId,
            sessionId,
            conversationId: null,
            identity,
            correlation,
            authorization);
    }
}
