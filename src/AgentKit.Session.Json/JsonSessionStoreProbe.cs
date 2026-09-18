// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist session evidence.</summary>
/// <remarks>
/// Because a host may replace the serializer contract entirely, initialization encodes and decodes one synthetic record
/// that exercises every shape the store writes: validating identity structs, a polymorphic in-run correlation, a complete
/// execution identity with claims and authentication evidence, captured authorization, and a real session entry routed
/// through the portable codec catalog. Failing this check at bootstrap is far safer than discovering an unusable contract
/// while committing a session's authoritative history.
/// </remarks>
internal static class JsonSessionStoreProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>An append record carrying a fully populated context and one message entry.</returns>
    /// <remarks>The value uses fixed identities and instants so the check never depends on a clock, a random source, or the host locale.</remarks>
    internal static JsonSessionStoreLogRecord Create()
    {
        var instant = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var agentId = new AgentId(new Guid("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(new Guid("22222222-2222-2222-2222-222222222222"));
        var correlation = new InRunOperationCorrelation(
            new OperationId(new Guid("33333333-3333-3333-3333-333333333333")),
            new RunId(new Guid("44444444-4444-4444-4444-444444444444")),
            new TurnId(new Guid("55555555-5555-5555-5555-555555555555")));
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
        var branchId = new BranchId(new Guid("77777777-7777-7777-7777-777777777777"));
        var message = new UserMessage(
            new MessageId(new Guid("88888888-8888-8888-8888-888888888888")),
            agentId,
            sessionId,
            new ConversationId(new Guid("99999999-9999-9999-9999-999999999999")),
            branchId,
            correlation.RunId,
            correlation.TurnId,
            instant,
            MessageState.Complete,
            [new TextPart("probe", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var entry = new MessageSessionEntry(
            new SessionEntryId(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new SessionAddress(agentId, sessionId),
            correlation,
            branchId,
            new SessionSequence(1),
            null,
            instant,
            new SchemaVersion("1"),
            message);
        var request = new SessionAppendRequest(
            context, branchId, new SessionVersion(0), new IdempotencyKey("probe"), [entry]);
        return JsonSessionStoreLogRecord.ForAppend(request, instant);
    }
}
