// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

public sealed class DefaultAgentLoopTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenSessionCoordinatorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultAgentLoop(
            null!,
            new FakeSecurityProfileSelector(),
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            FakeModelSelector.Selecting(TestFactory.Model()),
            new FakeLlmModelResolver(new RespondingLlmModel(
                new ModelAlias("chat"),
                modelRequest => TestFactory.CompletedWithText(modelRequest.Context.ModelRequestId))),
            new DefaultRunContinuationPolicy(TimeProvider.System),
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions())));

        exception.ParamName.ShouldBe("sessionCoordinator");
    }

    [Fact]
    public void Constructor_WhenModelSelectorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultAgentLoop(
            new FakeSessionCoordinator(_branchId),
            new FakeSecurityProfileSelector(),
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            null!,
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))),
            new DefaultRunContinuationPolicy(TimeProvider.System),
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions())));

        exception.ParamName.ShouldBe("modelSelector");
    }

    [Fact]
    public void Constructor_WhenContinuationPolicyIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultAgentLoop(
            new FakeSessionCoordinator(_branchId),
            new FakeSecurityProfileSelector(),
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            FakeModelSelector.Selecting(TestFactory.Model()),
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))),
            null!,
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions())));

        exception.ParamName.ShouldBe("continuationPolicy");
    }

    [Fact]
    public async Task RunAsync_WhenTurnRequestsNoTool_OffersTheCommittedTurnToTheContinuationPolicy()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var policy = new ScriptedRunContinuationPolicy(static context =>
            new CompleteRun(new AgentRunCompleted(((CommittedTurnContinuationBoundary) context.Boundary).Response)));
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var context = policy.Contexts.ShouldHaveSingleItem();
        context.RunId.ShouldBe(request.RunId);
        context.AgentId.ShouldBe(_agentId);
        context.SessionId.ShouldBe(_sessionId);
        context.State.ShouldBe(AgentRunState.Driving);
        context.OperationStateRevision.ShouldBe(new OperationStateRevision(1));
        context.BranchCursor.BranchId.ShouldBe(_branchId);
        context.BranchCursor.LastEntryId.ShouldBe(coordinator.Entries[^1].Id);
        context.ConfigurationVersion.ShouldBe(request.Authorization.ConfigurationVersion);
        context.RequiredStopOutcome.ShouldBeNull();
        context.Causes.ShouldBeEmpty();
        var boundary = context.Boundary.ShouldBeOfType<CommittedTurnContinuationBoundary>();
        boundary.Response.ShouldBeSameAs(result.NewMessages[0]);
        boundary.ToolResults.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyHaltsAfterNoToolTurn_SettlesWithTheHaltOutcome()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var halt = new AgentRunInvalidState("policy halt");
        var policy = new ScriptedRunContinuationPolicy(_ => new HaltRun(halt));
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeSameAs(halt);
        result.NewMessages.ShouldHaveSingleItem().State.ShouldBe(MessageState.Complete);
        result.FinalVersion.ShouldBe(coordinator.Version);
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyHaltsAfterToolResults_SettlesWithoutAnotherModelRequest()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var halt = new AgentRunInvalidState("halt after tools");
        var policy = new ScriptedRunContinuationPolicy(_ => new HaltRun(halt));
        var modelCalls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out var invoker,
            _ => ++modelCalls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeSameAs(halt);
        modelCalls.ShouldBe(1);
        _ = invoker.ReceivedRequests.ShouldHaveSingleItem();
        result.NewMessages.Length.ShouldBe(2);
        var context = policy.Contexts.ShouldHaveSingleItem();
        var boundary = context.Boundary.ShouldBeOfType<CommittedTurnContinuationBoundary>();
        var reference = boundary.ToolResults.ShouldHaveSingleItem();
        reference.ToolCallId.ShouldBe(callId);
        reference.SessionEntryId.ShouldBe(coordinator.Entries[^1].Id);
        reference.TurnId.ShouldBe(boundary.Response.TurnId!.Value);
        context.Causes.ShouldHaveSingleItem().ShouldBeOfType<CommittedToolResultsContinuationCause>()
            .ToolResults.ShouldBe(boundary.ToolResults);
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyCompletesAfterToolResults_SettlesWithTheProposedOutcome()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var policy = new ScriptedRunContinuationPolicy(static context =>
            new CompleteRun(new AgentRunCompleted(((CommittedTurnContinuationBoundary) context.Boundary).Response)));
        var modelCalls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => ++modelCalls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeOfType<AgentRunCompleted>().FinalMessage.ShouldBeSameAs(result.NewMessages[0]);
        modelCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyContinuesAfterNoToolTurn_RunsAnotherTurn()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var evaluations = 0;
        var policy = new ScriptedRunContinuationPolicy(context => ++evaluations == 1
            ? new ContinueRun(new ContinuationReason(new ExplicitPolicyContinuationCause("keep-going"), []))
            : new CompleteRun(new AgentRunCompleted(((CommittedTurnContinuationBoundary) context.Boundary).Response)));
        var modelCalls = 0;
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ =>
            {
                modelCalls++;
                return TestFactory.CompletedWithText(requestId);
            },
            contextAssembler: assembler,
            continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        modelCalls.ShouldBe(2);
        result.NewMessages.Length.ShouldBe(2);
        assembler.Requests[1].History.Length.ShouldBe(2);
        policy.Contexts[1].OperationStateRevision.ShouldBe(new OperationStateRevision(2));
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyContinuesOnTheFinalTurn_ReturnsAgentRunTurnLimitReached()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var policy = new ScriptedRunContinuationPolicy(static _ =>
            new ContinueRun(new ContinuationReason(new ExplicitPolicyContinuationCause("keep-going"), [])));
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeOfType<AgentRunTurnLimitReached>().MaxTurns.ShouldBe(1);
        _ = result.NewMessages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyObservesCancellation_ReturnsAgentRunCancelledWithCommittedMessages()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        var policy = new ScriptedRunContinuationPolicy(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token);

        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        _ = result.NewMessages.ShouldHaveSingleItem();
        result.FinalVersion.ShouldBe(coordinator.Version);
    }

    [Theory]
    [InlineData(0, 5, 30, 5)]
    [InlineData(200, -1, 30, 5)]
    [InlineData(200, 5, 0, 5)]
    [InlineData(200, 5, 30, 0)]
    public void Constructor_WhenOptionsCarryAnImpossibleLimit_ThrowsArgumentOutOfRangeException(
        int pageSize, int retryLimit, int settlementSeconds, int observerSeconds)
    {
        var options = new AgentLoopOptions
        {
            HistoryReadPageSize = pageSize,
            AppendConflictRetryLimit = retryLimit,
            SettlementTimeout = TimeSpan.FromSeconds(settlementSeconds),
            ObserverDeliveryTimeout = TimeSpan.FromSeconds(observerSeconds),
        };

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DefaultAgentLoop(
            new FakeSessionCoordinator(_branchId),
            new FakeSecurityProfileSelector(),
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            FakeModelSelector.Selecting(TestFactory.Model()),
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))),
            new DefaultRunContinuationPolicy(TimeProvider.System),
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(options)));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task RunAsync_WhenAppendConflictRetryLimitIsZero_FailsOnTheFirstConflictWithoutRetrying()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId),
            options: new AgentLoopOptions { AppendConflictRetryLimit = 0 });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.ConditionalAppendOverride = request =>
        {
            coordinator.SimulateConcurrentAppend([TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, 2)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenAppendConflictsMoreThanTheRetryLimit_GivesUpAfterTheConfiguredRetries()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId),
            options: new AgentLoopOptions { AppendConflictRetryLimit = 2 });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.ConditionalAppendOverride = request =>
        {
            coordinator.SimulateConcurrentAppend([TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, coordinator.NextSequence + 1)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        coordinator.ReceivedAppends.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_WhenTheToolMessageCommitNeverCompletes_SettlesAsSessionOperationFailedAtTheSettlementTimeout()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var clock = new FakeTimeProvider();
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithToolCall(requestId, callId),
            options: new AgentLoopOptions { SettlementTimeout = TimeSpan.FromSeconds(30) },
            timeProvider: clock);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.StallAppend = static request => request.IdempotencyKey.Value.EndsWith(":tools", StringComparison.Ordinal);

        var run = loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);
        await coordinator.AppendStalled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        run.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(29));
        run.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1));

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        failed.SafeMessage.ShouldContain("unknown");
        _ = result.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        result.FinalVersion.ShouldBe(new SessionVersion(2));
    }

    [Fact]
    public async Task RunAsync_WhenTheInterruptedCommitNeverCompletes_SettlesAsSessionOperationFailedAtTheSettlementTimeout()
    {
        var clock = new FakeTimeProvider();
        var loop = CreateLoop(
            out var coordinator, out _, _ => new ModelAttemptFailed(
                TestFactory.Failure(), [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)], null),
            options: new AgentLoopOptions { SettlementTimeout = TimeSpan.FromSeconds(5) },
            timeProvider: clock);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.StallAppend = static _ => true;

        var run = loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);
        await coordinator.AppendStalled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(5));

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenADetachedObserverDeliveryStalls_ContinuesAfterTheObserverDeliveryTimeout()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var clock = new FakeTimeProvider();
        var observer = new RecordingAgentRunObserver
        {
            StallDelivery = static runEvent => runEvent is AgentRunToolCallCompleted,
        };
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            options: new AgentLoopOptions { ObserverDeliveryTimeout = TimeSpan.FromSeconds(5) },
            timeProvider: clock);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var run = loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer },
            TestContext.Current.CancellationToken);
        await observer.DeliveryStalled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        run.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(5));

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        result.NewMessages.Length.ShouldBe(3);
        _ = observer.Events.OfType<AgentRunToolCallCompleted>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenHistoryEndsWithAnUnsettledAssistantToolCall_SettlesItBeforeTheFirstTurn()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out var invoker, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        var dangling = TestFactory.SeedAssistantToolCallEntry(_agentId, _sessionId, _branchId, 2, callId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1), dangling]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        invoker.ReceivedRequests.ShouldBeEmpty();
        result.NewMessages.Length.ShouldBe(2);
        var settlement = result.NewMessages[0].ShouldBeOfType<ToolMessage>();
        settlement.RunId.ShouldBe(request.RunId);
        var settled = settlement.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolResultPart>();
        settled.CallId.ShouldBe(callId);
        settled.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Cancelled);
        settled.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Interrupted);
        settled.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        settled.Outcome.Retryable.ShouldBeFalse();
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(3);
        history[2].ShouldBeSameAs(settlement);
        var settlementEntry = coordinator.Entries[2].ShouldBeOfType<MessageSessionEntry>();
        settlementEntry.CausalParentId.ShouldBe(dangling.Id);
        settlementEntry.Correlation.ShouldBeOfType<InRunOperationCorrelation>().RunId.ShouldBe(request.RunId);
        coordinator.ReceivedAppends[0].IdempotencyKey.Value.ShouldContain(dangling.Message.Id.ToString());
        coordinator.ReceivedAppends[0].ExpectedVersion.ShouldBe(new SessionVersion(1));
    }

    [Fact]
    public async Task RunAsync_WhenTheToolMessageCommitFailsInOneRun_TheNextRunOnTheSameBranchSettlesTheCallAndSucceeds()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out var invoker,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            contextAssembler: assembler,
            options: new AgentLoopOptions { SettlementTimeout = TimeSpan.FromMilliseconds(250) });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.ConditionalAppendOverride = static request =>
            request.IdempotencyKey.Value.EndsWith(":tools", StringComparison.Ordinal) ? new SessionAppendFailed("store fault") : null;

        var first = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);
        _ = first.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        _ = first.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        coordinator.ConditionalAppendOverride = null;

        var second = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = second.Outcome.ShouldBeOfType<AgentRunCompleted>();
        _ = invoker.ReceivedRequests.ShouldHaveSingleItem();
        var secondRunHistory = assembler.Requests[^1].History;
        secondRunHistory.Length.ShouldBe(3);
        secondRunHistory[2].ShouldBeOfType<ToolMessage>().Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolResultPart>()
            .CallId.ShouldBe(callId);
        second.NewMessages.Length.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenRecoveryHasAlreadySettledTheDanglingCall_DoesNotSettleItAgain()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedAssistantToolCallEntry(_agentId, _sessionId, _branchId, 2, callId),
        ]);

        _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);
        var second = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = second.Outcome.ShouldBeOfType<AgentRunCompleted>();
        _ = second.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        coordinator.Entries.OfType<MessageSessionEntry>().Select(static entry => entry.Message).OfType<ToolMessage>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenTheRecoverySettlementCannotBeCommitted_ReturnsAgentRunSessionOperationFailedWithoutAModelRequest()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var modelCalls = 0;
        var loop = CreateLoop(out var coordinator, out _, _ =>
        {
            modelCalls++;
            return TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid()));
        });
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedAssistantToolCallEntry(_agentId, _sessionId, _branchId, 2, callId),
        ]);
        coordinator.AppendOverride = static _ => new SessionAppendFailed("store fault");

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        modelCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhenTheToolMessageCommitFailsOnce_RetriesUnderTheSameIdempotencyKeyWithinTheSettlementBound()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var clock = new FakeTimeProvider();
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            timeProvider: clock);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var failed = false;
        coordinator.ConditionalAppendOverride = request =>
        {
            if (failed || !request.IdempotencyKey.Value.EndsWith(":tools", StringComparison.Ordinal))
            {
                return null;
            }

            failed = true;
            return new SessionAppendFailed("transient store fault");
        };

        var run = loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);
        // The retry backoff is measured on the fake clock; advance it in small steps until the run settles so the
        // test never depends on when the delay's timer is registered.
        for (var i = 0; i < 100 && !run.IsCompleted; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var toolAppends = coordinator.ReceivedAppends.Where(static request => request.IdempotencyKey.Value.EndsWith(":tools", StringComparison.Ordinal)).ToArray();
        toolAppends.Length.ShouldBe(2);
        toolAppends[0].IdempotencyKey.ShouldBe(toolAppends[1].IdempotencyKey);
        result.NewMessages.Length.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var loop = CreateLoop(out _, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => loop.RunAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task RunAsync_WhenHistoryCannotBeLoaded_ReturnsAgentRunSessionOperationFailedWithoutAVersion()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        coordinator.ReadOverride = static request => new SessionReadFailed("store unavailable");

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenRunAuthorizationCannotBeCaptured_ReturnsAgentRunAuthorizationUnavailableBeforeAnyEffect()
    {
        var selector = new FakeSecurityProfileSelector
        {
            Override = static _ => new SecurityAuthorizationCaptureUnavailable("authority offline"),
        };
        var modelCalls = 0;
        var loop = CreateLoop(out var coordinator, out _, _ =>
        {
            modelCalls++;
            return TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid()));
        }, securityProfileSelector: selector);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeOfType<AgentRunAuthorizationUnavailable>().SafeReason.ShouldBe("authority offline");
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
        modelCalls.ShouldBe(0);
        coordinator.ReceivedAppends.ShouldBeEmpty();
        _ = selector.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenTurnAuthorizationCannotBeCaptured_ReturnsAgentRunAuthorizationUnavailableWithEarlierCommits()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var captures = 0;
        var selector = new FakeSecurityProfileSelector
        {
            // Run capture, first-turn capture, then the second turn's capture is unavailable.
            Override = _ => ++captures == 3 ? new SecurityAuthorizationCaptureUnavailable("authority offline") : null,
        };
        var modelCalls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => ++modelCalls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            securityProfileSelector: selector);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunAuthorizationUnavailable>();
        result.NewMessages.Length.ShouldBe(2);
        result.FinalVersion.ShouldBe(coordinator.Version);
        modelCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenCapturedAuthorizationDiffersFromRunStartEvidence_ReturnsAgentRunInvalidState()
    {
        var selector = new FakeSecurityProfileSelector
        {
            Override = static request =>
            {
                var matching = TestSupport.TestSecurityEvidence.Authorization(
                    request.Scope.AgentId, request.Scope.SessionId, request.Scope.Correlation, request.Identity);
                return new SecurityAuthorizationCaptured(new SecurityAuthorizationContext(
                    matching.ProfileKey,
                    matching.ProfileVersion,
                    matching.PolicySnapshot,
                    matching.AuthorityKey,
                    matching.AgentDefinitionRevision,
                    new ConfigurationVersion(matching.ConfigurationVersion.Value + 1),
                    matching.Scope,
                    matching.Identity));
            },
        };
        var loop = CreateLoop(
            out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())), securityProfileSelector: selector);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunInvalidState>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenExactEvidenceIsPresent_PropagatesSnapshotAndUsesAgentInsteadOfMutableMirrors()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var descriptor = TestFactory.Model();
        var selector = FakeModelSelector.Selecting(descriptor);
        var assembler = new RecordingContextAssembler();
        var request = TestFactory.ExactRunRequest(_agentId, _sessionId, _branchId) with
        {
            ModelPolicy = new ModelSelectionPolicy([new ModelAlias("poisoned")]),
            Instructions = [TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1, "poisoned").Message],
        };
        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            selector,
            new FakeLlmModelResolver(new RespondingLlmModel(
                new ModelAlias("chat"),
                modelRequest => TestFactory.CompletedWithText(modelRequest.Context.ModelRequestId))),
            assembler);

        _ = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        selector.LastRequest.ShouldNotBeNull().Policy.ShouldBe(request.Agent!.Models);
        var evidence = assembler.Requests.ShouldHaveSingleItem().Evidence.ShouldNotBeNull();
        evidence.Agent.ShouldBeSameAs(request.Agent);
        evidence.Configuration.ShouldBeSameAs(request.Configuration);
        _ = evidence.Authorization.Scope.Correlation.ShouldBeOfType<InRunOperationCorrelation>().TurnId.ShouldNotBeNull();
        evidence.History.SourceCursor.Version.ShouldBe(new SessionVersion(1));
        evidence.History.SourceCursor.Sequence.ShouldBe(new SessionSequence(1));
        _ = evidence.History.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenSelectedModelHasNoRegisteredAdapter_ReturnsModelSelectionFailed()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var descriptor = TestFactory.Model();

        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            FakeModelSelector.Selecting(descriptor),
            new FakeLlmModelResolver(null));

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId),
            TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunModelSelectionFailed>();
        failed.SafeReason.ShouldContain("no LLM model adapter is registered");
    }

    [Fact]
    public async Task RunAsync_WhenNoCompatibleModel_ReturnsModelSelectionFailedWithDiagnostics()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var diagnostic = new ModelSelectionDiagnostic(
            new ModelAlias("chat"),
            ModelCandidateOutcome.MissingRequiredCapability,
            "no tools");

        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            new FakeModelSelector(new NoCompatibleModel(ModelRequirements.None, [diagnostic])),
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))));

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId),
            TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunModelSelectionFailed>();
        failed.Diagnostics.ShouldHaveSingleItem().Alias.Value.ShouldBe("chat");
    }

    [Fact]
    public async Task RunAsync_WhenModelPolicyIsInvalid_ReturnsModelSelectionFailed()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            new FakeModelSelector(new InvalidModelPolicy("policy names no usable candidate")),
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))));

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId),
            TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeOfType<AgentRunModelSelectionFailed>()
            .SafeReason.ShouldBe("policy names no usable candidate");
    }

    [Fact]
    public async Task RunAsync_SelectsTheModelOncePerRunRatherThanPerTurn()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var descriptor = TestFactory.Model();
        var selector = FakeModelSelector.Selecting(descriptor);
        var callCount = 0;
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), _ =>
        {
            callCount++;
            return callCount == 1
                ? TestFactory.CompletedWithToolCall(
                    new ModelRequestId(Guid.NewGuid()),
                    new ToolCallId(Guid.NewGuid()))
                : TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid()));
        });

        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            selector,
            new FakeLlmModelResolver(adapter));

        _ = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId),
            TestContext.Current.CancellationToken);

        callCount.ShouldBe(2);
        selector.SelectCount.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenContextAssemblyFails_ReturnsAgentRunContextPreparationFailed()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        coordinator.Seed([TestFactory.SeedIncompleteMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunContextPreparationFailed>();
        failed.Failure.Kind.ShouldBe(ContextPreparationFailureKind.EmptyHistory);
    }

    [Fact]
    public async Task RunAsync_WhenModelRespondsWithNoToolCalls_ReturnsAgentRunCompleted()
    {
        var modelRequestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(
            out var coordinator, out var toolInvoker, _ => TestFactory.CompletedWithText(modelRequestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var completed = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        completed.FinalMessage.State.ShouldBe(MessageState.Complete);
        result.NewMessages.Length.ShouldBe(1);
        result.AgentId.ShouldBe(_agentId);
        result.SessionId.ShouldBe(_sessionId);
        result.BranchId.ShouldBe(_branchId);
        result.RunId.ShouldBe(request.RunId);
        result.FinalVersion.ShouldBe(new SessionVersion(2));
        coordinator.Entries.Count.ShouldBe(2);
        coordinator.Entries.OfType<MessageSessionEntry>().Last().SchemaVersion.ShouldBe(new SchemaVersion("1"));
        toolInvoker.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenObserved_EmitsParentedContentFreeRunTrace()
    {
        const string protectedOutput = "do-not-export-model-output";
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => TestFactory.CompletedWithText(requestId, protectedOutput));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        _ = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var run = stopped.Single(activity => activity.OperationName == AgentKitActivityNames.InvokeAgent);
        var turn = stopped.Single(activity => activity.OperationName == AgentKitActivityNames.AgentTurn);
        var context = stopped.Single(activity => activity.OperationName == AgentKitActivityNames.ContextPrepare);
        var model = stopped.Single(activity => activity.OperationName == AgentKitActivityNames.Chat);
        var commit = stopped.Single(activity => activity.OperationName == AgentKitActivityNames.SessionCommit);
        run.Status.ShouldBe(ActivityStatusCode.Ok);
        run.GetTagItem(AgentKitTagNames.RequestModel).ShouldBe("test-model");
        run.GetTagItem(AgentKitTagNames.ProviderName).ShouldBe("test-provider");
        turn.ParentSpanId.ShouldBe(run.SpanId);
        context.ParentSpanId.ShouldBe(turn.SpanId);
        model.ParentSpanId.ShouldBe(turn.SpanId);
        commit.ParentSpanId.ShouldBe(turn.SpanId);
        stopped.SelectMany(static activity => activity.TagObjects)
            .Select(static tag => tag.Value?.ToString())
            .ShouldNotContain(protectedOutput);
    }

    [Fact]
    public async Task RunAsync_WhenTheRunActivityIsNotSampled_DoesNotTagTheHostParentActivity()
    {
        using var hostSource = new ActivitySource("agentkit-tests-host");
        using var hostListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "agentkit-tests-host",
            Sample = SampleAllData,
        };
        using var loopListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleNone,
        };
        ActivitySource.AddActivityListener(hostListener);
        ActivitySource.AddActivityListener(loopListener);
        using var parent = hostSource.StartActivity("host-request").ShouldNotBeNull();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        Activity.Current.ShouldBeSameAs(parent);
        parent.GetTagItem(AgentKitTagNames.RequestModel).ShouldBeNull();
        parent.GetTagItem(AgentKitTagNames.ProviderName).ShouldBeNull();
        parent.TagObjects.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenModelRequestsToolCall_InvokesToolThenCompletesOnNextTurn()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var firstRequestId = new ModelRequestId(Guid.NewGuid());
        var secondRequestId = new ModelRequestId(Guid.NewGuid());
        var callCount = 0;

        var loop = CreateLoop(out var coordinator, out var toolInvoker, _ =>
        {
            callCount++;
            return callCount == 1
                ? TestFactory.CompletedWithToolCall(firstRequestId, callId)
                : TestFactory.CompletedWithText(secondRequestId);
        });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        toolInvoker.ReceivedRequests.Count.ShouldBe(1);
        toolInvoker.ReceivedRequests[0].Context.ToolCallId.ShouldBe(callId);

        // user message, assistant tool-call message, tool result message, final assistant message.
        result.NewMessages.Length.ShouldBe(3);
        _ = result.NewMessages[0].ShouldBeOfType<AssistantMessage>();
        var toolMessage = result.NewMessages[1].ShouldBeOfType<ToolMessage>();
        var toolResult = toolMessage.Parts[0].ShouldBeOfType<ToolResultPart>();
        toolResult.CallId.ShouldBe(callId);
        _ = result.NewMessages[2].ShouldBeOfType<AssistantMessage>();
        coordinator.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task RunAsync_WhenMaxTurnsReachedWithPendingToolCalls_ReturnsAgentRunTurnLimitReachedWithoutInvokingTools()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());

        // Tools are deliberately offered on the final turn here so the model's pending calls exercise the
        // reject-all settlement path; the default (DisableToolsOnFinalTurn) is covered separately.
        var loop = CreateLoop(
            out var coordinator, out var toolInvoker, _ => TestFactory.CompletedWithToolCall(requestId, callId), maxTurns: 1,
            options: new AgentLoopOptions { DisableToolsOnFinalTurn = false });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var turnLimit = result.Outcome.ShouldBeOfType<AgentRunTurnLimitReached>();
        turnLimit.MaxTurns.ShouldBe(1);
        toolInvoker.ReceivedRequests.ShouldBeEmpty();
        // The assistant message is committed, and every requested call still receives its exactly-one terminal
        // result (rejected, definitely not performed) so the session remains causally valid for later runs.
        result.NewMessages.Length.ShouldBe(2);
        _ = result.NewMessages[0].ShouldBeOfType<AssistantMessage>();
        var rejected = result.NewMessages[1].ShouldBeOfType<ToolMessage>().Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolResultPart>();
        rejected.CallId.ShouldBe(callId);
        rejected.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        rejected.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    [Fact]
    public async Task RunAsync_WhenOnTheFinalPermittedTurn_RequestsTheFinalResponseWithoutTools()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out var invoker,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            contextAssembler: assembler);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var tool = new LlmToolDefinition(new ToolId("search"), "search", null, default);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 2) with { Tools = [tool] };

        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        _ = invoker.ReceivedRequests.ShouldHaveSingleItem();
        assembler.Requests.Count.ShouldBe(2);
        assembler.Requests[0].Tools.ShouldBe([tool]);
        assembler.Requests[0].ToolChoice.ShouldBe(LlmToolChoice.Auto);
        assembler.Requests[1].Tools.ShouldBeEmpty();
        assembler.Requests[1].ToolChoice.ShouldBe(LlmToolChoice.None);
    }

    [Fact]
    public async Task RunAsync_WhenFinalTurnToolsAreNotDisabled_OffersTheRunToolsOnEveryTurn()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            contextAssembler: assembler,
            options: new AgentLoopOptions { DisableToolsOnFinalTurn = false });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var tool = new LlmToolDefinition(new ToolId("search"), "search", null, default);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 2) with { Tools = [tool] };

        _ = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        assembler.Requests.Count.ShouldBe(2);
        assembler.Requests[1].Tools.ShouldBe([tool]);
        assembler.Requests[1].ToolChoice.ShouldBe(LlmToolChoice.Auto);
    }

    [Fact]
    public async Task RunAsync_WhenModelRequestsToolsDespiteFinalTurnToolsBeingDisabled_StillSettlesEveryCallAsRejected()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(
            out var coordinator, out var invoker, _ => TestFactory.CompletedWithToolCall(requestId, callId), contextAssembler: assembler);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), TestContext.Current.CancellationToken);

        assembler.Requests.ShouldHaveSingleItem().ToolChoice.ShouldBe(LlmToolChoice.None);
        _ = result.Outcome.ShouldBeOfType<AgentRunTurnLimitReached>();
        invoker.ReceivedRequests.ShouldBeEmpty();
        result.NewMessages[1].ShouldBeOfType<ToolMessage>().Parts.ShouldHaveSingleItem()
            .ShouldBeOfType<ToolResultPart>().Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
    }

    [Fact]
    public async Task RunAsync_WhenModelAttemptFailsWithPartialOutput_PreservesInterruptedMessageAndReturnsAgentRunFailed()
    {
        var failure = TestFactory.Failure();
        var loop = CreateLoop(
            out var coordinator, out _, _ => new ModelAttemptFailed(
                failure, [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)], ModelUsage.NotReported));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var failedOutcome = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        failedOutcome.Failure.ShouldBe(failure);
        result.NewMessages.Length.ShouldBe(1);
        var interrupted = result.NewMessages[0].ShouldBeOfType<AssistantMessage>();
        interrupted.State.ShouldBe(MessageState.Interrupted);
        coordinator.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenModelAttemptFailsWithNoPartialOutput_ReturnsAgentRunFailedWithoutCommittingMessage()
    {
        var failure = TestFactory.Failure();
        var loop = CreateLoop(
            out var coordinator, out _, _ => new ModelAttemptFailed(failure, [], null));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var failedOutcome = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        failedOutcome.Failure.ShouldBe(failure);
        result.NewMessages.ShouldBeEmpty();
        coordinator.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenModelAttemptCancelledWithPartialOutput_PreservesInterruptedMessageAndReturnsAgentRunCancelled()
    {
        var cancellation = TestFactory.Cancellation();
        var loop = CreateLoop(
            out var coordinator, out _, _ => new ModelAttemptCancelled(
                cancellation, [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)], null));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var cancelledOutcome = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        cancelledOutcome.SafeMessage.ShouldBe(cancellation.SafeMessage);
        result.NewMessages.Length.ShouldBe(1);
        result.NewMessages[0].State.ShouldBe(MessageState.Interrupted);
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflicts_ReturnsAgentRunSessionOperationFailed()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.AppendOverride = static request =>
            new SessionAppendConflict(request.ExpectedVersion, new SessionVersion(request.ExpectedVersion.Value + 5));

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflictsOnce_RetriesAtTheReportedVersionAndSucceeds()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var conflicted = false;
        coordinator.ConditionalAppendOverride = request =>
        {
            if (conflicted)
            {
                return null;
            }

            conflicted = true;

            // Simulate a tool invoked mid-turn (the plan/todo tool, for one) committing its own non-message
            // session fact directly through the coordinator, independently of this run's own version tracking.
            coordinator.Seed([TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, request.ExpectedVersion.Value + 1)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        coordinator.ReceivedAppends.Count.ShouldBe(2);
        coordinator.ReceivedAppends[1].ExpectedVersion.Value.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflictsWithAnInterleavedMessage_FailsClosedWithoutCommittingTheStaleResponse()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        coordinator.EnforceSequenceContinuity = true;
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var conflicted = false;
        coordinator.ConditionalAppendOverride = request =>
        {
            if (conflicted)
            {
                return null;
            }

            conflicted = true;
            // A concurrent writer commits a user message the pending response never saw.
            coordinator.SimulateConcurrentAppend([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2, "steer")]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBe(new SessionVersion(1));
        coordinator.ReceivedAppends.Count.ShouldBe(1);
        coordinator.Entries.OfType<MessageSessionEntry>().Select(static entry => entry.Message).OfType<AssistantMessage>().ShouldBeEmpty();
        _ = assembler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenToolResultAppendConflictsWithAnInterleavedMessage_NextTurnHistoryContainsThatMessage()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            contextAssembler: assembler);
        coordinator.EnforceSequenceContinuity = true;
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var interleaved = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 3, "interleaved");
        var appendCount = 0;
        coordinator.ConditionalAppendOverride = request =>
        {
            if (++appendCount != 2)
            {
                return null;
            }

            // A concurrent writer commits a user message between the assistant tool request and its results.
            coordinator.SimulateConcurrentAppend([interleaved]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        // The run's own committed messages exclude the concurrent writer's message.
        result.NewMessages.Length.ShouldBe(3);
        assembler.Requests.Count.ShouldBe(2);
        var secondTurnHistory = assembler.Requests[1].History;
        secondTurnHistory.Length.ShouldBe(4);
        _ = secondTurnHistory[1].ShouldBeOfType<AssistantMessage>();
        secondTurnHistory[2].ShouldBeSameAs(interleaved.Message);
        _ = secondTurnHistory[3].ShouldBeOfType<ToolMessage>();
        coordinator.Entries[^1].Sequence.ShouldBe(new SessionSequence(5));
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflictsAndTheInterleavedRangeCannotBeRead_ReturnsAgentRunSessionOperationFailed()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.ConditionalAppendOverride = request =>
        {
            coordinator.SimulateConcurrentAppend([TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, 2)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };
        // History loading (from sequence 0) works; only the rebase read of the interleaved range fails.
        coordinator.ConditionalReadOverride = static request =>
            request.FromSequenceExclusive.Value == 0 ? null : new SessionReadFailed("store unavailable");

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        coordinator.ReceivedAppends.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenToolResultAppendConflicts_ReturnsAgentRunSessionOperationFailed()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithToolCall(requestId, callId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var appendCount = 0;
        coordinator.ConditionalAppendOverride = request =>
        {
            appendCount++;
            return appendCount == 1
                ? null
                : new SessionAppendConflict(request.ExpectedVersion, new SessionVersion(request.ExpectedVersion.Value + 5));
        };

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        coordinator.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenToolResultAppendConflictsOnce_RetriesAtTheReportedVersionAndSucceeds()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(out var coordinator, out _, _ =>
            ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var appendCount = 0;
        var conflicted = false;
        coordinator.ConditionalAppendOverride = request =>
        {
            appendCount++;
            if (appendCount != 2 || conflicted)
            {
                return null;
            }

            conflicted = true;

            // Simulate a tool invoked mid-turn (the plan/todo tool, for one) committing its own session entry
            // directly through the coordinator, independently of this run's own version tracking.
            coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, request.ExpectedVersion.Value + 1)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        coordinator.ReceivedAppends.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed, SideEffectCertainty.Unknown)]
    [InlineData(ToolTerminalStatus.Cancelled, ToolCallOutcomeKind.Cancelled, SideEffectCertainty.PartiallyPerformed)]
    [InlineData(ToolTerminalStatus.Succeeded, ToolCallOutcomeKind.Success, SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData((ToolTerminalStatus) 99, ToolCallOutcomeKind.Failed, SideEffectCertainty.Unknown)]
    public async Task RunAsync_WhenToolReturnsPreciseEvidence_RetainsItInSessionAndNextModelRequest(ToolTerminalStatus status, ToolCallOutcomeKind kind, SideEffectCertainty certainty)
    {
        var callId = new ToolCallId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var requestId = new ModelRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var outcome = new ToolCallOutcome(kind, status, certainty, false, null, ExtensionData.Empty);
        var invocation = new ToolInvocationResult(outcome, []);
        var calls = 0;
        ToolCallOutcome? nextRequestOutcome = null;
        var loop = CreateLoop(out var coordinator, out var invoker, request =>
        {
            if (++calls == 1)
            {
                return TestFactory.CompletedWithToolCall(requestId, callId);
            }
            nextRequestOutcome = request.Context.Messages.OfType<ToolMessage>().Single().Parts.OfType<ToolResultPart>().Single().Outcome;
            return TestFactory.CompletedWithText(requestId);
        }, toolResult: invocation);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        nextRequestOutcome.ShouldBe(outcome);
        result.NewMessages.OfType<ToolMessage>().Single().Parts.OfType<ToolResultPart>().Single().Outcome.ShouldBe(outcome);
        coordinator.Entries.OfType<MessageSessionEntry>().Select(static entry => entry.Message).OfType<ToolMessage>()
            .Single().Parts.OfType<ToolResultPart>().Single().Outcome.ShouldBe(outcome);
        invoker.ReceivedRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenToolObserverFails_PreservesOneEffectAndOneCorrelatedTerminalResult()
    {
        var callId = new ToolCallId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var requestId = new ModelRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var calls = 0;
        var observer = new RecordingAgentRunObserver { ThrowAfterRecording = true };
        var loop = CreateLoop(
            out var coordinator,
            out var invoker,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer };

        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        invoker.ReceivedRequests.ShouldHaveSingleItem().Context.ToolCallId.ShouldBe(callId);
        var terminal = observer.Events.OfType<AgentRunToolCallCompleted>().ShouldHaveSingleItem();
        terminal.Result.CallId.ShouldBe(callId);
        observer.Events.OfType<AgentRunToolCallStarted>().ShouldHaveSingleItem().Call.CallId.ShouldBe(callId);
        coordinator.Entries.OfType<MessageSessionEntry>()
            .Select(static entry => entry.Message)
            .OfType<ToolMessage>()
            .Single().Parts.OfType<ToolResultPart>().ShouldHaveSingleItem().CallId.ShouldBe(callId);
    }

    [Fact]
    public async Task RunAsync_WhenModelStreams_ForwardsTypedProgressToRunObserver()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var streamed = new ModelPartDelta(requestId, 1, 0, new TextContentDelta("partial"));
        var observer = new RecordingAgentRunObserver();
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => TestFactory.CompletedWithText(requestId),
            modelEvent: streamed);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer },
            TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var progress = observer.Events.OfType<AgentRunModelResponseEvent>().ShouldHaveSingleItem();
        progress.ResponseEvent.ShouldBeSameAs(streamed);
        progress.TurnId.ShouldNotBe(default);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationRacesSuccessfulToolResultObservation_CommitsOneSuccessfulResult()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        var observer = new RecordingAgentRunObserver
        {
            EventRecorded = runEvent =>
            {
                if (runEvent is AgentRunToolCallCompleted)
                {
                    cts.Cancel();
                }
            },
        };
        var loop = CreateLoop(
            out var coordinator,
            out var invoker,
            _ => TestFactory.CompletedWithToolCall(requestId, callId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer },
            cts.Token);

        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        result.NewMessages.Length.ShouldBe(2);
        result.FinalVersion.ShouldBe(coordinator.Version);
        invoker.ReceivedRequests.ShouldHaveSingleItem().Context.ToolCallId.ShouldBe(callId);
        observer.Events.OfType<AgentRunToolCallCompleted>().ShouldHaveSingleItem()
            .Result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var committed = coordinator.Entries.OfType<MessageSessionEntry>()
            .Select(static entry => entry.Message).OfType<ToolMessage>()
            .Single().Parts.OfType<ToolResultPart>().ShouldHaveSingleItem();
        committed.CallId.ShouldBe(callId);
        committed.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
    }

    [Fact]
    public async Task RunAsync_WhenAToolCallIsCancelledMidBatch_SettlesEveryRequestedCallAndReturnsAgentRunCancelled()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var callId1 = new ToolCallId(Guid.NewGuid());
        var callId2 = new ToolCallId(Guid.NewGuid());
        var callId3 = new ToolCallId(Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        var handlerCalls = 0;

        var response = new ModelAttemptCompleted(TestFactory.Response(
            requestId,
            [
                new ToolCallPart(callId1, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
                new ToolCallPart(callId2, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
                new ToolCallPart(callId3, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
            ],
            NormalizedStopReason.ToolUse));
        var loop = CreateLoop(out var coordinator, out var invoker, _ => response, toolHandler: _ =>
        {
            handlerCalls++;
            if (handlerCalls == 2)
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }

            return TestFactory.SuccessResult();
        });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token);

        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        result.NewMessages.Length.ShouldBe(2);
        _ = result.NewMessages[1].ShouldBeOfType<ToolMessage>();
        result.FinalVersion.ShouldBe(coordinator.Version);
        invoker.ReceivedRequests.Count.ShouldBe(2);
        var committedParts = coordinator.Entries.OfType<MessageSessionEntry>()
            .Select(static entry => entry.Message).OfType<ToolMessage>()
            .Single().Parts.OfType<ToolResultPart>().ToArray();
        committedParts.Length.ShouldBe(3);
        committedParts[0].Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        committedParts[1].Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Cancelled);
        committedParts[1].Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Interrupted);
        committedParts[2].Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Cancelled);
        committedParts[2].Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Interrupted);
    }

    [Fact]
    public async Task RunAsync_WhenAToolAbsorbsCancellationIntoASettledResult_StopsRemainingCallsAndReturnsAgentRunCancelled()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var callId1 = new ToolCallId(Guid.NewGuid());
        var callId2 = new ToolCallId(Guid.NewGuid());
        using var cts = new CancellationTokenSource();

        var response = new ModelAttemptCompleted(TestFactory.Response(
            requestId,
            [
                new ToolCallPart(callId1, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
                new ToolCallPart(callId2, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
            ],
            NormalizedStopReason.ToolUse));
        var cancelledOutcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Cancelled, ToolTerminalStatus.Cancelled, SideEffectCertainty.Unknown, true, "cancelled", ExtensionData.Empty);
        var loop = CreateLoop(out var coordinator, out var invoker, _ => response, toolHandler: _ =>
        {
            // Simulates a tool (e.g. a process runner) that kills its own work and returns a settled
            // "cancelled" outcome instead of letting OperationCanceledException propagate.
            cts.Cancel();
            return new ToolInvocationResult(cancelledOutcome, []);
        });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token);

        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        result.NewMessages.Length.ShouldBe(2);
        invoker.ReceivedRequests.Count.ShouldBe(1);
        var committedParts = coordinator.Entries.OfType<MessageSessionEntry>()
            .Select(static entry => entry.Message).OfType<ToolMessage>()
            .Single().Parts.OfType<ToolResultPart>().ToArray();
        committedParts.Length.ShouldBe(2);
        committedParts[0].Outcome.ShouldBe(cancelledOutcome);
        committedParts[1].Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Cancelled);
        committedParts[1].Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Interrupted);
    }

    // ---- Probing tests: each documents a normative requirement and is expected to expose a defect if it fails. ----

    [Fact]
    public async Task RunAsync_WhenPreviousRunHitTurnLimitWithPendingToolCalls_NextRunStillAssemblesContext()
    {
        // AGENTS.md: "Every bounded, identified call reaches one terminal record ... including pre-invocation rejection."
        // If the turn-limit path commits ToolCallParts with no ToolResultPart, the session is permanently poisoned.
        var callId = new ToolCallId(Guid.NewGuid());
        var firstRequestId = new ModelRequestId(Guid.NewGuid());
        var secondRequestId = new ModelRequestId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => ++calls == 1
                ? TestFactory.CompletedWithToolCall(firstRequestId, callId)
                : TestFactory.CompletedWithText(secondRequestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var first = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), TestContext.Current.CancellationToken);
        _ = first.Outcome.ShouldBeOfType<AgentRunTurnLimitReached>();

        var second = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 4), TestContext.Current.CancellationToken);

        _ = second.Outcome.ShouldBeOfType<AgentRunCompleted>();
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflictsAfterMultiEntryConcurrentAppend_RebasesToTheActualSequence()
    {
        // The store requires whole-session sequence continuity (NextSequence + i + 1); version is not sequence.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.EnforceSequenceContinuity = true;
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var conflicted = false;
        coordinator.ConditionalAppendOverride = request =>
        {
            if (conflicted)
            {
                return null;
            }

            conflicted = true;
            // A concurrent writer commits two non-message facts in one version bump: Version 1 -> 2, NextSequence 1 -> 3.
            coordinator.SimulateConcurrentAppend(
            [
                TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, 2),
                TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, 3),
            ]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        coordinator.Entries[^1].Sequence.ShouldBe(new SessionSequence(4));
    }

    [Fact]
    public async Task RunAsync_WhenModelAttemptCancelledWithPartialOutputAndTokenIsCancelled_CommitsInterruptedMessageBeforeReturning()
    {
        // The loop's own remarks: partial output "is first committed as an Interrupted AssistantMessage so it is never discarded".
        // The real coordinator throws on a cancelled token, so the commit must not use the caller's cancelled token.
        using var cts = new CancellationTokenSource();
        var cancellation = TestFactory.Cancellation();
        var loop = CreateLoop(out var coordinator, out _, _ =>
        {
            cts.Cancel();
            return new ModelAttemptCancelled(
                cancellation, [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)], null);
        });
        coordinator.HonorCancellation = true;
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token);

        // Once partial output is committed the run has a durable effect, so cancellation settles as a typed
        // outcome carrying that message rather than throwing and discarding it.
        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        result.NewMessages.ShouldHaveSingleItem().State.ShouldBe(MessageState.Interrupted);
        var interrupted = coordinator.Entries.OfType<MessageSessionEntry>()
            .Select(static entry => entry.Message)
            .OfType<AssistantMessage>()
            .ToArray();
        _ = interrupted.ShouldHaveSingleItem();
        interrupted[0].State.ShouldBe(MessageState.Interrupted);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledBeforeAnyCommit_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var loop = CreateLoop(out var coordinator, out _, _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token));

        coordinator.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledOnALaterTurnAfterAnEarlierCommit_ReturnsAgentRunCancelledWithCommittedMessages()
    {
        using var cts = new CancellationTokenSource();
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(out var coordinator, out _, _ =>
        {
            if (++calls == 1)
            {
                return TestFactory.CompletedWithToolCall(requestId, callId);
            }

            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token);

        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        result.NewMessages.Length.ShouldBe(2);
        result.FinalVersion.ShouldBe(coordinator.Version);
        coordinator.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_WhenModelReturnsDuplicateToolCallIds_DoesNotInvokeTheSameIdentityTwice()
    {
        // tool-scheduling-and-concurrency.md: duplicate call IDs fail preflight; one identity must never produce two effects.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var callId = new ToolCallId(Guid.NewGuid());
        var calls = 0;
        var response = new ModelAttemptCompleted(TestFactory.Response(
            requestId,
            [
                new ToolCallPart(callId, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
                new ToolCallPart(callId, new ToolReference(new ToolId("search"), null, "search"), default, null, ExtensionData.Empty),
            ],
            NormalizedStopReason.ToolUse));
        var loop = CreateLoop(
            out var coordinator, out var invoker, _ => ++calls == 1 ? response : TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        invoker.ReceivedRequests.Count(request => request.Context.ToolCallId == callId).ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task RunAsync_WhenModelReportsToolUseWithoutAnyToolCall_FailsAsProtocolViolationWithoutCompleting()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var textPart = new TextPart("I will call a tool", TextSemantics.Plain, ExtensionData.Empty);
        var loop = CreateLoop(out var coordinator, out var invoker, _ => new ModelAttemptCompleted(
            TestFactory.Response(requestId, [textPart], NormalizedStopReason.ToolUse)));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        var interrupted = result.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        interrupted.State.ShouldBe(MessageState.Interrupted);
        interrupted.Parts.ShouldHaveSingleItem().ShouldBe(textPart);
        invoker.ReceivedRequests.ShouldBeEmpty();
        coordinator.Entries.OfType<MessageSessionEntry>().Select(static entry => entry.Message)
            .OfType<AssistantMessage>().ShouldHaveSingleItem().State.ShouldBe(MessageState.Interrupted);
    }

    [Fact]
    public async Task RunAsync_WhenModelStopsBecauseOfOutputLength_DoesNotSettleAsCompleted()
    {
        // agent-loop-state-machine.md: the loop "MUST not silently pretend the agent chose to finish".
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => new ModelAttemptCompleted(TestFactory.Response(
            requestId, [new TextPart("truncated", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Length)));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        result.Outcome.ShouldNotBeOfType<AgentRunCompleted>();
    }

    [Theory]
    [InlineData(NormalizedStopReason.Cancelled, typeof(AgentRunCancelled))]
    [InlineData(NormalizedStopReason.Length, typeof(AgentRunOutputLengthLimitReached))]
    [InlineData(NormalizedStopReason.Deferred, typeof(AgentRunInvalidState))]
    [InlineData(NormalizedStopReason.Pending, typeof(AgentRunFailed))]
    [InlineData(NormalizedStopReason.Error, typeof(AgentRunFailed))]
    [InlineData((NormalizedStopReason) 99, typeof(AgentRunFailed))]
    public async Task RunAsync_WhenCompletedAttemptReportsUnacceptedStopReason_SettlesWithTheMatchingTypedOutcome(
        NormalizedStopReason stopReason, Type expectedOutcome)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out var invoker, _ => new ModelAttemptCompleted(TestFactory.Response(
            requestId, [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)], stopReason)));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeOfType(expectedOutcome);
        var interrupted = result.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        interrupted.State.ShouldBe(MessageState.Interrupted);
        interrupted.Response.StopReason.ShouldBe(stopReason);
        invoker.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCompletedAttemptReportsLength_ReportsTheRequestIdentityAndPartialOutput()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => new ModelAttemptCompleted(TestFactory.Response(
            requestId, [new TextPart("truncated", TextSemantics.Plain, ExtensionData.Empty)], NormalizedStopReason.Length)));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        var limit = result.Outcome.ShouldBeOfType<AgentRunOutputLengthLimitReached>();
        limit.ModelRequestId.ShouldBe(requestId);
        limit.HasPartialOutput.ShouldBeTrue();
        limit.SafeMessage.ShouldNotContain("truncated");
    }

    [Theory]
    [InlineData(NormalizedStopReason.Pending)]
    [InlineData(NormalizedStopReason.Error)]
    public async Task RunAsync_WhenCompletedAttemptReportsNonTerminalStopReason_FailsAsProtocolViolation(NormalizedStopReason stopReason)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => new ModelAttemptCompleted(TestFactory.Response(requestId, [], stopReason)));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.ProviderId.ShouldBe(new ProviderId("test-provider"));
        result.NewMessages.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenToolInvokerThrowsNonCancellationException_EveryRequestedCallStillReachesATerminalRecord()
    {
        // tool-call-lifecycle.md: one terminal record per identified call; an invoker fault must not orphan the ToolCallPart.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var callId = new ToolCallId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            toolHandler: _ => throw new InvalidOperationException("invoker fault"));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        try
        {
            _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Propagating the fault is acceptable; leaving a dangling ToolCallPart in the session is not.
        }

        var committedCallIds = coordinator.Entries.OfType<MessageSessionEntry>()
            .SelectMany(static entry => entry.Message.Parts.OfType<ToolCallPart>()).Select(static part => part.CallId).ToArray();
        var committedResultIds = coordinator.Entries.OfType<MessageSessionEntry>()
            .SelectMany(static entry => entry.Message.Parts.OfType<ToolResultPart>()).Select(static part => part.CallId).ToArray();
        committedResultIds.ShouldBe(committedCallIds);
    }

    private DefaultAgentLoop CreateLoop(
        out FakeSessionCoordinator coordinator,
        out FakeToolInvoker toolInvoker,
        Func<LlmModelRequest, ModelAttemptResult> respond,
        int maxTurns = 8,
        ToolInvocationResult? toolResult = null,
        Func<ToolCallRequest, ToolInvocationResult>? toolHandler = null,
        ModelResponseEvent? modelEvent = null,
        IContextAssembler? contextAssembler = null,
        IRunContinuationPolicy? continuationPolicy = null,
        FakeSecurityProfileSelector? securityProfileSelector = null,
        AgentLoopOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _ = maxTurns;
        coordinator = new FakeSessionCoordinator(_branchId);
        toolInvoker = new FakeToolInvoker(toolHandler ?? (_ => toolResult ?? TestFactory.SuccessResult()));
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), respond, modelEvent);
        var descriptor = TestFactory.Model();

        return new DefaultAgentLoop(
            coordinator,
            securityProfileSelector ?? new FakeSecurityProfileSelector(),
            contextAssembler ?? new DefaultContextAssembler(),
            toolInvoker,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            FakeModelSelector.Selecting(descriptor),
            new FakeLlmModelResolver(adapter),
            continuationPolicy ?? new DefaultRunContinuationPolicy(TimeProvider.System),
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            timeProvider ?? TimeProvider.System,
            Options.Create(options ?? new AgentLoopOptions()));
    }

    private static DefaultAgentLoop CreateLoopWith(
        FakeSessionCoordinator coordinator,
        IModelCatalog catalog,
        IModelSelector selector,
        ILlmModelResolver resolver,
        IContextAssembler? contextAssembler = null) =>
        new(
            coordinator,
            new FakeSecurityProfileSelector(),
            contextAssembler ?? new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            catalog,
            selector,
            resolver,
            new DefaultRunContinuationPolicy(TimeProvider.System),
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions()));

    private static GuidIdentifierGenerator<T> IdGenerator<T>(Func<Guid, T> factory)
        where T : struct =>
        new(factory);

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private static ActivitySamplingResult SampleNone(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.None;

    private sealed class RespondingLlmModel: ILlmModel
    {
        private readonly Func<LlmModelRequest, ModelAttemptResult> _respond;
        private readonly ModelResponseEvent? _modelEvent;

        public RespondingLlmModel(
            ModelAlias alias,
            Func<LlmModelRequest, ModelAttemptResult> respond,
            ModelResponseEvent? modelEvent = null)
        {
            Alias = alias;
            _respond = respond;
            _modelEvent = modelEvent;
        }

        public ModelAlias Alias { get; }

        public async Task<ModelAttemptResult> ExecuteAsync(
            LlmModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default)
        {
            if (_modelEvent is not null)
            {
                await observer.OnEventAsync(_modelEvent, cancellationToken);
            }

            return _respond(request);
        }
    }
}
