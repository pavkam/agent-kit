// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Shared construction helpers for session store tests.</summary>
internal static class TestFactory
{
    public static InMemorySessionStore CreateStore(TimeProvider? timeProvider = null) => new(
        new GuidIdentifierGenerator<SessionId>(static v => new SessionId(v)),
        new GuidIdentifierGenerator<BranchId>(static v => new BranchId(v)),
        timeProvider ?? TimeProvider.System);

    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        new(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human, ExtensionData.Empty);

    public static SessionCreateRequest CreateRequest(
        AgentId? agentId = null,
        IdempotencyKey? idempotencyKey = null,
        ConversationId? conversationId = null) => new(
        agentId ?? new AgentId(Guid.NewGuid()),
        Identity(),
        conversationId,
        idempotencyKey ?? new IdempotencyKey(Guid.NewGuid().ToString()),
        ExtensionData.Empty);

    public static OperationCorrelation Correlation(RunId? runId = null) =>
        new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId ?? new RunId(Guid.NewGuid()), null);

    public static SessionOperationContext OperationContext(
        SessionAddress address,
        RunId? runId = null,
        string tenant = "tenant-1",
        string principal = "user-1") =>
        new(address.AgentId, address.SessionId, Correlation(runId), Identity(tenant, principal));

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
