// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using AgentKit.Conformance;

/// <summary>Runs continuation-policy conformance and first-party precedence cases.</summary>
[Collection(ContinuationObservationGroup.Name)]
public sealed class DefaultRunContinuationPolicyTests:
    RunContinuationPolicyConformanceTests<DefaultRunContinuationPolicyTests.Fixture>
{
    /// <inheritdoc/>
    protected override Fixture CreateFixture() => new();

    [Fact]
    public async Task DecideAsync_WhenPromotedInputAndOutputRepairCoexist_SelectsInputAndRetainsRepair()
    {
        var fixture = CreateFixture();
        var retry = CreateRetry();
        var boundary = fixture.CreateCommittedBoundary(retry, requiresOutput: true);
        var promotion = new PromotedInputContinuationCause(fixture.CreatePromotion(boundary.Response.TurnId!.Value));
        var repair = new OutputRepairContinuationCause(retry);

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(boundary, [repair, promotion]), TestContext.Current.CancellationToken);

        var reason = decision.ShouldBeOfType<ContinueRun>().Reason;
        reason.SelectedCause.ShouldBeSameAs(promotion);
        reason.OtherPendingCauses.ShouldBe([repair]);
    }

    [Fact]
    public async Task DecideAsync_WhenRequiredOutputIsMissingAndExplicitCauseExists_HaltsInvalidState()
    {
        var fixture = CreateFixture();
        var context = fixture.CreateContext(
            fixture.CreateCommittedBoundary(decision: null, requiresOutput: true),
            [new ExplicitPolicyContinuationCause("follow-up")]);

        var decision = await fixture.Policy.DecideAsync(context, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunInvalidState>();
    }

    [Fact]
    public async Task DecideAsync_WhenConfigurationRejectedAndPromotedInputExists_HaltsOutput()
    {
        var fixture = CreateFixture();
        var rejected = new OutputConfigurationRejected(new OutputSchemaConfigurationFailure(
            OutputSchemaConfigurationFailureKind.MalformedSchema, "Invalid schema.", []));
        var boundary = fixture.CreateCommittedBoundary(rejected, requiresOutput: true);
        var promoted = new PromotedInputContinuationCause(fixture.CreatePromotion(boundary.Response.TurnId!.Value));

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(boundary, [promoted]), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunOutputRejected>().Rejection.ShouldBeSameAs(rejected);
    }

    [Theory]
    [InlineData(AgentRunState.AwaitingModel)]
    [InlineData(AgentRunState.StreamingModel)]
    [InlineData(AgentRunState.RecordingToolCalls)]
    [InlineData(AgentRunState.AwaitingTools)]
    [InlineData(AgentRunState.CommittingToolResults)]
    public async Task DecideAsync_WhenEffectsAreActive_DoesNotCompleteFalseIdle(AgentRunState state)
    {
        var fixture = CreateFixture();

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), [], state: state),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunInvalidState>();
    }

    [Fact]
    public async Task DecideAsync_WhenModelIsStreaming_DoesNotCompleteCommittedBoundary()
    {
        var fixture = CreateFixture();

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(fixture.CreateCommittedBoundary(), [], state: AgentRunState.StreamingModel),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunInvalidState>();
    }

    [Fact]
    public async Task DecideAsync_WhenActivityStartListenerThrows_PreservesSemanticDecision()
    {
        using var listener = CreateThrowingListener(throwOnStart: true);
        ActivitySource.AddActivityListener(listener);
        var fixture = CreateFixture();

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    [Fact]
    public async Task DecideAsync_WhenActivityStopListenerThrows_PreservesSemanticDecisionAndAmbientActivity()
    {
        using var parent = new Activity("parent").Start();
        using var listener = CreateThrowingListener(throwOnStart: false);
        ActivitySource.AddActivityListener(listener);
        var fixture = CreateFixture();

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void CompleteRun_WhenOutcomeIsNotSuccessful_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => _ = new CompleteRun(new AgentRunTurnLimitReached(1)));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void HaltRun_WhenOutcomeIsSuccessful_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => _ = new HaltRun(new AgentRunIdle()));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("outcome");
    }

    private static OutputRetryRequired CreateRetry() => new(
        new OutputRepairInstruction("Repair output."),
        new OutputValidationFailure(OutputValidationFailureKind.ValidatorFailed, "Rejected.", []));

    private static ActivityListener CreateThrowingListener(bool throwOnStart) => new()
    {
        ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
        Sample = SampleAll,
        ActivityStarted = throwOnStart ? static _ => throw new InvalidOperationException("observer") : null,
        ActivityStopped = throwOnStart ? null : static _ => throw new InvalidOperationException("observer"),
    };

    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> options) =>
        ActivitySamplingResult.AllData;

    /// <summary>Composes the first-party policy with one stable run correlation.</summary>
    public sealed class Fixture: IRunContinuationPolicyConformanceFixture
    {
        private readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        private readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        private readonly ExecutionLaneId _laneId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        private readonly OperationId _operationId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        private readonly RunId _runId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        private readonly BranchId _branchId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        private readonly OperationStateRevision _revision = new(1);
        private readonly SessionSequence _cutoff = new(7);

        /// <inheritdoc/>
        public IRunContinuationPolicy Policy { get; } = new DefaultRunContinuationPolicy(TimeProvider.System);

        /// <inheritdoc/>
        public RunContinuationContext CreateContext(
            RunContinuationBoundary boundary,
            ImmutableArray<RunContinuationCause> causes,
            AgentRunOutcome? requiredStop = null,
            AgentRunState state = AgentRunState.Driving) =>
            new(
                _agentId,
                _sessionId,
                _laneId,
                _operationId,
                _runId,
                state,
                _revision,
                new SessionBranchCursor(_branchId, new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))),
                _cutoff,
                new ConfigurationVersion(1),
                new RunPolicyVersion(1),
                boundary,
                requiredStop,
                causes);

        /// <inheritdoc/>
        public CommittedTurnContinuationBoundary CreateCommittedBoundary(
            OutputProcessingResult? decision = null,
            bool requiresOutput = false)
        {
            var turnId = new TurnId(Guid.Parse("66666666-6666-6666-6666-666666666666"));
            var requestId = new ModelRequestId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
            var response = TestFactory.Response(requestId, [new TextPart("answer", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Completed);
            var message = new AssistantMessage(
                new MessageId(Guid.Parse("88888888-8888-8888-8888-888888888888")),
                _agentId,
                _sessionId,
                null,
                _branchId,
                _runId,
                turnId,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                response.Parts,
                new AssistantResponseMetadata(response.RequestId, response.Identity, response.StopReason, null, response.Usage, ExtensionData.Empty),
                ExtensionData.Empty);
            return new CommittedTurnContinuationBoundary(message, [], decision, requiresOutput);
        }

        /// <summary>Creates promotion evidence correlated with this fixture's committed turn.</summary>
        public InputPromotionSnapshot CreatePromotion(TurnId turnId) => new(
            _agentId,
            _sessionId,
            _laneId,
            new InRunOperationCorrelation(_operationId, _runId, turnId),
            _revision,
            new SessionBranchCursor(_branchId, new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))),
            _cutoff,
            expectedVersion: null,
            expectedFencingToken: null,
            PromotionBoundary.AfterTurnCommitted,
            turnId,
            new TurnId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            [new AdmissionId(Guid.Parse("99999999-9999-9999-9999-999999999999"))]);
    }
}
