// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Creates valid request evidence shared by authority tests without selecting a concrete grant-store adapter.</summary>
internal static class SecurityAuthorityTestData
{
    /// <summary>Creates a valid security request with deterministic identity, scope, and bounded expiry.</summary>
    /// <param name="now">The optional issue instant used to derive the exclusive request deadline.</param>
    /// <returns>One request whose resource and input values are valid but do not grant authority.</returns>
    internal static SecurityRequest CreateRequest(DateTimeOffset? now = null)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);
        return new SecurityRequest(
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            scope,
            null,
            identity,
            new ComponentId("filesystem"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:abc"),
            (now ?? DateTimeOffset.UnixEpoch).AddMinutes(10));
    }

    /// <summary>Creates a valid, non-expired approval request bound to a request from <see cref="CreateRequest"/>.</summary>
    /// <param name="now">The optional issue instant used to derive the request and its bounded expiry.</param>
    /// <returns>One approval request suitable for exercising approval-handler and broker behavior.</returns>
    internal static ApprovalRequest CreateApprovalRequest(DateTimeOffset? now = null)
    {
        var issued = now ?? DateTimeOffset.UnixEpoch;
        var securityRequest = CreateRequest(issued);
        var binding = new ApprovalScopeBinding(
            securityRequest,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            issued,
            issued.AddMinutes(5),
            1);
        return new ApprovalRequest(
            new ApprovalRequestId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
            binding,
            "Approve a bounded test operation.",
            issued);
    }
}
