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
        int maximumPageSize = 256) => new(
        new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey(storeKey), SessionStoreCapabilities.None, requiresDurableStore: false,
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
