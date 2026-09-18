// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist routing evidence.</summary>
/// <remarks>
/// The probe exercises every shape a routing record carries: validating identity structs, a polymorphic before-run
/// correlation, a complete execution identity with claims and authentication evidence, captured authorization, and a
/// committed location. Failing this check at bootstrap is far safer than discovering an unusable contract while committing
/// the route that decides which store owns a session.
/// </remarks>
internal static class JsonSessionDirectoryProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>A route-write record carrying a fully populated context and location.</returns>
    /// <remarks>The value uses fixed identities and instants so the check never depends on a clock, a random source, or the host locale.</remarks>
    internal static JsonSessionDirectoryLogRecord Create()
    {
        var instant = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var agentId = new AgentId(new Guid("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(new Guid("22222222-2222-2222-2222-222222222222"));
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(new Guid("33333333-3333-3333-3333-333333333333")), null);
        var identity = new ExecutionIdentity(
            new TenantId("probe-tenant"),
            new PrincipalId("probe-principal"),
            ExecutionSubjectKind.Human,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("probe-evidence"),
                new IdentityIssuerId("probe-issuer"),
                "probe-method",
                instant,
                instant.AddHours(1),
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:probe"))),
            [new IdentityClaim(
                new IdentityIssuerId("probe-issuer"), "probe-type", "probe-value", IdentityClaimValueKind.Text)],
            [],
            IdentityAssuranceLevel.Strong,
            new IdentityVersion(1));
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("probe"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(new Guid("66666666-6666-6666-6666-666666666666")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:probe-policy")),
            new ComponentKey<ISecurityAuthority>("probe"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
        var context = new SessionOperationContext(agentId, sessionId, null, correlation, identity, authorization);
        var location = new SessionLocation(
            new SessionAddress(agentId, sessionId),
            identity.TenantId,
            new SessionStoreKey("agentkit.json"),
            new SessionDirectoryRevision(1),
            instant,
            new SchemaVersion("1"));
        return JsonSessionDirectoryLogRecord.ForWrite(
            new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("probe")));
    }
}
