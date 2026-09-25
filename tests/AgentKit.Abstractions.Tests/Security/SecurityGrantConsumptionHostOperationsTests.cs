// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

public sealed class SecurityGrantConsumptionHostOperationsTests
{
    [Fact]
    public void CreateGrantConsumptionIntentRecord_WhenValid_IncludesEnforcementMetadata()
    {
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            admissionId: null);
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            sessionId: null,
            correlation);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SecurityRequestId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            scope,
            new ExecutionIdentity(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Service,
                new AuthenticationEvidence(
                    new AuthenticationEvidenceId("test"),
                    new IdentityIssuerId("issuer"),
                    "test",
                    DateTimeOffset.UnixEpoch,
                    null,
                    new AuthenticationEvidenceFingerprint(new ContentHash("test"))),
                [],
                [],
                IdentityAssuranceLevel.Basic,
                new IdentityVersion(1)),
            new ComponentId("test"),
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://example.test")],
            new InputFingerprint("test"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(5),
            allowedUses: 1);
        var enforcement = new SecurityEnforcementRequest(
            grant.Scope,
            grant.Identity,
            grant.Audience,
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://example.test")],
            new InputFingerprint("test"),
            grant.RevocationVersion);
        var record = SecurityGrantConsumptionHostOperations.CreateGrantConsumptionIntentRecord(
            grant,
            enforcement,
            new SecurityAuditRecordId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        record.EventKind.ShouldBe(SecurityAuditEventKind.GrantConsumptionIntent);
        record.Outcome.ShouldBe(SecurityAuditOutcome.Accepted);
        record.Fields.ShouldContainKey("audience");
    }
}
