// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Builds the deterministic durable values every durability conformance suite exchanges.</summary>
/// <remarks>
/// Every value here is content-free and fully determined by its arguments, so two adapters running the same suite
/// exchange byte-identical requests. A fixture derives its grants from these requests rather than inventing its own,
/// which is what lets one suite authorize against adapters whose journal keys and audiences differ.
/// </remarks>
public static class DurabilityConformanceData
{
    /// <summary>Gets the fixed instant every conformance record is stamped with.</summary>
    /// <value>An instant far from epoch boundaries so ordering mistakes are visible.</value>
    public static DateTimeOffset Now { get; } = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>Gets the agent every conformance operation belongs to.</summary>
    /// <value>A stable agent identity.</value>
    public static AgentId Agent { get; } = new(Guid.Parse("11000000-0000-0000-0000-000000000001"));

    /// <summary>Gets the session every conformance operation belongs to.</summary>
    /// <value>A stable session identity.</value>
    public static SessionId Session { get; } = new(Guid.Parse("12000000-0000-0000-0000-000000000001"));

    /// <summary>Gets the run every conformance operation belongs to.</summary>
    /// <value>A stable run identity.</value>
    public static RunId Run { get; } = new(Guid.Parse("13000000-0000-0000-0000-000000000001"));

    /// <summary>Gets the turn every conformance operation belongs to.</summary>
    /// <value>A stable turn identity.</value>
    public static TurnId Turn { get; } = new(Guid.Parse("14000000-0000-0000-0000-000000000001"));

    /// <summary>Gets the primary operation identity conformance scenarios address.</summary>
    /// <value>A stable operation identity.</value>
    public static OperationId Operation { get; } = new(Guid.Parse("15000000-0000-0000-0000-000000000001"));

    /// <summary>Gets a second operation identity used to prove addresses are tracked independently.</summary>
    /// <value>A stable operation identity distinct from <see cref="Operation"/>.</value>
    public static OperationId OtherOperation { get; } = new(Guid.Parse("15000000-0000-0000-0000-000000000002"));

    /// <summary>Gets the worker identity a lease scenario acquires ownership as.</summary>
    /// <value>A stable worker identity.</value>
    public static WorkerId Worker { get; } = new(Guid.Parse("16000000-0000-0000-0000-000000000001"));

    /// <summary>Gets a second worker identity used to prove mutual exclusion and takeover.</summary>
    /// <value>A stable worker identity distinct from <see cref="Worker"/>.</value>
    public static WorkerId OtherWorker { get; } = new(Guid.Parse("16000000-0000-0000-0000-000000000002"));

    /// <summary>Gets the checkpoint identity conformance snapshots carry.</summary>
    /// <value>A stable checkpoint identity.</value>
    public static CheckpointId Checkpoint { get; } = new(Guid.Parse("17000000-0000-0000-0000-000000000001"));

    /// <summary>Builds the durable coordinates one conformance operation is addressed by.</summary>
    /// <param name="operationId">The operation to address, defaulting to <see cref="Operation"/>.</param>
    /// <returns>An in-run address carrying the shared agent, session, run, and turn.</returns>
    public static DurableOperationAddress Address(OperationId? operationId = null) =>
        new(Agent, Session, Run, operationId ?? Operation, Turn);

    /// <summary>Builds the captured authorization one conformance operation runs under.</summary>
    /// <param name="operationId">The operation the correlation names, defaulting to <see cref="Operation"/>.</param>
    /// <returns>An in-run authorization capture whose scope describes <see cref="Address(OperationId?)"/>.</returns>
    public static SecurityAuthorizationContext Authorization(OperationId? operationId = null) => new(
        new SecurityProfileKey("conformance-security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("18000000-0000-0000-0000-000000000001")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:conformance-policy")),
        new ComponentKey<ISecurityAuthority>("conformance-authority"),
        new AgentDefinitionRevision(0),
        new ConfigurationVersion(1),
        new SecurityAuthorizationScope(
            Agent, Session, new InRunOperationCorrelation(operationId ?? Operation, Run, Turn)),
        TestExecutionIdentity.Create(
            new TenantId("conformance-tenant"), new PrincipalId("conformance-principal"), ExecutionSubjectKind.Human));

    /// <summary>Builds the durable execution context a conformance request is bound to.</summary>
    /// <param name="journalKey">The journal the adapter under test is registered under.</param>
    /// <param name="operationId">The operation the context correlates, defaulting to <see cref="Operation"/>.</param>
    /// <param name="authorization">An explicit authorization capture, defaulting to <see cref="Authorization(OperationId?)"/>.</param>
    /// <returns>A context naming the conformance profile, backend, lease manager, and recovery policy.</returns>
    public static DurableExecutionContext Context(
        DurableJournalKey journalKey,
        OperationId? operationId = null,
        SecurityAuthorizationContext? authorization = null) => new(
        new DurabilityProfileKey("conformance-profile"),
        new DurabilityProfileVersion(1),
        new DurableBackendKey("conformance-backend"),
        journalKey,
        new DurableLeaseManagerKey("conformance-leases"),
        new RecoveryPolicyKey("conformance-policy"),
        authorization ?? Authorization(operationId));

    /// <summary>Builds an opaque durable state payload.</summary>
    /// <param name="marker">Distinguishes otherwise identical payloads.</param>
    /// <returns>A versioned payload carrying three bytes.</returns>
    public static OperationPayload Payload(byte marker = 1) => new(new SchemaVersion("conformance-v1"), [marker, 2, 3]);

    /// <summary>Builds the complete recoverable declaration for one conformance operation.</summary>
    /// <param name="journalKey">The journal the adapter under test is registered under.</param>
    /// <param name="operationId">The operation to describe, defaulting to <see cref="Operation"/>.</param>
    /// <param name="context">An explicit durable context, defaulting to <see cref="Context"/>.</param>
    /// <returns>A non-idempotent caller-owned descriptor with a five-minute deadline.</returns>
    public static RecoverableOperationDescriptor Descriptor(
        DurableJournalKey journalKey,
        OperationId? operationId = null,
        DurableExecutionContext? context = null) => new(
        Address(operationId),
        context ?? Context(journalKey, operationId),
        new DurableOperationName("conformance.operation"),
        new DurableOperationVersion("v1"),
        new IdempotencyKey("conformance-idempotency"),
        Payload(),
        DurableRetryOwner.Caller,
        DurableTimeoutOwner.Caller,
        CancellationSemantics.LocalWaitOnly,
        SecurityEffect.Execute,
        IdempotencyClassification.NonIdempotent,
        Now.AddMinutes(5));

    /// <summary>Builds one acceptance declaration.</summary>
    /// <param name="journalKey">The journal the adapter under test is registered under.</param>
    /// <param name="token">The ownership generation committing acceptance.</param>
    /// <param name="operationId">The operation to accept, defaulting to <see cref="Operation"/>.</param>
    /// <param name="context">An explicit durable context, defaulting to <see cref="Context"/>.</param>
    /// <returns>An acceptance record stamped at <see cref="Now"/>.</returns>
    public static DurableOperationStart Start(
        DurableJournalKey journalKey,
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null) =>
        new(Descriptor(journalKey, operationId, context), Payload(), token, Now);

    /// <summary>Builds one state snapshot.</summary>
    /// <param name="journalKey">The journal the adapter under test is registered under.</param>
    /// <param name="token">The ownership generation committing the snapshot.</param>
    /// <param name="operationId">The operation to snapshot, defaulting to <see cref="Operation"/>.</param>
    /// <param name="context">An explicit durable context, defaulting to <see cref="Context"/>.</param>
    /// <param name="marker">Distinguishes otherwise identical snapshot payloads.</param>
    /// <returns>A tool-call checkpoint stamped at <see cref="Now"/>.</returns>
    public static DurableCheckpoint CheckpointRecord(
        DurableJournalKey journalKey,
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null,
        byte marker = 2) => new(
        Checkpoint,
        Address(operationId),
        context ?? Context(journalKey, operationId),
        DurableCheckpointKind.ToolCallRecorded,
        Payload(marker),
        token,
        Now);

    /// <summary>Builds one authoritative terminal record.</summary>
    /// <param name="journalKey">The journal the adapter under test is registered under.</param>
    /// <param name="token">The ownership generation committing the terminal record.</param>
    /// <param name="operationId">The operation to settle, defaulting to <see cref="Operation"/>.</param>
    /// <param name="context">An explicit durable context, defaulting to <see cref="Context"/>.</param>
    /// <param name="state">The terminal lifecycle state.</param>
    /// <param name="certainty">What is actually known about the external effect.</param>
    /// <param name="marker">Distinguishes otherwise identical terminal payloads.</param>
    /// <returns>A terminal record stamped at <see cref="Now"/>.</returns>
    public static DurableOperationResult Result(
        DurableJournalKey journalKey,
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null,
        DurableOperationState state = DurableOperationState.Completed,
        SideEffectCertainty certainty = SideEffectCertainty.DefinitelyPerformed,
        byte marker = 3) => new(
        Address(operationId),
        context ?? Context(journalKey, operationId),
        state,
        certainty,
        Payload(marker),
        token,
        Now);

    /// <summary>Builds one waiting record and its wait condition.</summary>
    /// <param name="journalKey">The journal the adapter under test is registered under.</param>
    /// <param name="token">The ownership generation committing the waiting record.</param>
    /// <param name="operationId">The operation to defer, defaulting to <see cref="Operation"/>.</param>
    /// <param name="context">An explicit durable context, defaulting to <see cref="Context"/>.</param>
    /// <param name="notBefore">The earliest instant the operation may resume.</param>
    /// <param name="certainty">What is actually known about the external effect.</param>
    /// <param name="externalReference">The external owner's handle when work was handed off.</param>
    /// <param name="externalIdempotencyKey">The key the effect owner accepted, when one exists.</param>
    /// <returns>A waiting record stamped at <see cref="Now"/>.</returns>
    public static DurableOperationWaiting Waiting(
        DurableJournalKey journalKey,
        FencingToken token,
        OperationId? operationId = null,
        DurableExecutionContext? context = null,
        DateTimeOffset? notBefore = null,
        SideEffectCertainty certainty = SideEffectCertainty.Unknown,
        ExternalOperationReference? externalReference = null,
        IdempotencyKey? externalIdempotencyKey = null) => new(
        new DurableOperationBinding(Address(operationId), context ?? Context(journalKey, operationId)),
        token,
        Now,
        certainty,
        externalReference,
        externalIdempotencyKey,
        notBefore ?? Now.AddMinutes(10));

    /// <summary>Builds one execution-lease acquisition request.</summary>
    /// <param name="workerId">The worker seeking ownership, defaulting to <see cref="Worker"/>.</param>
    /// <param name="operationId">The operation to own, defaulting to <see cref="Operation"/>.</param>
    /// <param name="duration">The requested lease duration, defaulting to one minute.</param>
    /// <returns>A lease request for the shared conformance address.</returns>
    public static ExecutionLeaseRequest LeaseRequest(
        WorkerId? workerId = null,
        OperationId? operationId = null,
        TimeSpan? duration = null) =>
        new(Address(operationId), workerId ?? Worker, duration ?? TimeSpan.FromMinutes(1));
}
