// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Shared construction helpers for session store tests.</summary>
internal static class TestFactory
{
    public static InMemorySessionStore CreateStore(TimeProvider? timeProvider = null)
    {
        var security = new TestSecurityHarness();
        var store = new InMemorySessionStore(
            new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)),
            new GuidIdentifierGenerator<SecurityAuditRecordId>(static v => new SecurityAuditRecordId(v)),
            security,
            security,
            timeProvider ?? TimeProvider.System);
        TestSecurityHarness.Register(store, security);
        return store;
    }

    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    public static SessionCreateRequest CreateRequest(
        AgentId? agentId = null,
        IdempotencyKey? idempotencyKey = null,
        ConversationId? conversationId = null,
        ExecutionIdentity? identity = null)
    {
        var resolvedAgentId = agentId ?? new AgentId(Guid.NewGuid());
        identity ??= Identity();
        return new SessionCreateRequest(
            resolvedAgentId,
            identity,
            CreationAuthorization(resolvedAgentId, identity),
            conversationId,
            idempotencyKey ?? new IdempotencyKey(Guid.NewGuid().ToString()),
            ExtensionData.Empty);
    }

    public static OperationCorrelation Correlation(RunId? runId = null) =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId ?? new RunId(Guid.NewGuid()), null);

    public static SessionOperationContext OperationContext(
        SessionAddress address,
        RunId? runId = null,
        string tenant = "tenant-1",
        string principal = "user-1") =>
        CreateOperationContext(address, Correlation(runId), Identity(tenant, principal));

    private static SessionOperationContext CreateOperationContext(
        SessionAddress address, OperationCorrelation correlation, ExecutionIdentity identity) => new(
        address.AgentId, address.SessionId, null, correlation, identity,
        Authorization(address.AgentId, address.SessionId, correlation, identity));

    private static SecurityAuthorizationContext CreationAuthorization(AgentId agentId, ExecutionIdentity identity)
    {
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")), null);
        return Authorization(agentId, null, correlation, identity);
    }

    internal static SecurityAuthorizationContext Authorization(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) => new(
        new SecurityProfileKey("security"), new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(
            Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1),
        new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    internal static SessionOperationContext LaneContext(
        SessionAddress address,
        ExecutionLaneId laneId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
        address.AgentId, address.SessionId, laneId, correlation, identity,
        Authorization(address.AgentId, address.SessionId, correlation, identity));

    public static MessageSessionEntry MessageEntry(SessionAddress address, BranchId branchId, long sequence, string text = "hello") =>
        new(
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
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));

    public static async Task<SessionDescriptor> CreateSessionAsync(InMemorySessionStore store, AgentId? agentId = null)
    {
        var result = await store.CreateAsync(CreateRequest(agentId), TestContext.Current.CancellationToken);
        return ((SessionCreated) result).Descriptor;
    }
}
