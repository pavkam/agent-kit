// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist durable evidence.</summary>
/// <remarks>
/// The probe is deliberately denser than any single real operation: a compaction snapshot whose operation carries a
/// declaration with extension data, a latest checkpoint, a terminal result, an external reference, an external
/// idempotency key, and a deferred resumption instant. Proving that value round-trips exercises every nullable,
/// collection, enumeration, and byte-payload member at once, so an encoding contract that cannot carry durable state
/// fails during initialization rather than while recording authoritative recovery evidence.
/// </remarks>
/// <remarks>
/// Because it combines members that never co-occur in real state — a settled result alongside a deferred resumption
/// instant — the probe is an encoding fixture only. It is never projected into <see cref="RecoveryEvidence"/>, which
/// rightly rejects that combination.
/// </remarks>
internal static class DurableJournalProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>A compaction snapshot record covering every member a durable journal persists.</returns>
    internal static DurableJournalRecord Create()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var agentId = new AgentId(new Guid("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(new Guid("22222222-2222-2222-2222-222222222222"));
        var runId = new RunId(new Guid("33333333-3333-3333-3333-333333333333"));
        var turnId = new TurnId(new Guid("44444444-4444-4444-4444-444444444444"));
        var operationId = new OperationId(new Guid("55555555-5555-5555-5555-555555555555"));
        var address = new DurableOperationAddress(agentId, sessionId, runId, operationId, turnId);
        var binding = new DurableOperationBinding(address, CreateContext(agentId, sessionId, runId, turnId, operationId, now));
        var token = new FencingToken(7);
        var projection = new DurableOperationProjection(binding, token)
        {
            Descriptor = new RecoverableOperationDescriptor(
                binding,
                new DurableOperationName("probe.operation"),
                new DurableOperationVersion("v1"),
                new IdempotencyKey("probe-idempotency"),
                new OperationPayload(new SchemaVersion("probe-v1"), [1, 2, 3]),
                DurableRetryOwner.Caller,
                DurableTimeoutOwner.Caller,
                CancellationSemantics.LocalWaitOnly,
                SecurityEffect.Execute,
                IdempotencyClassification.NonIdempotent,
                now.AddMinutes(5),
                new OperationId(new Guid("66666666-6666-6666-6666-666666666666")),
                new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty
                    .Add("probe.extension", new ExtensionValue([.. "true"u8])))),
            State = DurableOperationState.Completed,
            SideEffectCertainty = SideEffectCertainty.DefinitelyPerformed,
            LatestCheckpoint = new DurableCheckpoint(
                new CheckpointId(new Guid("77777777-7777-7777-7777-777777777777")),
                binding,
                DurableCheckpointKind.ToolCallRecorded,
                new OperationPayload(new SchemaVersion("probe-v1"), [4, 5, 6]),
                token,
                now),
            TerminalResult = new DurableOperationResult(
                binding,
                DurableOperationState.Completed,
                SideEffectCertainty.DefinitelyPerformed,
                new OperationPayload(new SchemaVersion("probe-v1"), [7, 8, 9]),
                token,
                now,
                "probe-safe-message"),
            NotBefore = now.AddMinutes(10),
            ExternalReference = new ExternalOperationReference(new DurableBackendKey("probe-backend"), "probe-handle"),
            ExternalIdempotencyKey = new IdempotencyKey("probe-external-idempotency"),
        };

        return DurableJournalRecord.ForSnapshot(projection);
    }

    /// <summary>Builds the captured durability composition the probe's operation is bound to.</summary>
    /// <param name="agentId">The agent the probe scope names.</param>
    /// <param name="sessionId">The session the probe scope names.</param>
    /// <param name="runId">The run the probe correlation names.</param>
    /// <param name="turnId">The turn the probe correlation names.</param>
    /// <param name="operationId">The operation the probe correlation names.</param>
    /// <param name="now">The fixed instant the probe's authentication evidence is stamped with.</param>
    /// <returns>A context whose authorization scope describes the probe address exactly.</returns>
    private static DurableExecutionContext CreateContext(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        DateTimeOffset now)
    {
        Debug.Assert(now != default, "The probe is stamped with a fixed non-default instant.");
        var identity = new ExecutionIdentity(
            new TenantId("probe-tenant"),
            new PrincipalId("probe-principal"),
            ExecutionSubjectKind.Human,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("probe-evidence"),
                new IdentityIssuerId("probe-issuer"),
                "probe-method",
                now,
                now.AddHours(1),
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:probe"))),
            [new IdentityClaim(new IdentityIssuerId("probe-issuer"), "probe-claim", "value", IdentityClaimValueKind.Text)],
            [],
            IdentityAssuranceLevel.Strong,
            new IdentityVersion(1));
        return new DurableExecutionContext(
            new DurabilityProfileKey("probe-profile"),
            new DurabilityProfileVersion(1),
            new DurableBackendKey("probe-backend"),
            new DurableJournalKey("probe-journal"),
            new DurableLeaseManagerKey("probe-leases"),
            new RecoveryPolicyKey("probe-recovery"),
            new SecurityAuthorizationContext(
                new SecurityProfileKey("probe-security"),
                new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(new Guid("88888888-8888-8888-8888-888888888888")),
                    new SecurityPolicyVersion(1),
                    new ContentHash("sha256:probe-policy")),
                new ComponentKey<ISecurityAuthority>("probe-authority"),
                new AgentDefinitionRevision(0),
                new ConfigurationVersion(1),
                new SecurityAuthorizationScope(
                    agentId, sessionId, new InRunOperationCorrelation(operationId, runId, turnId)),
                identity));
    }
}
