// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Deterministic builders for durable-journal fixture values.</summary>
internal static class DurableJournalTestData
{
    internal static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddHours(1);

    internal static AgentId AgentId { get; } = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    internal static SessionId SessionId { get; } = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    internal static RunId RunId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    internal static OperationId OperationId { get; } = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    internal static TurnId TurnId { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    internal static CheckpointId CheckpointId { get; } = new(Guid.Parse("60000000-0000-0000-0000-000000000001"));

    internal static DurableOperationAddress Address(OperationId? operationId = null) =>
        new(AgentId, SessionId, RunId, operationId ?? OperationId, TurnId);

    internal static SecurityAuthorizationContext Authorization(OperationId? operationId = null) => new(
        new SecurityProfileKey("security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"),
        new AgentDefinitionRevision(0),
        new ConfigurationVersion(1),
        new SecurityAuthorizationScope(AgentId, SessionId, new InRunOperationCorrelation(operationId ?? OperationId, RunId, TurnId)),
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));

    internal static SecurityAuthorizationContext ForeignAuthorization(OperationId? operationId = null) => new(
        new SecurityProfileKey("other-security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000002")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"),
        new AgentDefinitionRevision(0),
        new ConfigurationVersion(1),
        new SecurityAuthorizationScope(AgentId, SessionId, new InRunOperationCorrelation(operationId ?? OperationId, RunId, TurnId)),
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));

    internal static DurableExecutionContext Context(SecurityAuthorizationContext? authorization = null, OperationId? operationId = null) => new(
        new DurabilityProfileKey("profile"),
        new DurabilityProfileVersion(1),
        new DurableBackendKey("backend"),
        new DurableJournalKey("journal"),
        new DurableLeaseManagerKey("leases"),
        new RecoveryPolicyKey("policy"),
        authorization ?? Authorization(operationId));

    internal static OperationPayload Payload(byte marker = 1) => new(new SchemaVersion("v1"), [marker, 2, 3]);

    internal static RecoverableOperationDescriptor Descriptor(OperationId? operationId = null, DurableExecutionContext? context = null) => new(
        Address(operationId),
        context ?? Context(operationId: operationId),
        new DurableOperationName("tool.call"),
        new DurableOperationVersion("v1"),
        new IdempotencyKey("idem-1"),
        Payload(),
        DurableRetryOwner.Caller,
        DurableTimeoutOwner.Caller,
        CancellationSemantics.LocalWaitOnly,
        SecurityEffect.Execute,
        IdempotencyClassification.NonIdempotent,
        Now.AddMinutes(5));

    internal static DurableOperationStart Start(FencingToken token, OperationId? operationId = null, DurableExecutionContext? context = null) =>
        new(Descriptor(operationId, context), Payload(), token, Now);

    internal static DurableCheckpoint Checkpoint(FencingToken token, OperationId? operationId = null, DurableExecutionContext? context = null) =>
        new(CheckpointId, Address(operationId), context ?? Context(operationId: operationId), DurableCheckpointKind.ToolCallRecorded, Payload(2), token, Now);

    internal static DurableOperationResult Result(
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null,
        DurableOperationState state = DurableOperationState.Completed,
        SideEffectCertainty certainty = SideEffectCertainty.DefinitelyPerformed,
        byte marker = 3) =>
        new(Address(operationId), context ?? Context(operationId: operationId), state, certainty, Payload(marker), token, Now);
}
