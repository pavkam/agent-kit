// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

internal static class TestFactory
{
    public static ExecutionIdentity Identity(string tenant = "tenant-1", string principal = "user-1") =>
        TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    public static SessionCreateRequest CreateRequest(AgentId? agentId = null, IdempotencyKey? idempotencyKey = null) => new(
        agentId ?? new AgentId(Guid.NewGuid()),
        Identity(),
        null,
        idempotencyKey ?? new IdempotencyKey(Guid.NewGuid().ToString()), ExtensionData.Empty);

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

    public static SessionOperationContext OperationContext(SessionAddress address) =>
        new(address.AgentId, address.SessionId, Correlation(), Identity());

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
