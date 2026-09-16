// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies InputPromotedSessionEntry behavior and contracts.</summary>
public sealed class InputPromotedSessionEntryTests
{
    [Fact]
    public void InputPromotedSessionEntry_WhenInitiatingAdmissionIsNotPromoted_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var exception = Should.Throw<ArgumentException>(() => new InputPromotedSessionEntry(evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), evidence.LaneId, new AdmissionId(Guid.NewGuid()), new SessionSequence(1), [evidence.AdmissionId]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("initiatingAdmissionId");
    }

    [Fact]
    public void InputPromotedSessionEntry_WhenInitiatingAdmissionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var evidence = Evidence();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new InputPromotedSessionEntry(evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), evidence.LaneId, default, new SessionSequence(1), [evidence.AdmissionId]));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("initiatingAdmissionId");
    }

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
        var promoted = new InputPromotedSessionEntry(evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), evidence.LaneId, evidence.AdmissionId, new SessionSequence(1), [evidence.AdmissionId]);
        promoted.InitiatingAdmissionId.ShouldBe(evidence.AdmissionId);
        promoted.ExecutionLaneId.ShouldBe(evidence.LaneId);
        promoted.Cutoff.ShouldBe(new SessionSequence(1));
        promoted.AdmissionIds.ShouldBe([evidence.AdmissionId]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var evidence = Evidence();
        var original = new InputPromotedSessionEntry(evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId, new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), evidence.LaneId, evidence.AdmissionId, new SessionSequence(1), [evidence.AdmissionId]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
