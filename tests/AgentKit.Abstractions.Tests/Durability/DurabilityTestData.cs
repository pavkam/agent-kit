// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Deterministic builders for durability contract values. Every identity is a
/// fixed GUID so equality assertions never depend on generation order.
/// </summary>
internal static class DurabilityTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddHours(1);

    public static AgentId AgentId { get; } =
        new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static SessionId SessionId { get; } =
        new(Guid.Parse("b0000000-0000-0000-0000-000000000002"));

    public static RunId RunId { get; } =
        new(Guid.Parse("c0000000-0000-0000-0000-000000000003"));

    public static OperationId OperationId { get; } =
        new(Guid.Parse("d0000000-0000-0000-0000-000000000004"));

    public static TurnId TurnId { get; } =
        new(Guid.Parse("e0000000-0000-0000-0000-000000000005"));

    public static CheckpointId CheckpointId { get; } =
        new(Guid.Parse("f0000000-0000-0000-0000-000000000006"));

    public static WorkerId WorkerId { get; } =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000007"));

    public static ExecutionLeaseId LeaseId { get; } =
        new(Guid.Parse("b1000000-0000-0000-0000-000000000008"));

    public static FencingToken Token { get; } = new(1);

    public static DurableOperationAddress Address() =>
        new(AgentId, SessionId, RunId, OperationId, TurnId);

    public static SecurityAuthorizationScope Authorization() =>
        new(AgentId, SessionId, new BeforeRunOperationCorrelation(OperationId, null));

    public static DurableExecutionContext Context() =>
        new(
            new DurabilityProfileKey("profile"),
            new DurabilityProfileVersion(1),
            new DurableBackendKey("backend"),
            new DurableJournalKey("journal"),
            new DurableLeaseManagerKey("leases"),
            new RecoveryPolicyKey("policy"),
            Authorization());

    public static OperationPayload Payload() =>
        new(new SchemaVersion("v1"), [1, 2, 3]);

    public static RecoverableOperationDescriptor Descriptor() =>
        new(
            Address(),
            Context(),
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

    public static DurableCheckpoint Checkpoint() =>
        new(
            CheckpointId,
            Address(),
            Context(),
            DurableCheckpointKind.ToolCallRecorded,
            Payload(),
            Token,
            Now);

    public static DurableOperationResult Result() =>
        new(
            Address(),
            Context(),
            DurableOperationState.Completed,
            SideEffectCertainty.DefinitelyPerformed,
            Payload(),
            Token,
            Now);

    public static RecoveryEvidence Evidence() =>
        new(
            Address(),
            Context(),
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown,
            startDefinitelyAbsent: false,
            terminalResultRecorded: false);

    public static ExternalOperationReference ExternalReference() =>
        new(new DurableBackendKey("backend"), "handle-1");
}
