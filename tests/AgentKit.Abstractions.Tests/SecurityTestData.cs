// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

internal static class SecurityTestData
{
    public static SecurityGrant Grant()
    {
        var __scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null));
        var __identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new(
        new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new SecurityRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
        __scope,
        __identity,
        TestSupport.TestSecurityEvidence.Authorization(__scope.AgentId, __scope.SessionId, __scope.Correlation, __identity),
        new ComponentId("test"),
        SecurityOperationKind.FileRead,
        SecurityEffect.Observe,
        [new ProtectedResource(ProtectedResourceKind.File, "a.txt")],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        1);
    }
}
