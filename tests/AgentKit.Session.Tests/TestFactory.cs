// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

internal static class TestFactory
{
    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    public static SessionCreateRequest CreateRequest(AgentId? agentId = null, IdempotencyKey? idempotencyKey = null)
    {
        var selectedAgentId = agentId ?? new AgentId(Guid.NewGuid());
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), admissionId: null);
        return new SessionCreateRequest(selectedAgentId, identity,
            Authorization(selectedAgentId, null, correlation, identity), null,
            idempotencyKey ?? new IdempotencyKey(Guid.NewGuid().ToString()), ExtensionData.Empty);
    }

    public static SessionDescriptor Descriptor(SessionAddress? address = null, BranchId? branchId = null, long version = 0) => new(
        address ?? new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())),
        null,
        new TenantId("tenant-1"),
        new PrincipalId("user-1"),
        new SessionStoreKey("fake"),
        branchId ?? new BranchId(Guid.NewGuid()),
        new SessionVersion(version),
        SessionLifecycleState.Active,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        ExtensionData.Empty);

    public static OperationCorrelation Correlation(RunId? runId = null) =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId ?? new RunId(Guid.NewGuid()), null);

    public static SessionOperationContext OperationContext(SessionAddress address)
    {
        var identity = Identity();
        var correlation = Correlation();
        return new SessionOperationContext(address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
    }

    public static SessionProfileSnapshot Profile(string storeKey = "fake", int maximumAppendEntries = 128,
        int maximumPageSize = 256, bool requiresDurableStore = false,
        SessionStoreCapabilities requiredStoreCapabilities = SessionStoreCapabilities.None) => new(
        new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey(storeKey), requiredStoreCapabilities, requiresDurableStore,
        requiresDistributedFencing: false, new SessionRetentionProfileKey("retention"), SessionBusyBehavior.Reject,
        maximumAppendEntries, maximumPageSize, verifySnapshotHashes: true, deleteOnDispose: false,
        new ContentHash("sha256:profile"));

    public static SecurityAuthorizationContext Authorization(AgentId agentId, SessionId? sessionId,
        OperationCorrelation correlation, ExecutionIdentity identity) => new(
        new SecurityProfileKey("security"), new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1),
        new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    public static SessionOperationContext BeforeRunLaneContext(SessionAddress address, ExecutionLaneId laneId,
        OperationId? operationId = null)
    {
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(operationId ?? new OperationId(Guid.NewGuid()), null);
        return new SessionOperationContext(address.AgentId, address.SessionId, laneId, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
    }

    public static SessionOperationContext InRunLaneContext(SessionAddress address, ExecutionLaneId laneId,
        RunId runId, TurnId turnId, OperationId? operationId = null)
    {
        var identity = Identity();
        var correlation = new InRunOperationCorrelation(operationId ?? new OperationId(Guid.NewGuid()), runId, turnId);
        return new SessionOperationContext(address.AgentId, address.SessionId, laneId, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
    }

    public static SessionProfileReference ProfileReference() =>
        new(new SessionProfileKey("profile"), new SessionProfileVersion(1));

    public static AgentInput Input() => new(new InputId(Guid.NewGuid()), InputDelivery.FollowUp,
        [new TextPart("content", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

    public static InputPreprocessingManifest Preprocessing() => new(new ConfigurationVersion(1),
        new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective"));

    public static SessionExecutionLaneProvisionRequest LaneProvisionRequest(SessionOperationContext context) => new(
        context, new SessionBranchCursor(new BranchId(Guid.NewGuid()), null), new SessionVersion(1),
        new SessionEntryId(Guid.NewGuid()), ProfileReference(),
        new RunConfigurationReference(context.Authorization.ConfigurationVersion, new RunPolicyVersion(1),
            new ContentHash("sha256:configuration")), DateTimeOffset.UnixEpoch, new IdempotencyKey("lane"));

    public static SessionInputAdmissionRequest InputAdmissionRequest(SessionOperationContext context)
    {
        var input = Input();
        return new(
            context, new AdmissionId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), input,
            input, Preprocessing(), DateTimeOffset.UnixEpoch, new SessionVersion(1),
            new SessionLaneRevision(1), new SessionBranchCursor(new BranchId(Guid.NewGuid()), null),
            new IdempotencyKey("admit"), maximumPendingInputs: 8);
    }

    public static SessionRunStartRequest RunStartRequest(SessionOperationContext beforeRunContext, RunId runId,
        TurnId turnId)
    {
        var admissionId = new AdmissionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var promotionEntryId = new SessionEntryId(Guid.NewGuid());
        var acceptedEntryId = new SessionEntryId(Guid.NewGuid());
        var configuration = new RunConfigurationReference(beforeRunContext.Authorization.ConfigurationVersion,
            new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var inRunCorrelation = new InRunOperationCorrelation(beforeRunContext.Correlation.OperationId, runId, turnId);
        var inRunAuthorization = new SecurityAuthorizationContext(beforeRunContext.Authorization.ProfileKey,
            beforeRunContext.Authorization.ProfileVersion, beforeRunContext.Authorization.PolicySnapshot,
            beforeRunContext.Authorization.AuthorityKey, beforeRunContext.Authorization.AgentDefinitionRevision,
            configuration.ConfigurationVersion,
            new SecurityAuthorizationScope(beforeRunContext.AgentId, beforeRunContext.SessionId, inRunCorrelation),
            beforeRunContext.Identity);
        return new SessionRunStartRequest(beforeRunContext, admissionId, [admissionId], new SessionSequence(1),
            new SessionLaneRevision(1), new SessionVersion(1), new SessionBranchCursor(branchId, null), null,
            runId, turnId, promotionEntryId, [new SessionEntryId(Guid.NewGuid())],
            [new MessageId(Guid.NewGuid())], acceptedEntryId, new OperationStateRevision(1), ProfileReference(),
            configuration, inRunAuthorization, DateTimeOffset.UnixEpoch, new IdempotencyKey("start"));
    }

    public static SessionRunStateRequest RunStateRequest(SessionOperationContext inRunContext) => new(inRunContext);

    public static SessionRunReleaseRequest RunReleaseRequest(SessionOperationContext inRunContext) => new(
        inRunContext, new OperationStateRevision(1), new SessionVersion(1), new IdempotencyKey("release"));

    public static SessionDirectoryListRequest DirectoryListRequest(AgentId? agentId = null)
    {
        var selectedAgentId = agentId ?? new AgentId(Guid.NewGuid());
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        return new SessionDirectoryListRequest(selectedAgentId, identity,
            Authorization(selectedAgentId, null, correlation, identity), null, 16);
    }

    public static MessageSessionEntry MessageEntry(SessionAddress address, BranchId branchId, long sequence) => new(
        new SessionEntryId(Guid.NewGuid()),
        address,
        Correlation(),
        branchId,
        new SessionSequence(sequence),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        new UserMessage(
            new MessageId(Guid.NewGuid()),
            address.AgentId,
            address.SessionId,
            null,
            branchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty));
}
