// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

using AgentKit.TestSupport;

/// <summary>Verifies RunContinuationContext behavior and contracts.</summary>
public sealed class RunContinuationContextTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly ExecutionLaneId _laneId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
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
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(laneId: new ExecutionLaneId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))));
        var exception = Should.Throw<ArgumentException>(() => Context(CommittedBoundary(), [cause]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenPromotionPreviousTurnDiffersFromCommittedResponse_ThrowsExactArgumentException()
    {
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(previousTurnId: new TurnId(Guid.Parse("88888888-8888-8888-8888-888888888888"))));
        var exception = Should.Throw<ArgumentException>(() => Context(CommittedBoundary(), [cause]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenCheckpointPromotionTargetsDifferentRetryTurn_ThrowsExactArgumentException()
    {
        var retryTurnId = new TurnId(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var boundary = new RetryContinuationBoundary(retryTurnId, new ModelRequestId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(boundary: PromotionBoundary.AfterContinuationCheckpoint, previousTurnId: null, targetTurnId: _targetTurnId));
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

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var boundary = new IdleContinuationBoundary();
        var context = Context(boundary, []);
        context.AgentId.ShouldBe(_agentId);
        context.SessionId.ShouldBe(_sessionId);
        context.ExecutionLaneId.ShouldBe(_laneId);
        context.OperationId.ShouldBe(_operationId);
        context.RunId.ShouldBe(_runId);
        context.State.ShouldBe(AgentRunState.Driving);
        context.OperationStateRevision.ShouldBe(_revision);
        context.BranchCursor.ShouldBe(Cursor());
        context.InputPromotionCutoff.ShouldBe(_cutoff);
        context.ConfigurationVersion.ShouldBe(new ConfigurationVersion(2));
        context.PolicyVersion.ShouldBe(new RunPolicyVersion(1));
        context.Boundary.ShouldBe(boundary);
        context.RequiredStopOutcome.ShouldBeNull();
        context.Causes.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenRequiredStopOutcomeIsSuccessful_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RunContinuationContext(
            _agentId, _sessionId, _laneId, _operationId, _runId, AgentRunState.Driving, _revision, Cursor(), _cutoff,
            new ConfigurationVersion(2), new RunPolicyVersion(1), new IdleContinuationBoundary(), new AgentRunIdle(), []));
        exception.ParamName.ShouldBe("requiredStopOutcome");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Context(new IdleContinuationBoundary(), []);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Constructor_WhenCausesRepeatSameInstance_ThrowsExactArgumentException()
    {
        var cause = new DeferredCompletionContinuationCause(_operationId);
        var exception = Should.Throw<ArgumentException>(() => Context(new IdleContinuationBoundary(), [cause, cause]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenCommittedBoundaryResponseCorrelationMismatches_ThrowsExactArgumentException()
    {
        var boundary = CommittedBoundary();
        var otherRunId = new RunId(Guid.Parse("ff000000-0000-0000-0000-000000000001"));
        var exception = Should.Throw<ArgumentException>(() => new RunContinuationContext(
            _agentId, _sessionId, _laneId, _operationId, otherRunId, AgentRunState.Driving, _revision, Cursor(), _cutoff,
            new ConfigurationVersion(2), new RunPolicyVersion(1), boundary, requiredStopOutcome: null, []));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("boundary");
    }

    [Fact]
    public void Constructor_WhenPromotedCauseTargetsIdleBoundary_AcceptsEvidence()
    {
        var boundary = new IdleContinuationBoundary();
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(boundary: PromotionBoundary.OtherwiseIdle, previousTurnId: null));
        var context = Context(boundary, [cause]);
        _ = context.Causes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_WhenPromotedCauseTargetsDeferredBoundaryAndTurnsMatch_AcceptsEvidence()
    {
        var deferredTurnId = _targetTurnId;
        var deferredModelRequestId = new ModelRequestId(Guid.Parse("ffffffff-0000-0000-0000-000000000001"));
        var boundary = new DeferredContinuationBoundary(deferredTurnId, deferredModelRequestId, _operationId);
        var cause = new PromotedInputContinuationCause(PromotionSnapshot(boundary: PromotionBoundary.AfterContinuationCheckpoint, previousTurnId: null, targetTurnId: deferredTurnId));
        var context = Context(boundary, [cause], AgentRunState.SuspendedDeferred);
        _ = context.Causes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_WhenCommittedToolResultsCauseDoesNotMatchBoundary_ThrowsExactArgumentException()
    {
        var callId = new ToolCallId(Guid.Parse("11110000-0000-0000-0000-000000000001"));
        var boundary = CommittedBoundaryWithToolCall(callId);
        var mismatchedCause = new CommittedToolResultsContinuationCause([
            new CommittedToolResultReference(
                new SessionEntryId(Guid.Parse("22220000-0000-0000-0000-000000000001")),
                new ToolCallId(Guid.Parse("33330000-0000-0000-0000-000000000001")),
                _committedTurnId),
        ]);
        var exception = Should.Throw<ArgumentException>(() => Context(boundary, [mismatchedCause]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenOutputRepairCauseDoesNotMatchBoundaryDecision_ThrowsExactArgumentException()
    {
        var boundary = CommittedBoundary();
        var decision = new OutputRetryRequired(
            new OutputRepairInstruction("retry with corrections"),
            new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "invalid", []));
        var cause = new OutputRepairContinuationCause(decision);
        var exception = Should.Throw<ArgumentException>(() => Context(boundary, [cause]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void Constructor_WhenDeferredCompletionCauseDoesNotMatchBoundary_ThrowsExactArgumentException()
    {
        var boundary = CommittedBoundary();
        var cause = new DeferredCompletionContinuationCause(_operationId);
        var exception = Should.Throw<ArgumentException>(() => Context(boundary, [cause]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    private static CommittedTurnContinuationBoundary CommittedBoundaryWithToolCall(ToolCallId callId)
    {
        var requestId = new ModelRequestId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var parts = new ContentPart[]
        {
            new ToolCallPart(callId, new ToolReference(new ToolAlias("tool"), null, null), default, null, ExtensionData.Empty),
        }.ToImmutableArray();
        var response = new AssistantMessage(
            new MessageId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), _agentId, _sessionId, conversationId: null, _branchId, _runId, _committedTurnId,
            DateTimeOffset.UnixEpoch, MessageState.Complete, parts,
            new AssistantResponseMetadata(requestId, new ProviderResponseIdentity(new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("test-model"), new ModelId("test-model"), null, null, null), NormalizedStopReason.Completed, rawStopReason: null, ModelUsage.NotReported, ExtensionData.Empty),
            ExtensionData.Empty);
        var toolResults = ImmutableArray.Create(new CommittedToolResultReference(
            new SessionEntryId(Guid.Parse("44440000-0000-0000-0000-000000000001")),
            callId,
            _committedTurnId));
        return new CommittedTurnContinuationBoundary(response, toolResults, outputDecision: null, requiresOutputValidation: false);
    }

    private static RunContinuationContext Context(RunContinuationBoundary boundary, ImmutableArray<RunContinuationCause> causes, AgentRunState state = AgentRunState.Driving) => new(_agentId, _sessionId, _laneId, _operationId, _runId, state, _revision, Cursor(), _cutoff, new ConfigurationVersion(2), new RunPolicyVersion(1), boundary, requiredStopOutcome: null, causes);
    private static RunContinuationContext ContextWithDefault(string parameter) => new(parameter == "agentId" ? default : _agentId, parameter == "sessionId" ? default : _sessionId, parameter == "executionLaneId" ? default : _laneId, parameter == "operationId" ? default : _operationId, parameter == "runId" ? default : _runId, AgentRunState.Driving, parameter == "operationStateRevision" ? default : _revision, Cursor(), _cutoff, parameter == "configurationVersion" ? default : new ConfigurationVersion(2), parameter == "policyVersion" ? default : new RunPolicyVersion(1), new IdleContinuationBoundary(), requiredStopOutcome: null, []);
    private static InputPromotionSnapshot PromotionSnapshot(ExecutionLaneId? laneId = null, SessionSequence? cutoff = null, PromotionBoundary boundary = PromotionBoundary.AfterTurnCommitted, TurnId? previousTurnId = null, TurnId? targetTurnId = null) => new(_agentId, _sessionId, laneId ?? _laneId, new InRunOperationCorrelation(_operationId, _runId, _committedTurnId), _revision, Cursor(), cutoff ?? _cutoff, expectedVersion: null, expectedFencingToken: null, boundary, previousTurnId ?? (boundary == PromotionBoundary.AfterTurnCommitted ? _committedTurnId : null), targetTurnId ?? _targetTurnId, [new AdmissionId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))]);
    private static SessionBranchCursor Cursor() => new(_branchId, new SessionEntryId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
    private static CommittedTurnContinuationBoundary CommittedBoundary()
    {
        var requestId = new ModelRequestId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var response = new AssistantMessage(new MessageId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), _agentId, _sessionId, conversationId: null, _branchId, _runId, _committedTurnId, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty)], new AssistantResponseMetadata(requestId, new ProviderResponseIdentity(new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("test-model"), new ModelId("test-model"), null, null, null), NormalizedStopReason.Completed, rawStopReason: null, ModelUsage.NotReported, ExtensionData.Empty), ExtensionData.Empty);
        return new CommittedTurnContinuationBoundary(response, [], outputDecision: null, requiresOutputValidation: false);
    }

    private static readonly OperationId _installedOperationId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly OperationId _compactionOperationId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static readonly RunId _runIdCompactionRetryContinuationCause = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static readonly TurnId _turnId = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));
    private static readonly ModelRequestId _requestId = new(Guid.Parse("77777777-7777-7777-7777-777777777777"));
    private static readonly BranchId _branchIdCompactionRetryContinuationCause = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));
    [Fact]
    public void RunContinuationContext_WhenManifestBelongsToForeignBranch_ThrowsExactArgumentException()
    {
        var compaction = Compaction();
        var foreign = compaction with
        {
            Record = compaction.Record with
            {
                Manifest = compaction.Record.Manifest with
                {
                    BranchId = new BranchId(Guid.Parse("99999999-9999-9999-9999-999999999999")),
                },
            },
        };
        var cause = new CompactionRetryContinuationCause(_requestId, foreign);
        var exception = Should.Throw<ArgumentException>(() => ContextCompactionRetryContinuationCause(cause));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void RunContinuationContext_WhenCompactionUsesDistinctSuboperation_AcceptsCausalEvidence()
    {
        var cause = new CompactionRetryContinuationCause(_requestId, Compaction());
        var context = ContextCompactionRetryContinuationCause(cause);
        context.OperationId.ShouldBe(_installedOperationId);
        cause.Compaction.Context.Correlation.OperationId.ShouldBe(_compactionOperationId);
        cause.Compaction.Context.Correlation.OperationId.ShouldNotBe(context.OperationId);
    }

    private static RunContinuationContext ContextCompactionRetryContinuationCause(CompactionRetryContinuationCause cause) => new(_agentId, _sessionId, _laneId, _installedOperationId, _runIdCompactionRetryContinuationCause, AgentRunState.WaitingRetry, new OperationStateRevision(4), new SessionBranchCursor(_branchIdCompactionRetryContinuationCause, new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))), new SessionSequence(12), new ConfigurationVersion(2), new RunPolicyVersion(1), new RetryContinuationBoundary(_turnId, _requestId), requiredStopOutcome: null, [cause]);
    private static CompactionSucceeded Compaction()
    {
        var context = ContextValue();
        var manifest = new CompactionManifest(new CompactionManifestId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), context, _branchIdCompactionRetryContinuationCause, new SessionVersion(3), new CompactionSourceRange(new SessionSequence(1), new SessionSequence(5)), new SessionSequence(6), new CompactionProducer(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty), new ContextEpoch(1), new CompactionSizeEstimate(20, 100, 1), new CompactionSizeEstimate(5, 25, 1), DateTimeOffset.UnixEpoch, ExtensionData.Empty);
        var checkpoint = new CompactionCheckpoint([new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var record = new CompactionRecord(context, new SessionVersion(3), new SessionVersion(4), CompactionRecordStatus.Active, manifest, checkpoint, supersedes: null, rejection: null, DateTimeOffset.UnixEpoch, ExtensionData.Empty);
        return new CompactionSucceeded(context, record);
    }

    private static CompactionOperationContext ContextValue() => TestSecurityEvidence.CompactionContext(new CompactionId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), _agentId, _sessionId, new InRunOperationCorrelation(_compactionOperationId, _runIdCompactionRetryContinuationCause, _turnId), TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
}
