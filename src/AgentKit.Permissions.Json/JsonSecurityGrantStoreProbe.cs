// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist grant evidence.</summary>
/// <remarks>
/// Because a host may replace the serializer contract entirely, initialization encodes and decodes one synthetic record that
/// exercises every shape the store writes: a full grant with captured authorization, a delegated identity with claims, an
/// optional fencing token, a nullable instant, ordered resources, and every enumeration. Failing this check at bootstrap is
/// far safer than discovering an unusable contract while committing authority evidence.
/// </remarks>
internal static class JsonSecurityGrantStoreProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>A consumption record carrying a fully populated grant and enforcement receipt.</returns>
    /// <remarks>The value uses fixed identities and instants so the check never depends on a clock, a random source, or the host locale.</remarks>
    internal static JsonSecurityGrantLogRecord Create()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresAt = notBefore.AddHours(1);
        var scope = new SecurityAuthorizationScope(
            new AgentId(new Guid("11111111-1111-1111-1111-111111111111")),
            new SessionId(new Guid("22222222-2222-2222-2222-222222222222")),
            new InRunOperationCorrelation(
                new OperationId(new Guid("33333333-3333-3333-3333-333333333333")),
                new RunId(new Guid("44444444-4444-4444-4444-444444444444")),
                new TurnId(new Guid("55555555-5555-5555-5555-555555555555"))));
        var identity = new ExecutionIdentity(
            new TenantId("probe-tenant"),
            new PrincipalId("probe-principal"),
            ExecutionSubjectKind.Human,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("probe-evidence"),
                new IdentityIssuerId("probe-issuer"),
                "probe-method",
                notBefore,
                expiresAt,
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:probe"))),
            [new IdentityClaim(new IdentityIssuerId("probe-issuer"), "probe-type", "probe-value",
                IdentityClaimValueKind.Text)],
            [],
            IdentityAssuranceLevel.Strong,
            new IdentityVersion(1));
        var resources = ImmutableArray.Create(
            new ProtectedResource(ProtectedResourceKind.File, "/probe/resource"));
        var fingerprint = new InputFingerprint("sha256:probe-input");
        var grant = new SecurityGrant(
            new GrantId(new Guid("66666666-6666-6666-6666-666666666666")),
            new SecurityRequestId(new Guid("77777777-7777-7777-7777-777777777777")),
            scope,
            identity,
            new ComponentId("probe-audience"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            resources,
            fingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            notBefore,
            expiresAt,
            2);
        var enforcement = new SecurityEnforcementRequest(
            scope, identity, grant.Audience, grant.Kind, grant.Effect, resources, fingerprint,
            grant.RevocationVersion);
        var intent = new SecurityEnforcementIntent(
            new SecurityEnforcementIntentId(new Guid("88888888-8888-8888-8888-888888888888")),
            new FencingToken(1));
        var receipt = new SecurityEnforcementIntentReceipt(
            intent.Id, grant.Id, grant.RequestId, enforcement, intent.RequiredFence,
            SecurityEnforcementBinding.Fingerprint(enforcement, intent), notBefore);
        return JsonSecurityGrantLogRecord.ForConsumption(grant.Id, 1, receipt) with
        {
            Grant = JsonSecurityGrant.FromDomain(grant),
        };
    }
}
