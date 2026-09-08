// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

public sealed class RunContinuationContextTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly ExecutionLaneId _laneId = new("main");
    private static readonly OperationId _operationId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly RunId _runId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static readonly BranchId _branchId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static readonly TurnId _committedTurnId = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));
    private static readonly TurnId _targetTurnId = new(Guid.Parse("77777777-7777-7777-7777-777777777777"));
    private static readonly OperationStateRevision _revision = new(3);
    private static readonly SessionSequence _cutoff = new(8);

    [Fact]
    public void Constructor_WhenPromotedTargetIsDistinctFromCommittedTurn_AcceptsNextTurnEvidence()
    {
        var boundary = CommittedBoundary();
        var promotion = new PromotedInputContinuationCause(PromotionSnapshot());

        var context = Context(boundary, [promotion]);

        context.Causes.ShouldHaveSingleItem().ShouldBeSameAs(promotion);
        promotion.Snapshot.PreviousTurnId.ShouldBe(_committedTurnId);
        promotion.Snapshot.TargetTurnId.ShouldBe(_targetTurnId);
        ((TurnId?) promotion.Snapshot.TargetTurnId).ShouldNotBe(promotion.Snapshot.PreviousTurnId);
    }

    [Fact]
    public void Constructor_WhenPromotionCutoffDiffers_ThrowsExactArgumentException()
    {
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(cutoff: new SessionSequence(9)));

        var exception = Should.Throw<ArgumentException>(() => Context(CommittedBoundary(), [cause]));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenPromotionLaneDiffers_ThrowsExactArgumentException()
    {
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(laneId: new ExecutionLaneId("other")));

        var exception = Should.Throw<ArgumentException>(() => Context(CommittedBoundary(), [cause]));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenPromotionPreviousTurnDiffersFromCommittedResponse_ThrowsExactArgumentException()
    {
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(
            previousTurnId: new TurnId(Guid.Parse("88888888-8888-8888-8888-888888888888"))));

        var exception = Should.Throw<ArgumentException>(() => Context(CommittedBoundary(), [cause]));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenCheckpointPromotionTargetsDifferentRetryTurn_ThrowsExactArgumentException()
    {
        var retryTurnId = new TurnId(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var boundary = new RetryContinuationBoundary(retryTurnId, new ModelRequestId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(
            boundary: PromotionBoundary.AfterContinuationCheckpoint,
            previousTurnId: null,
            targetTurnId: _targetTurnId));

        var exception = Should.Throw<ArgumentException>(() => Context(boundary, [cause], AgentRunState.WaitingRetry));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("executionLaneId")]
    [InlineData("operationId")]
    [InlineData("runId")]
    [InlineData("operationStateRevision")]
    [InlineData("configurationVersion")]
    [InlineData("policyVersion")]
    public void Constructor_WhenRequiredIdentityOrVersionIsDefault_ThrowsExactArgumentOutOfRangeException(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ContextWithDefault(parameter));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }

    private static RunContinuationContext Context(
        RunContinuationBoundary boundary,
        ImmutableArray<RunContinuationCause> causes,
        AgentRunState state = AgentRunState.Driving) =>
        new(
            _agentId,
            _sessionId,
            _laneId,
            _operationId,
            _runId,
            state,
            _revision,
            Cursor(),
            _cutoff,
            new ConfigurationVersion(2),
            new RunPolicyVersion(1),
            boundary,
            requiredStopOutcome: null,
            causes);

    private static RunContinuationContext ContextWithDefault(string parameter) =>
        new(
            parameter == "agentId" ? default : _agentId,
            parameter == "sessionId" ? default : _sessionId,
            parameter == "executionLaneId" ? default : _laneId,
            parameter == "operationId" ? default : _operationId,
            parameter == "runId" ? default : _runId,
            AgentRunState.Driving,
            parameter == "operationStateRevision" ? default : _revision,
            Cursor(),
            _cutoff,
            parameter == "configurationVersion" ? default : new ConfigurationVersion(2),
            parameter == "policyVersion" ? default : new RunPolicyVersion(1),
            new IdleContinuationBoundary(),
            requiredStopOutcome: null,
            []);

    private static InputPromotionSnapshot PromotionSnapshot(
        ExecutionLaneId? laneId = null,
        SessionSequence? cutoff = null,
        PromotionBoundary boundary = PromotionBoundary.AfterTurnCommitted,
        TurnId? previousTurnId = null,
        TurnId? targetTurnId = null) =>
        new(
            _agentId,
            _sessionId,
            laneId ?? _laneId,
            new InRunOperationCorrelation(_operationId, _runId, _committedTurnId),
            _revision,
            Cursor(),
            cutoff ?? _cutoff,
            expectedVersion: null,
            expectedFencingToken: null,
            boundary,
            previousTurnId ?? (boundary == PromotionBoundary.AfterTurnCommitted ? _committedTurnId : null),
            targetTurnId ?? _targetTurnId,
            [new AdmissionId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))]);

    private static SessionBranchCursor Cursor() =>
        new(_branchId, new SessionEntryId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));

    private static CommittedTurnContinuationBoundary CommittedBoundary()
    {
        var requestId = new ModelRequestId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var response = new AssistantMessage(
            new MessageId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")),
            _agentId,
            _sessionId,
            conversationId: null,
            _branchId,
            _runId,
            _committedTurnId,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty)],
            new AssistantResponseMetadata(
                requestId,
                new ProviderResponseIdentity(
                    new ProviderId("test"),
                    null,
                    new ApiFamilyId("test"),
                    new ModelId("test-model"),
                    new ModelId("test-model"),
                    null,
                    null,
                    null),
                NormalizedStopReason.Completed,
                rawStopReason: null,
                ModelUsage.Empty,
                ExtensionData.Empty),
            ExtensionData.Empty);
        return new CommittedTurnContinuationBoundary(response, [], outputDecision: null, requiresOutputValidation: false);
    }
}
