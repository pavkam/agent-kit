// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;
using AgentKit.TestSupport;

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

    public static SecurityAuthorizationContext Authorization() =>
        Authorization(
            AgentId,
            SessionId,
            new InRunOperationCorrelation(OperationId, RunId, TurnId));

    public static SecurityAuthorizationContext Authorization(
        AgentId agentId,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity? identity = null) =>
        new(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("c1000000-0000-0000-0000-000000000009")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(0),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity ?? TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human));

    public static DurableExecutionContext Context() =>
        new(
            new DurabilityProfileKey("profile"),
            new DurabilityProfileVersion(1),
            new DurableBackendKey("backend"),
            new DurableJournalKey("journal"),
            new DurableLeaseManagerKey("leases"),
            new RecoveryPolicyKey("policy"),
            Authorization());

    public static DurableExecutionContext Context(SecurityAuthorizationContext authorization) =>
        new(
            new DurabilityProfileKey("profile"),
            new DurabilityProfileVersion(1),
            new DurableBackendKey("backend"),
            new DurableJournalKey("journal"),
            new DurableLeaseManagerKey("leases"),
            new RecoveryPolicyKey("policy"),
            authorization);

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

    public static DurableOperationBinding Binding() => new(Address(), Context());

    public static DurableBackendDescriptor BackendDescriptor() =>
        new(
            new DurableBackendKey("backend"),
            new DurableBackendCapabilities(
                SupportsDistributedOwnership: false,
                SupportsExternalHandoff: true,
                SupportsReconciliation: true),
            [new DurableOperationName("tool.call")],
            supportsFencing: true,
            supportsReconciliation: true);

    public static SecurityGrant Grant()
    {
        var authorization = Authorization();
        return new SecurityGrant(
            new GrantId(Guid.Parse("d1000000-0000-0000-0000-00000000000a")),
            new SecurityRequestId(Guid.Parse("e1000000-0000-0000-0000-00000000000b")),
            authorization.Scope,
            authorization.Identity,
            authorization,
            new ComponentId("durable-journal"),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, "durable-journal:journal")],
            new InputFingerprint("sha256:durable"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            Now,
            Now.AddMinutes(1),
            1);
    }

    public static SecurityEnforcementIntent Intent() =>
        new(new SecurityEnforcementIntentId(Guid.Parse("f1000000-0000-0000-0000-00000000000c")), null);

    public static IExecutionLease Lease() => new FakeExecutionLease();

    public static IDurabilityRuntimeLease RuntimeLease() => new FakeRuntimeLease();

    private sealed class FakeRuntimeLease: IDurabilityRuntimeLease
    {
        public DurableExecutionContext Context => DurabilityTestData.Context();

        public IDurableExecutionBackend Backend => throw new NotSupportedException("The contract test never activates a backend.");

        public IDurableOperationJournal Journal => throw new NotSupportedException("The contract test never activates a journal.");

        public IDurableLeaseManager LeaseManager => throw new NotSupportedException("The contract test never activates a lease manager.");

        public IRecoveryPolicy RecoveryPolicy => throw new NotSupportedException("The contract test never activates a recovery policy.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeExecutionLease: IExecutionLease
    {
        public ExecutionLeaseId LeaseId => DurabilityTestData.LeaseId;

        public WorkerId OwnerWorkerId => WorkerId;

        public DurableOperationAddress Address => DurabilityTestData.Address();

        public FencingToken FencingToken => Token;

        public DateTimeOffset ExpiresAt => Now.AddMinutes(1);

        public ValueTask<LeaseRenewalResult> RenewAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<LeaseRenewalResult>(new LeaseRenewed(ExpiresAt));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
