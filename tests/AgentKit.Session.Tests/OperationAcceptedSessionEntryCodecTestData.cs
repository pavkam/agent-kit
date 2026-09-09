// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

internal static class OperationAcceptedSessionEntryCodecTestData
{
    internal static OperationAcceptedSessionEntry Entry(
        string profileKey = "default",
        string configurationFingerprint = "sha256:configuration",
        int identityClaimCount = 1)
    {
        var address = new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3)));
        var correlation = new InRunOperationCorrelation(new OperationId(Id(4)), new RunId(Id(7)), new TurnId(Id(8)));
        var identity = Identity(identityClaimCount);
        var authorizationIdentity = Identity(identityClaimCount);
        var configuration = new RunConfigurationReference(new ConfigurationVersion(3), new RunPolicyVersion(4),
            new ContentHash(configurationFingerprint));
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("secure"),
            new SecurityProfileVersion(2),
            new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Id(40)),
                new SecurityPolicyVersion(5), new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("primary"), new AgentDefinitionRevision(6),
            configuration.ConfigurationVersion, new SecurityAuthorizationScope(address.AgentId, address.SessionId, correlation),
            authorizationIdentity);
        var entryId = new SessionEntryId(Id(30));
        var materializedEntries = ImmutableArray.Create(new SessionEntryId(Id(21)), new SessionEntryId(Id(22)));
        var acceptedAt = new DateTimeOffset(1970, 1, 1, 1, 30, 0, TimeSpan.FromMinutes(90));
        var state = new SessionAcceptedRunState(address, new ExecutionLaneId(Id(6)), new SessionLaneRevision(9), correlation,
            new OperationStateRevision(1), identity, authorization,
            new SessionProfileReference(new SessionProfileKey(profileKey), new SessionProfileVersion(2)), configuration,
            new SessionBranchCursor(new BranchId(Id(5)), new SessionEntryId(Id(20))),
            new SessionBranchCursor(new BranchId(Id(5)), entryId), new SessionSequence(10),
            new AdmissionId(Id(11)), [new AdmissionId(Id(11)), new AdmissionId(Id(12))], materializedEntries,
            [new MessageId(Id(31)), new MessageId(Id(32))], new TurnId(Id(8)), acceptedAt);
        return new OperationAcceptedSessionEntry(entryId, address, correlation, new BranchId(Id(5)),
            new SessionSequence(100), materializedEntries[^1], acceptedAt, new SchemaVersion("1"), state);
    }

    internal static bool Equivalent(OperationAcceptedSessionEntry expected, OperationAcceptedSessionEntry actual) =>
        expected.Id == actual.Id && expected.Address == actual.Address && expected.Correlation == actual.Correlation
        && expected.BranchId == actual.BranchId && expected.Sequence == actual.Sequence
        && expected.CausalParentId == actual.CausalParentId && expected.RecordedAt == actual.RecordedAt
        && expected.SchemaVersion == actual.SchemaVersion && Equivalent(expected.State, actual.State);

    private static bool Equivalent(SessionAcceptedRunState expected, SessionAcceptedRunState actual) =>
        expected.Address == actual.Address && expected.ExecutionLaneId == actual.ExecutionLaneId
        && expected.LaneRevision == actual.LaneRevision && expected.Correlation == actual.Correlation
        && expected.OperationStateRevision == actual.OperationStateRevision && expected.Identity == actual.Identity
        && expected.Authorization == actual.Authorization && expected.SessionProfile == actual.SessionProfile
        && expected.Configuration == actual.Configuration && expected.PreviousCursor == actual.PreviousCursor
        && expected.CommittedCursor == actual.CommittedCursor && expected.PromotionCutoff == actual.PromotionCutoff
        && expected.InitiatingAdmissionId == actual.InitiatingAdmissionId
        && expected.PromotedAdmissionIds.SequenceEqual(actual.PromotedAdmissionIds)
        && expected.MaterializedEntryIds.SequenceEqual(actual.MaterializedEntryIds)
        && expected.MaterializedMessageIds.SequenceEqual(actual.MaterializedMessageIds)
        && expected.InitialTurnId == actual.InitialTurnId && expected.AcceptedAt == actual.AcceptedAt
        && expected.State == actual.State;

    private static ExecutionIdentity Identity(int claimCount)
    {
        var claim = new IdentityClaim(new IdentityIssuerId("issuer"), "role", "operator", IdentityClaimValueKind.Text);
        var authenticatedAt = new DateTimeOffset(1970, 1, 1, 2, 0, 0, TimeSpan.FromHours(2));
        var evidence = new AuthenticationEvidence(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"),
            "mfa", authenticatedAt, authenticatedAt.AddHours(1),
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:evidence")));
        var link = new DelegationIdentityLink(new DelegationId(Id(50)), new TenantId("tenant"),
            new PrincipalId("parent"), new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("parent-evidence"),
            new IdentityVersion(1), authenticatedAt, [claim], IdentityAssuranceLevel.Basic);
        var claims = Enumerable.Repeat(claim, claimCount).ToImmutableArray();
        return new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human,
            evidence, claims, [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
    }

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
