// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

public sealed class SessionRunAcceptanceContractTests
{
    [Fact]
    public void SessionRunStartRequest_WhenContextConfigurationDiffers_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var context = Context(evidence, new ConfigurationVersion(1));

        var exception = Should.Throw<ArgumentException>(() => StartRequest(
            evidence, context, new ConfigurationVersion(2)));

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

    [Fact]
    public void InputPromotedSessionEntry_WhenInitiatingAdmissionIsNotPromoted_ThrowsExactArgumentException()
    {
        var evidence = Evidence();

        var exception = Should.Throw<ArgumentException>(() => new InputPromotedSessionEntry(
            evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId,
            new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"), evidence.LaneId, new AdmissionId(Guid.NewGuid()),
            new SessionSequence(1), [evidence.AdmissionId]));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("initiatingAdmissionId");
    }

    [Fact]
    public void InputPromotedSessionEntry_WhenInitiatingAdmissionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var evidence = Evidence();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new InputPromotedSessionEntry(
            evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId,
            new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"), evidence.LaneId, default,
            new SessionSequence(1), [evidence.AdmissionId]));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("initiatingAdmissionId");
    }

    [Fact]
    public void SessionAcceptedRunState_WhenInitiatingAdmissionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var evidence = Evidence();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => State(
            evidence, defaultInitiatingAdmissionId: true));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("initiatingAdmissionId");
    }

    [Fact]
    public void SessionAcceptedRunState_WhenInitiatingAdmissionIsNotPromoted_ThrowsExactArgumentException()
    {
        var evidence = Evidence();

        var exception = Should.Throw<ArgumentException>(() => State(
            evidence, initiatingAdmissionId: new AdmissionId(Guid.NewGuid())));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("initiatingAdmissionId");
    }

    [Fact]
    public void SessionAcceptedRunState_WhenCommittedTipIsUnchanged_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var cursor = new SessionBranchCursor(evidence.BranchId, evidence.PreviousEntryId);

        var exception = Should.Throw<ArgumentException>(() => State(evidence, committedCursor: cursor));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("committedCursor");
    }

    [Fact]
    public void SessionAcceptedRunState_WhenCommittedTipIsMaterialized_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var cursor = new SessionBranchCursor(evidence.BranchId, evidence.MaterializedEntryId);

        var exception = Should.Throw<ArgumentException>(() => State(evidence, committedCursor: cursor));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("committedCursor");
    }

    [Fact]
    public void SessionAcceptedRunState_WhenPreviousTipIsMaterialized_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var cursor = new SessionBranchCursor(evidence.BranchId, evidence.MaterializedEntryId);

        var exception = Should.Throw<ArgumentException>(() => State(evidence, previousCursor: cursor));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("materializedEntryIds");
    }

    [Fact]
    public void SessionAcceptedRunState_WhenAuthorizationConfigurationDiffers_ThrowsExactArgumentException()
    {
        var evidence = Evidence();

        var exception = Should.Throw<ArgumentException>(() => State(
            evidence, authorizationConfigurationVersion: new ConfigurationVersion(2)));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void OperationAcceptedSessionEntry_WhenEntryParentsItself_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var state = State(evidence);

        var exception = Should.Throw<ArgumentException>(() => new OperationAcceptedSessionEntry(
            evidence.AcceptedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId,
            new SessionSequence(4), evidence.AcceptedEntryId, DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"), state));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causalParentId");
    }

    [Fact]
    public void OperationAcceptedSessionEntry_WhenEntryReusesMaterializedIdentity_ThrowsExactArgumentException()
    {
        var evidence = Evidence();
        var state = State(evidence);

        var exception = Should.Throw<ArgumentException>(() => new OperationAcceptedSessionEntry(
            evidence.MaterializedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId,
            new SessionSequence(4), evidence.MaterializedEntryId, DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"), state));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void ValidAcceptanceContracts_WhenReconstructed_RetainTriggerAndCausalRelations()
    {
        var evidence = Evidence();
        var state = State(evidence);
        var promoted = new InputPromotedSessionEntry(
            evidence.PromotionEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId,
            new SessionSequence(2), evidence.PreviousEntryId, DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"), evidence.LaneId, evidence.AdmissionId,
            new SessionSequence(1), [evidence.AdmissionId]);
        var accepted = new OperationAcceptedSessionEntry(
            evidence.AcceptedEntryId, evidence.Address, evidence.InRunCorrelation, evidence.BranchId,
            new SessionSequence(4), evidence.MaterializedEntryId, DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"), state);

        state.InitiatingAdmissionId.ShouldBe(evidence.AdmissionId);
        promoted.InitiatingAdmissionId.ShouldBe(evidence.AdmissionId);
        accepted.State.ShouldBe(state);
    }

    private static SessionRunStartRequest StartRequest(
        AcceptanceEvidence evidence,
        SessionOperationContext context,
        ConfigurationVersion configurationVersion)
    {
        var configuration = new RunConfigurationReference(
            configurationVersion, new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var authorization = Authorization(
            evidence, evidence.InRunCorrelation, configurationVersion);
        return new SessionRunStartRequest(
            context, evidence.AdmissionId, [evidence.AdmissionId], new SessionSequence(1),
            new SessionLaneRevision(1), new SessionVersion(1),
            new SessionBranchCursor(evidence.BranchId, evidence.PreviousEntryId), null,
            evidence.RunId, evidence.TurnId, evidence.PromotionEntryId, [evidence.MaterializedEntryId],
            [evidence.MessageId], evidence.AcceptedEntryId, new OperationStateRevision(1),
            new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)),
            configuration, authorization, DateTimeOffset.UnixEpoch, new IdempotencyKey("start"));
    }

    private static SessionAcceptedRunState State(
        AcceptanceEvidence evidence,
        AdmissionId? initiatingAdmissionId = null,
        SessionBranchCursor? previousCursor = null,
        SessionBranchCursor? committedCursor = null,
        bool defaultInitiatingAdmissionId = false,
        ConfigurationVersion? authorizationConfigurationVersion = null) => new(
        evidence.Address, evidence.LaneId, new SessionLaneRevision(2), evidence.InRunCorrelation,
        new OperationStateRevision(1), evidence.Identity,
        Authorization(
            evidence,
            evidence.InRunCorrelation,
            authorizationConfigurationVersion ?? new ConfigurationVersion(1)),
        new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)),
        new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1),
            new ContentHash("sha256:configuration")),
        previousCursor ?? new SessionBranchCursor(evidence.BranchId, evidence.PreviousEntryId),
        committedCursor ?? new SessionBranchCursor(evidence.BranchId, evidence.AcceptedEntryId),
        new SessionSequence(1), defaultInitiatingAdmissionId ? default : initiatingAdmissionId ?? evidence.AdmissionId,
        [evidence.AdmissionId],
        [evidence.MaterializedEntryId], [evidence.MessageId], evidence.TurnId, DateTimeOffset.UnixEpoch);

    private static SessionOperationContext Context(
        AcceptanceEvidence evidence,
        ConfigurationVersion configurationVersion) => new(
        evidence.Address.AgentId, evidence.Address.SessionId, evidence.LaneId,
        evidence.BeforeRunCorrelation, evidence.Identity,
        Authorization(evidence, evidence.BeforeRunCorrelation, configurationVersion));

    private static SecurityAuthorizationContext Authorization(
        AcceptanceEvidence evidence,
        OperationCorrelation correlation,
        ConfigurationVersion configurationVersion) => new(
        new SecurityProfileKey("security"), new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1),
        configurationVersion,
        new SecurityAuthorizationScope(evidence.Address.AgentId, evidence.Address.SessionId, correlation),
        evidence.Identity);

    private static AcceptanceEvidence Evidence()
    {
        var operationId = new OperationId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        return new AcceptanceEvidence(
            new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())),
            new ExecutionLaneId(Guid.NewGuid()), new BranchId(Guid.NewGuid()),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new BeforeRunOperationCorrelation(operationId, null),
            new InRunOperationCorrelation(operationId, runId, turnId), runId, turnId,
            new AdmissionId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()),
            new SessionEntryId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()),
            new SessionEntryId(Guid.NewGuid()), new MessageId(Guid.NewGuid()));
    }

    private sealed record AcceptanceEvidence(
        SessionAddress Address,
        ExecutionLaneId LaneId,
        BranchId BranchId,
        ExecutionIdentity Identity,
        BeforeRunOperationCorrelation BeforeRunCorrelation,
        InRunOperationCorrelation InRunCorrelation,
        RunId RunId,
        TurnId TurnId,
        AdmissionId AdmissionId,
        SessionEntryId PreviousEntryId,
        SessionEntryId PromotionEntryId,
        SessionEntryId MaterializedEntryId,
        SessionEntryId AcceptedEntryId,
        MessageId MessageId);
}
