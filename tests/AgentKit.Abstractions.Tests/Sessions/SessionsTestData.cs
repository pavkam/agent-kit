// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit.TestSupport;

/// <summary>
/// Deterministic builders for session contract values shared across the
/// Sessions fixture files. Every identity is a fixed GUID so equality
/// assertions never depend on generation order.
/// </summary>
internal static class SessionsTestData
{
    public static AgentId AgentId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
    public static SessionId SessionId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000002"));
    public static BranchId BranchId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000003"));
    public static ExecutionLaneId LaneId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000004"));
    public static OperationId OperationId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000005"));
    public static RunId RunId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000006"));
    public static TurnId TurnId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000007"));
    public static SessionEntryId EntryId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000008"));
    public static AdmissionId AdmissionId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-000000000009"));

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static SessionAddress Address() => new(AgentId, SessionId);

    public static BeforeRunOperationCorrelation BeforeRun() => new(OperationId, null);

    public static InRunOperationCorrelation InRun() => new(OperationId, RunId, TurnId);

    public static SecurityAuthorizationContext Authorization(
        OperationCorrelation correlation,
        SessionId? sessionId = null,
        ConfigurationVersion? configurationVersion = null,
        ExecutionIdentity? identity = null) =>
        new(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            configurationVersion ?? new ConfigurationVersion(1),
            new SecurityAuthorizationScope(AgentId, sessionId, correlation),
            identity ?? Identity());

    public static SessionOperationContext BeforeRunContext(bool laneBound = true) =>
        new(AgentId, SessionId, laneBound ? LaneId : null, BeforeRun(), Identity(), Authorization(BeforeRun(), SessionId));

    public static SessionOperationContext InRunContext() =>
        new(AgentId, SessionId, LaneId, InRun(), Identity(), Authorization(InRun(), SessionId));

    public static SessionProfileReference ProfileReference() => new(new SessionProfileKey("profile"), new SessionProfileVersion(1));

    public static RunConfigurationReference Configuration(ConfigurationVersion? version = null) =>
        new(version ?? new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));

    public static SessionBranchCursor Cursor(SessionEntryId? lastEntryId = null) => new(BranchId, lastEntryId);

    public static SessionLocation Location(TenantId? tenantId = null, SessionStoreKey? storeKey = null, SessionAddress? address = null) =>
        new(address ?? Address(), tenantId ?? new TenantId("tenant"), storeKey ?? new SessionStoreKey("store"),
            new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));

    public static SessionCreateRequest CreateRequest() =>
        new(AgentId, Identity(), Authorization(BeforeRun(), null), null, new IdempotencyKey("create"), ExtensionData.Empty);

    public static SessionProfileSnapshot ProfileSnapshot(string storeKey = "test-store") =>
        TestSecurityEvidence.SessionProfile(storeKey);

    public static AgentInput Input() =>
        new(new InputId(Guid.Parse("a0000000-0000-0000-0000-00000000000b")), InputDelivery.FollowUp,
            [new TextPart("input", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

    public static InputPreprocessingManifest Preprocessing() =>
        new(new ConfigurationVersion(1), new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective"));

    public static AdmittedInput AdmittedInput() =>
        new(AdmissionId, AgentId, SessionId, LaneId, Identity(), new SessionSequence(1), Input(), Input(), Preprocessing(), DateTimeOffset.UnixEpoch);

    public static AdmissionReceipt Receipt(bool existing = true) =>
        new(AdmissionId, new InputId(Guid.Parse("a0000000-0000-0000-0000-00000000000d")), AgentId, SessionId, LaneId, new SessionSequence(1), existing);

    public static SecurityGrant Grant()
    {
        var correlation = BeforeRun();
        var authorization = Authorization(correlation, SessionId);
        return new SecurityGrant(
            new GrantId(Guid.Parse("a0000000-0000-0000-0000-00000000000e")),
            new SecurityRequestId(Guid.Parse("a0000000-0000-0000-0000-00000000000f")),
            authorization.Scope,
            Identity(),
            authorization,
            new ComponentId("session-directory"),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, "session:test")],
            new InputFingerprint("sha256:test"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            1);
    }

    public static SessionRunStartRequest RunStartRequest()
    {
        var context = BeforeRunContext();
        var promotionEntryId = new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-000000000014"));
        var materializedEntryId = new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-000000000015"));
        var acceptedEntryId = new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-000000000016"));
        var messageId = new MessageId(Guid.Parse("a0000000-0000-0000-0000-000000000017"));
        var configuration = Configuration();
        var inRunAuthorization = Authorization(InRun(), SessionId, configuration.ConfigurationVersion);
        return new SessionRunStartRequest(
            context, AdmissionId, [AdmissionId], new SessionSequence(1), new SessionLaneRevision(1),
            new SessionVersion(1), Cursor(), null, RunId, TurnId, promotionEntryId, [materializedEntryId],
            [messageId], acceptedEntryId, new OperationStateRevision(1), ProfileReference(), configuration,
            inRunAuthorization, DateTimeOffset.UnixEpoch, new IdempotencyKey("start"));
    }

    public static SessionAcceptedRunState AcceptedRunState()
    {
        var correlation = InRun();
        var authorization = Authorization(correlation, SessionId);
        var materializedEntryId = new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-000000000010"));
        var acceptedEntryId = new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-000000000011"));
        var previousEntryId = new SessionEntryId(Guid.Parse("a0000000-0000-0000-0000-000000000012"));
        var messageId = new MessageId(Guid.Parse("a0000000-0000-0000-0000-000000000013"));
        return new SessionAcceptedRunState(
            Address(), LaneId, new SessionLaneRevision(1), correlation, new OperationStateRevision(1), Identity(),
            authorization, ProfileReference(), Configuration(),
            new SessionBranchCursor(BranchId, previousEntryId),
            new SessionBranchCursor(BranchId, acceptedEntryId),
            new SessionSequence(1), AdmissionId, [AdmissionId], [materializedEntryId], [messageId], TurnId,
            DateTimeOffset.UnixEpoch);
    }
}
