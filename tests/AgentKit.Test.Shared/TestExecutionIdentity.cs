// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit;

/// <summary>Creates deterministic, explicitly evidenced execution identities for tests.</summary>
/// <remarks>This helper is test-only. It models a trusted test ingress and never participates in production composition.</remarks>
public static class TestExecutionIdentity
{
    /// <summary>Creates a deterministic identity with safe synthetic test evidence.</summary>
    /// <param name="tenantId">The test tenant.</param>
    /// <param name="principalId">The test principal.</param>
    /// <param name="subjectKind">The test subject kind.</param>
    /// <returns>An immutable identity carrying explicit test-only evidence and no claims or delegation ancestry.</returns>
    public static ExecutionIdentity Create(
        TenantId tenantId,
        PrincipalId principalId,
        ExecutionSubjectKind subjectKind) =>
        new(
            tenantId,
            principalId,
            subjectKind,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("test-evidence"),
                new IdentityIssuerId("test-issuer"),
                "test",
                DateTimeOffset.UnixEpoch,
                null,
                new AuthenticationEvidenceFingerprint(new ContentHash("test-fingerprint"))),
            [],
            [],
            IdentityAssuranceLevel.Basic,
            new IdentityVersion(1));
}
