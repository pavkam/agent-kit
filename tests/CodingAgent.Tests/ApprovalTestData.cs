// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

/// <summary>Creates deterministic approval values shared by the CodingAgent approval adapter fixtures.</summary>
internal static class ApprovalTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    internal static ExecutionIdentity Identity(string principal = "operator") => new(
        new TenantId("local"),
        new PrincipalId(principal),
        ExecutionSubjectKind.Human,
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("coding-agent-test"),
            new IdentityIssuerId("test"),
            "test-terminal",
            Now,
            null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:test-identity"))),
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));

    internal static ApprovalRequest Request(ExecutionIdentity identity)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
                null));
        var securityRequest = new SecurityRequest(
            new SecurityRequestId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            scope,
            new ToolCallId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            identity,
            new ComponentId("filesystem"),
            SecurityOperationKind.FileWrite,
            SecurityEffect.Replace,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:input"),
            Now.AddMinutes(5));
        var binding = new ApprovalScopeBinding(
            securityRequest,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            Now,
            Now.AddMinutes(5),
            1);
        return new ApprovalRequest(
            new ApprovalRequestId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            binding,
            "Approve an exact workspace edit.",
            Now);
    }
}
