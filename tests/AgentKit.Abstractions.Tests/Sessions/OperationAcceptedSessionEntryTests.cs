// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies OperationAcceptedSessionEntry behavior and contracts.</summary>
public sealed class OperationAcceptedSessionEntryTests
{
    [Fact]
    public void OperationAcceptedSessionEntry_WhenEntryParentsItself_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var state = State(evidence);
        var exception = Should.Throw<ArgumentException>(() => new OperationAcceptedSessionEntry(evidence.AcceptedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(4), evidence.AcceptedEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), state));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causalParentId");
    }

    [Fact]
    public void OperationAcceptedSessionEntry_WhenEntryReusesMaterializedIdentity_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var state = State(evidence);
        var exception = Should.Throw<ArgumentException>(() => new OperationAcceptedSessionEntry(evidence.MaterializedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(4), evidence.MaterializedEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), state));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("id");
    }

    private static SessionAcceptedRunState State(AcceptanceEvidence evidence, AdmissionId? initiatingAdmissionId = null, SessionBranchCursor? previousCursor = null, SessionBranchCursor? committedCursor = null, bool defaultInitiatingAdmissionId = false, ConfigurationVersion? authorizationConfigurationVersion = null) => new(evidence.Address, evidence.LaneId, new SessionLaneRevision(2), evidence.InRunCorrelation, new OperationStateRevision(1), evidence.Identity, Authorization(evidence, evidence.InRunCorrelation, authorizationConfigurationVersion ?? new ConfigurationVersion(1)), new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)), new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration")), previousCursor ?? new SessionBranchCursor(evidence.BranchId, evidence.PreviousEntryId), committedCursor ?? new SessionBranchCursor(evidence.BranchId, evidence.AcceptedEntryId), new SessionSequence(1), defaultInitiatingAdmissionId ? default : initiatingAdmissionId ?? evidence.AdmissionId, [evidence.AdmissionId], [evidence.MaterializedEntryId], [evidence.MessageId], evidence.TurnId, DateTimeOffset.UnixEpoch);
    private static SecurityAuthorizationContext Authorization(AcceptanceEvidence evidence, OperationCorrelation correlation, ConfigurationVersion configurationVersion) => new(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("22222222-2222-2222-2222-222222222222")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), configurationVersion, new SecurityAuthorizationScope(evidence.Address.AgentId, evidence.Address.SessionId, correlation), evidence.Identity);
    private static AcceptanceEvidence Evidence()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        return new AcceptanceEvidence(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new ExecutionLaneId(Guid.NewGuid()), new BranchId(Guid.NewGuid()), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), new BeforeRunOperationCorrelation(operationId, null), new InRunOperationCorrelation(operationId, runId, turnId), runId, turnId, new AdmissionId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new MessageId(Guid.NewGuid()));
    }

    private sealed record AcceptanceEvidence(SessionAddress Address, ExecutionLaneId LaneId, BranchId BranchId, ExecutionIdentity Identity, BeforeRunOperationCorrelation BeforeRunCorrelation, InRunOperationCorrelation InRunCorrelation, RunId RunId, TurnId TurnId, AdmissionId AdmissionId, SessionEntryId PreviousEntryId, SessionEntryId PromotionEntryId, SessionEntryId MaterializedEntryId, SessionEntryId AcceptedEntryId, MessageId MessageId);
    [Fact]
    public void ValidAcceptanceContracts_WhenReconstructed_RetainTriggerAndCausalRelations()
    {
        var evidence = Evidence();
        var state = State(evidence);
        var accepted = new OperationAcceptedSessionEntry(evidence.AcceptedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(4), evidence.MaterializedEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), state);
        accepted.State.ShouldBe(state);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var evidence = Evidence();
        var state = State(evidence);
        var original = new OperationAcceptedSessionEntry(evidence.AcceptedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(4), evidence.MaterializedEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), state);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
