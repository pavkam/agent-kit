// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies SessionRunStartRequest behavior and contracts.</summary>
public sealed class SessionRunStartRequestTests
{
    [Fact]
    public void SessionRunStartRequest_WhenContextConfigurationDiffers_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var context = Context(evidence, new ConfigurationVersion(1));
        var exception = Should.Throw<ArgumentException>(() => StartRequest(evidence, context, new ConfigurationVersion(2)));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void SessionRunStartRequest_WhenConfigurationEvidenceMatches_RetainsExactValues()
    {
        var evidence = Evidence();
        var context = Context(evidence, new ConfigurationVersion(2));
        var request = StartRequest(evidence, context, new ConfigurationVersion(2));
        request.Context.ShouldBe(context);
        request.Configuration.ConfigurationVersion.ShouldBe(new ConfigurationVersion(2));
    }

    private static SessionRunStartRequest StartRequest(AcceptanceEvidence evidence, SessionOperationContext context, ConfigurationVersion configurationVersion)
    {
        var configuration = new RunConfigurationReference(configurationVersion, new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var authorization = Authorization(evidence, evidence.InRunCorrelation, configurationVersion);
        return new SessionRunStartRequest(context, evidence.AdmissionId, [evidence.AdmissionId], new SessionSequence(1), new SessionLaneRevision(1), new SessionVersion(1), new SessionBranchCursor(evidence.BranchId, evidence.PreviousEntryId), null, evidence.RunId, evidence.TurnId, evidence.PromotionEntryId, [evidence.MaterializedEntryId], [evidence.MessageId], evidence.AcceptedEntryId, new OperationStateRevision(1), new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)), configuration, authorization, DateTimeOffset.UnixEpoch, new IdempotencyKey("start"));
    }

    private static SessionOperationContext Context(AcceptanceEvidence evidence, ConfigurationVersion configurationVersion) => new(evidence.Address.AgentId, evidence.Address.SessionId, evidence.LaneId, evidence.BeforeRunCorrelation, evidence.Identity, Authorization(evidence, evidence.BeforeRunCorrelation, configurationVersion));
    private static SecurityAuthorizationContext Authorization(AcceptanceEvidence evidence, OperationCorrelation correlation, ConfigurationVersion configurationVersion) => new(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("22222222-2222-2222-2222-222222222222")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), configurationVersion, new SecurityAuthorizationScope(evidence.Address.AgentId, evidence.Address.SessionId, correlation), evidence.Identity);
    private static AcceptanceEvidence Evidence()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        return new AcceptanceEvidence(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new ExecutionLaneId(Guid.NewGuid()), new BranchId(Guid.NewGuid()), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), new BeforeRunOperationCorrelation(operationId, null), new InRunOperationCorrelation(operationId, runId, turnId), runId, turnId, new AdmissionId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()), new MessageId(Guid.NewGuid()));
    }

    private sealed record AcceptanceEvidence(SessionAddress Address, ExecutionLaneId LaneId, BranchId BranchId, ExecutionIdentity Identity, BeforeRunOperationCorrelation BeforeRunCorrelation, InRunOperationCorrelation InRunCorrelation, RunId RunId, TurnId TurnId, AdmissionId AdmissionId, SessionEntryId PreviousEntryId, SessionEntryId PromotionEntryId, SessionEntryId MaterializedEntryId, SessionEntryId AcceptedEntryId, MessageId MessageId);
}
