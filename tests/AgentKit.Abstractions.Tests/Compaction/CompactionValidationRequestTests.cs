// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionValidationRequest behavior and contracts.</summary>
public sealed class CompactionValidationRequestTests
{
    [Fact]
    public void CompactionValidationRequest_Equality_WhenSameValues_InstancesAreEqual() => new CompactionValidationRequest(Request(), Snapshot(), Cut(), Candidate()).ShouldBe(new CompactionValidationRequest(Request(), Snapshot(), Cut(), Candidate()));
    private static readonly Guid _fixedOperationGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid _fixedRunGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_fixedOperationGuid), new RunId(_fixedRunGuid), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    // Fixed GUIDs make the "same values" equality assertions above
    // deterministic without threading a shared instance through every
    // helper.
    private static readonly Guid _fixedCompactionGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _fixedAgentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _fixedSessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static CompactionOperationContext Context() => TestSupport.TestSecurityEvidence.CompactionContext(new CompactionId(_fixedCompactionGuid), new AgentId(_fixedAgentGuid), new SessionId(_fixedSessionGuid), Correlation(), Identity());
    private static CompactionTrigger Trigger() => new(CompactionTriggerKind.ExplicitMaintenance, "test", null);
    private static CompactionRequest Request() => new(Context(), new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SessionVersion(1), new SessionSequence(1), new ContextEpoch(0), Trigger(), 100, 0.5, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5), ExtensionData.Empty);
    private static CompactionSourceRange Range() => new(new SessionSequence(1), new SessionSequence(2));
    private static CompactionSourceSnapshot Snapshot() => Snapshot([]);
    private static CompactionSourceSnapshot Snapshot(ImmutableArray<SessionEntry> entries) => new(Context(), new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SessionVersion(1), new SessionSequence(1), entries);
    private static CompactionCut Cut() => new(Range(), new SessionSequence(3), [new SessionEntryId(Guid.Parse("55555555-5555-5555-5555-555555555555"))]);
    private static CompactionCheckpoint Checkpoint() => new([new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
    private static CompactionProducer Producer() => new(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty);
    private static CompactionManifest Manifest() => new(new CompactionManifestId(Guid.Parse("66666666-6666-6666-6666-666666666666")), Context(), new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SessionVersion(1), Range(), new SessionSequence(3), Producer(), new ContextEpoch(0), new CompactionSizeEstimate(10, 10, 1), new CompactionSizeEstimate(5, 5, 1), DateTimeOffset.UnixEpoch, ExtensionData.Empty);
    private static CompactionCandidate Candidate() => new(Manifest(), Checkpoint());
}
