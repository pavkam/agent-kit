// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist decision evidence.</summary>
internal static class JsonSecurityDecisionStoreProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>A record carrying one fully populated allowed decision.</returns>
    internal static JsonSecurityDecisionLogRecord Create()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var scope = new SecurityAuthorizationScope(
            new AgentId(new Guid("11111111-1111-1111-1111-111111111111")),
            new SessionId(new Guid("22222222-2222-2222-2222-222222222222")),
            new InRunOperationCorrelation(
                new OperationId(new Guid("33333333-3333-3333-3333-333333333333")),
                new RunId(new Guid("44444444-4444-4444-4444-444444444444")),
                null));
        var identity = new ExecutionIdentity(
            new TenantId("probe-tenant"),
            new PrincipalId("probe-principal"),
            ExecutionSubjectKind.Human,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("probe-evidence"),
                new IdentityIssuerId("probe-issuer"),
                "probe-method",
                now,
                now.AddHours(1),
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:probe"))),
            [new IdentityClaim(new IdentityIssuerId("probe-issuer"), "probe-claim", "value", IdentityClaimValueKind.Text)],
            [],
            IdentityAssuranceLevel.Strong,
            new IdentityVersion(1));
        var grant = new SecurityGrant(
            new GrantId(new Guid("55555555-5555-5555-5555-555555555555")),
            new SecurityRequestId(new Guid("66666666-6666-6666-6666-666666666666")),
            scope,
            identity,
            new ComponentId("probe-audience"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.File, "/probe/file.txt")],
            new InputFingerprint("sha256:probe-input"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            now,
            now.AddMinutes(30),
            1);
        var allowed = new SecurityAllowed(grant.RequestId, new SecurityPolicyVersion(1), grant);
        return JsonSecurityDecisionLogRecord.ForRecorded(allowed);
    }
}
