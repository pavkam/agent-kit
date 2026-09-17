// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

public sealed class DefaultAgentLoopTests
{
    private const string TestLoopKey = "test-loop";

    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());
    private AgentRunServices _services = null!;

    [Fact]
    public void Constructor_WhenOperationIdsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultAgentLoop(
            null!,
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            new FakeOptionsMonitor<AgentLoopOptions>(new AgentLoopOptions()),
            TestLoopKey));

        exception.ParamName.ShouldBe("operationIds");
    }

    [Fact]
    public void Constructor_WhenOptionsMonitorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultAgentLoop(
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            null!,
            TestLoopKey));

        exception.ParamName.ShouldBe("optionsMonitor");
    }

    [Fact]
    public void Constructor_WhenLoopKeyIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultAgentLoop(
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            new FakeOptionsMonitor<AgentLoopOptions>(new AgentLoopOptions()),
            null!));

        exception.ParamName.ShouldBe("loopKey");
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

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
    public async Task RunAsync_WhenSelectionDowngradesParallelToolCalls_DisablesItOnTheRequestSentToTheModel()
    {
        // The selector accepted a CapabilitiesDowngraded candidate (ParallelToolCalls) because the model cannot
        // honor it, but nothing applied that declared adjustment to the actual request settings: the run must not
        // still ask for ParallelToolCalls = true against a model that just declared it unsupported.
        var descriptor = TestFactory.Model();
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var requestId = new ModelRequestId(Guid.NewGuid());
        LlmModelRequest? received = null;
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), request =>
        {
            received = request;
            return TestFactory.CompletedWithText(requestId);
        });

        var selector = FakeModelSelector.SelectingWithAdjustments(
            descriptor,
            [new CapabilityAdjustment(
                ModelCapabilityKind.ParallelToolCalls,
                "Parallel tool calls were disabled; tools are requested one at a time.")]);

        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            selector,
            new FakeLlmModelResolver(adapter));

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId) with
        {
            Settings = LlmRequestSettings.Default with { ParallelToolCalls = true },
        };

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        _ = received.ShouldNotBeNull();
        received.Context.Settings.ParallelToolCalls.ShouldBe(false);
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyHaltsAfterNoToolTurn_SettlesWithTheHaltOutcome()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var halt = new AgentRunInvalidState("policy halt");
        var policy = new ScriptedRunContinuationPolicy(_ => new HaltRun(halt));
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeSameAs(halt);
        modelCalls.ShouldBe(1);
        _ = invoker.ReceivedRequests.ShouldHaveSingleItem();
        result.NewMessages.Length.ShouldBe(2);
        var context = policy.Contexts.ShouldHaveSingleItem();
        var boundary = context.Boundary.ShouldBeOfType<CommittedTurnContinuationBoundary>();
        var reference = boundary.ToolResults.ShouldHaveSingleItem();
        reference.ToolCallId.ShouldBe(callId);
        // The batch's real terminal-record identity is coordinator.Entries[^1].Id, but CommittedToolResultReference
        // requires a distinct SessionEntryId per call in the batch (every call's result is one ContentPart of that
        // single real entry), so the loop derives a policy-facing-only identity from it instead of reusing the real
        // entry id verbatim. See DefaultAgentLoop.DerivePerCallEntryId for exactly what this value does and does not mean.
        reference.SessionEntryId.ShouldNotBe(default);
        reference.TurnId.ShouldBe(boundary.Response.TurnId!.Value);
        context.Causes.ShouldHaveSingleItem().ShouldBeOfType<CommittedToolResultsContinuationCause>()
            .ToolResults.ShouldBe(boundary.ToolResults);
    }

    [Fact]
    public async Task RunAsync_WhenTurnRequestsMultipleToolCalls_OffersEveryCallsCommittedResultToTheContinuationPolicy()
    {
        var firstCallId = new ToolCallId(Guid.NewGuid());
        var secondCallId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var policy = new ScriptedRunContinuationPolicy(static context =>
            new CompleteRun(new AgentRunCompleted(((CommittedTurnContinuationBoundary) context.Boundary).Response)));
        var modelCalls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out var invoker,
            _ => ++modelCalls == 1
                ? new ModelAttemptCompleted(TestFactory.Response(
                    requestId,
                    [
                        new ToolCallPart(firstCallId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                        new ToolCallPart(secondCallId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                    ],
                    NormalizedStopReason.ToolUse))
                : TestFactory.CompletedWithText(requestId),
            continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        modelCalls.ShouldBe(1);
        invoker.ReceivedRequests.Count.ShouldBe(2);
        var context = policy.Contexts.ShouldHaveSingleItem();
        var boundary = context.Boundary.ShouldBeOfType<CommittedTurnContinuationBoundary>();
        boundary.ToolResults.Length.ShouldBe(2);
        boundary.ToolResults[0].ToolCallId.ShouldBe(firstCallId);
        boundary.ToolResults[1].ToolCallId.ShouldBe(secondCallId);
        // Every call in the batch gets its own distinct policy-facing identity, even though both results are
        // ContentParts of the same single real ToolMessage session entry.
        boundary.ToolResults[0].SessionEntryId.ShouldNotBe(boundary.ToolResults[1].SessionEntryId);
        boundary.ToolResults.All(reference => reference.TurnId == boundary.Response.TurnId!.Value).ShouldBeTrue();
        context.Causes.ShouldHaveSingleItem().ShouldBeOfType<CommittedToolResultsContinuationCause>()
            .ToolResults.ShouldBe(boundary.ToolResults);
    }

    [Fact]
    public async Task RunAsync_WhenContinuationPolicyHaltsAfterMultipleToolResults_SettlesWithoutAnotherModelRequest()
    {
        var firstCallId = new ToolCallId(Guid.NewGuid());
        var secondCallId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var halt = new AgentRunInvalidState("halt after multi-call tools");
        var policy = new ScriptedRunContinuationPolicy(_ => new HaltRun(halt));
        var modelCalls = 0;
        var loop = CreateLoop(
            out var coordinator,
            out var invoker,
            _ => ++modelCalls == 1
                ? new ModelAttemptCompleted(TestFactory.Response(
                    requestId,
                    [
                        new ToolCallPart(firstCallId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                        new ToolCallPart(secondCallId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                    ],
                    NormalizedStopReason.ToolUse))
                : TestFactory.CompletedWithText(requestId),
            continuationPolicy: policy);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBeSameAs(halt);
        modelCalls.ShouldBe(1);
        invoker.ReceivedRequests.Count.ShouldBe(2);
        result.NewMessages.Length.ShouldBe(2);
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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token);

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
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            new FakeOptionsMonitor<AgentLoopOptions>(options),
            TestLoopKey));

        exception.ParamName.ShouldBe("optionsMonitor");
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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var run = loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
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

        var run = loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer }, _services,
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

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        var first = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
        _ = first.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        _ = first.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        coordinator.ConditionalAppendOverride = null;

        var second = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
        var second = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var run = loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
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
    public async Task RunAsync_WhenBranchHasAnActiveCompactionCheckpoint_ContextReceivesSummaryPlusExactSuffix()
    {
        const string summary = "the user asked about weather and was told it is sunny";
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        var covered1 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1, "covered one");
        var covered2 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2, "covered two");
        var retained3 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 3, "retained three");
        var retained4 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 4, "retained four");
        var checkpoint = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 5, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3, summary);
        var later6 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 6, "after checkpoint");
        coordinator.Seed([covered1, covered2, retained3, retained4, checkpoint, later6]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(4);
        var projected = history[0].ShouldBeOfType<RuntimeMessage>();
        projected.ShouldNotBeAssignableTo<SystemMessage>();
        projected.ShouldNotBeAssignableTo<DeveloperMessage>();
        projected.State.ShouldBe(MessageState.Complete);
        projected.AgentId.ShouldBe(_agentId);
        projected.SessionId.ShouldBe(_sessionId);
        projected.BranchId.ShouldBe(_branchId);
        projected.ConversationId.ShouldBe(coordinator.ConversationId);
        projected.CreatedAt.ShouldBe(checkpoint.RecordedAt);
        projected.RunId.ShouldBe(checkpoint.Correlation.ShouldBeOfType<InRunOperationCorrelation>().RunId);
        projected.Parts.Length.ShouldBe(2);
        projected.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe(CompactionCheckpointProjection.HeaderText);
        projected.Parts[1].ShouldBeSameAs(checkpoint.Record.Checkpoint!.Summary[0]);
        projected.Extensions.Values[CompactionCheckpointProjection.FormatKey].ShouldBe(Json(CompactionCheckpointProjection.Format));
        projected.Extensions.Values[CompactionCheckpointProjection.CompactionIdKey].ShouldBe(Json(checkpoint.Record.Context.CompactionId.ToString()));
        projected.Extensions.Values[CompactionCheckpointProjection.ManifestIdKey].ShouldBe(Json(checkpoint.Record.Manifest.Id.ToString()));
        projected.Extensions.Values[CompactionCheckpointProjection.SessionEntryIdKey].ShouldBe(Json(checkpoint.Id.ToString()));
        projected.Extensions.Values[CompactionCheckpointProjection.SequenceKey].ShouldBe(Json(5L));
        projected.Extensions.Values[CompactionCheckpointProjection.CoveredStartKey].ShouldBe(Json(1L));
        projected.Extensions.Values[CompactionCheckpointProjection.CoveredEndKey].ShouldBe(Json(2L));
        projected.Extensions.Values[CompactionCheckpointProjection.RetainedSuffixStartKey].ShouldBe(Json(3L));
        history[1].ShouldBeSameAs(retained3.Message);
        history[2].ShouldBeSameAs(retained4.Message);
        history[3].ShouldBeSameAs(later6.Message);
        history.ShouldNotContain(covered1.Message);
        history.ShouldNotContain(covered2.Message);
        assembler.Requests[0].History.SelectMany(static message => message.Parts).OfType<TextPart>().Select(static part => part.Text)
            .ShouldNotContain("covered one");
        coordinator.Entries.OfType<MessageSessionEntry>().Select(static entry => entry.Message).OfType<RuntimeMessage>().ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCheckpointRetainsNoSuffix_ContextReceivesSummaryThenLaterEntriesOnly()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        var later = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 4, "after checkpoint");
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3),
            later,
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(2);
        _ = history[0].ShouldBeOfType<RuntimeMessage>();
        history[1].ShouldBeSameAs(later.Message);
    }

    [Fact]
    public async Task RunAsync_WhenTwoActiveCheckpointsExist_NewestWins()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler, logger: logger);
        var older = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2, "older summary");
        var retained5 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 5, "retained by newest");
        var newest = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 6, coveredStart: 1, coveredEnd: 4, retainedSuffixStart: 5, "newest summary");
        var later7 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 7, "after newest");
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            older,
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 4),
            retained5,
            newest,
            later7,
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(3);
        var projected = history[0].ShouldBeOfType<RuntimeMessage>();
        projected.Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("newest summary");
        projected.Extensions.Values[CompactionCheckpointProjection.CompactionIdKey].ShouldBe(Json(newest.Record.Context.CompactionId.ToString()));
        history[1].ShouldBeSameAs(retained5.Message);
        history[2].ShouldBeSameAs(later7.Message);
        history.OfType<RuntimeMessage>().Count().ShouldBe(1);
        logger.Snapshot().ShouldNotContain(static entry => entry.EventId.Id == 1092);
        var reconstructed = logger.Snapshot().Single(static entry => entry.EventId.Id == 1091);
        reconstructed.State["CoveredEntryCount"].ShouldBe(4);
        reconstructed.State["RetainedMessageCount"].ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenTheNewestCheckpointDoesNotCoverTheOlderOne_LogsAWarningAndStillUsesTheNewest()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler, logger: logger);
        var older = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2, "older summary");
        var retained4 = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 4, "retained by newest");
        var newest = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 5, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 4, "newest summary");
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            older,
            retained4,
            newest,
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(2);
        history[0].ShouldBeOfType<RuntimeMessage>().Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("newest summary");
        history[1].ShouldBeSameAs(retained4.Message);
        var warning = logger.Snapshot().Single(static entry => entry.EventId.Id == 1092);
        warning.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        warning.State["CompactionId"].ShouldBe(newest.Record.Context.CompactionId);
        warning.State["OlderCompactionId"].ShouldBe(older.Record.Context.CompactionId);
        warning.State["CheckpointSequence"].ShouldBe(new SessionSequence(5));
        warning.State["OlderCheckpointSequence"].ShouldBe(new SessionSequence(3));
        warning.Message.ShouldNotContain("summary");
    }

    [Fact]
    public async Task RunAsync_WhenCheckpointIsNotActive_IsIgnored()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler, logger: logger);
        var first = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1);
        var second = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2);
        var fourth = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 4);
        coordinator.Seed([
            first,
            second,
            TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2, status: CompactionRecordStatus.Rejected),
            fourth,
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.ShouldBe([first.Message, second.Message, fourth.Message]);
        history.OfType<RuntimeMessage>().ShouldBeEmpty();
        logger.Snapshot().ShouldNotContain(static entry => entry.EventId.Id == 1091 || entry.EventId.Id == 1092);
    }

    [Fact]
    public async Task RunAsync_WhenCheckpointExists_AppendsStillUseTheRealBranchTip()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.EnforceSequenceContinuity = true;
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 3),
            TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 4, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 5),
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        result.FinalVersion.ShouldBe(new SessionVersion(2));
        var append = coordinator.ReceivedAppends.ShouldHaveSingleItem();
        append.ExpectedVersion.ShouldBe(new SessionVersion(1));
        append.Entries.ShouldHaveSingleItem().Sequence.ShouldBe(new SessionSequence(6));
        coordinator.Entries.Count.ShouldBe(6);
        _ = coordinator.Entries[5].ShouldBeOfType<MessageSessionEntry>().Message.ShouldBeOfType<AssistantMessage>();
    }

    [Fact]
    public async Task RunAsync_WhenDanglingToolCallIsInsideCoveredRange_NoRecoveryIsAttempted()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out var invoker, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        var retained = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 3, "retained");
        var later = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 5, "after checkpoint");
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedAssistantToolCallEntry(_agentId, _sessionId, _branchId, 2, callId),
            retained,
            TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 4, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3),
            later,
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        invoker.ReceivedRequests.ShouldBeEmpty();
        _ = result.NewMessages.ShouldHaveSingleItem().ShouldBeOfType<AssistantMessage>();
        coordinator.ReceivedAppends.ShouldNotContain(static append => append.IdempotencyKey.Value.StartsWith("recovery:", StringComparison.Ordinal));
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(3);
        _ = history[0].ShouldBeOfType<RuntimeMessage>();
        history[1].ShouldBeSameAs(retained.Message);
        history[2].ShouldBeSameAs(later.Message);
        history.SelectMany(static message => message.Parts).OfType<ToolCallPart>().ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenDanglingToolCallIsInsideTheRetainedSuffix_StillSettlesItBeforeTheFirstTurn()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        var dangling = TestFactory.SeedAssistantToolCallEntry(_agentId, _sessionId, _branchId, 3, callId);
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            dangling,
            TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 4, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3),
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        result.NewMessages.Length.ShouldBe(2);
        var settlement = result.NewMessages[0].ShouldBeOfType<ToolMessage>();
        settlement.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolResultPart>().CallId.ShouldBe(callId);
        var history = assembler.Requests.ShouldHaveSingleItem().History;
        history.Length.ShouldBe(3);
        _ = history[0].ShouldBeOfType<RuntimeMessage>();
        history[1].ShouldBeSameAs(dangling.Message);
        history[2].ShouldBeSameAs(settlement);
        coordinator.ReceivedAppends[0].ExpectedVersion.ShouldBe(new SessionVersion(1));
        coordinator.ReceivedAppends[0].Entries[0].Sequence.ShouldBe(new SessionSequence(5));
    }

    [Fact]
    public async Task RunAsync_WhenTheSameCheckpointIsProjectedByTwoRuns_UsesTheSameMessageId()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var assembler = new RecordingContextAssembler();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), contextAssembler: assembler);
        var checkpoint = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 2, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1), checkpoint]);

        _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
        _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        assembler.Requests.Count.ShouldBe(2);
        var first = assembler.Requests[0].History[0].ShouldBeOfType<RuntimeMessage>();
        var second = assembler.Requests[1].History[0].ShouldBeOfType<RuntimeMessage>();
        second.Id.ShouldBe(first.Id);
        first.Id.Value.ShouldNotBe(checkpoint.Id.Value);
        first.Id.Value.ShouldNotBe(Guid.Empty);
        first.Id.Value.ToString("D")[14].ShouldBe('8');
    }

    [Fact]
    public async Task RunAsync_WhenBranchHasAnActiveCompactionCheckpoint_LogsAndTagsWithoutSummaryContent()
    {
        const string protectedSummary = "do-not-log-this-summary-content";
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var requestId = new ModelRequestId(Guid.NewGuid());
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), logger: logger);
        var checkpoint = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 4, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3, protectedSummary);
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 3),
            checkpoint,
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 5),
        ]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        _ = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1091);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Information);
        entry.Category.ShouldBe(typeof(DefaultAgentLoop).FullName);
        entry.State["RunId"].ShouldBe(request.RunId);
        entry.State["CompactionId"].ShouldBe(checkpoint.Record.Context.CompactionId);
        entry.State["CheckpointSequence"].ShouldBe(new SessionSequence(4));
        entry.State["CoveredEntryCount"].ShouldBe(2);
        entry.State["RetainedMessageCount"].ShouldBe(2);
        entry.State.Keys.ShouldNotContain("Summary");
        logger.Snapshot().SelectMany(static entry => entry.State.Values.Select(static value => value?.ToString()).Append(entry.Message))
            .ShouldNotContain(protectedSummary);
        var run = stopped.Single(static activity => activity.OperationName == AgentKitActivityNames.InvokeAgent);
        run.GetTagItem(AgentKitTagNames.CompactionId).ShouldBe(checkpoint.Record.Context.CompactionId.ToString());
        stopped.SelectMany(static activity => activity.TagObjects).Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedSummary);
    }

    [Fact]
    public async Task RunAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var loop = CreateLoop(out _, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => loop.RunAsync(null!, _services, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task RunAsync_WhenHistoryCannotBeLoaded_ReturnsAgentRunSessionOperationFailedWithoutAVersion()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        coordinator.ReadOverride = static request => new SessionReadFailed("store unavailable");

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenSessionLoadDoesNotReturnSessionLoaded_ReturnsAgentRunSessionOperationFailed()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        coordinator.LoadOverride = static _ => new SessionLoadFailed("store unavailable");

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenLoadedDescriptorAddressDiffersFromTheRequestedContext_ReturnsAgentRunSessionOperationFailed()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        var otherAgentId = new AgentId(Guid.NewGuid());
        coordinator.LoadOverride = context => new SessionLoaded(new SessionDescriptor(
            new SessionAddress(otherAgentId, context.SessionId),
            null,
            context.Identity.TenantId,
            context.Identity.PrincipalId,
            new SessionStoreKey("test-store"),
            _branchId,
            new SessionVersion(0),
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            ExtensionData.Empty));

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenHistoryPagesReportDifferentSnapshots_ReturnsAgentRunSessionOperationFailed()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        var first = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1);
        var second = TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2);
        var snapshotA = new SessionReadSnapshot(new SessionAddress(_agentId, _sessionId), _branchId, new SessionVersion(1), new SessionSequence(2));
        var snapshotB = new SessionReadSnapshot(new SessionAddress(_agentId, _sessionId), _branchId, new SessionVersion(2), new SessionSequence(2));
        var page = 0;
        coordinator.ReadOverride = _ => ++page switch
        {
            1 => new SessionPage([first], new SessionSequence(1), true, snapshotA),
            _ => new SessionPage([second], new SessionSequence(2), false, snapshotB),
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenTheRebaseReadReportsDifferentSnapshotsAcrossPages_ReturnsAgentRunSessionOperationFailed()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.ConditionalAppendOverride = request =>
        {
            coordinator.SimulateConcurrentAppend([TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, 2)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };
        var snapshotA = new SessionReadSnapshot(new SessionAddress(_agentId, _sessionId), _branchId, new SessionVersion(1), new SessionSequence(2));
        var snapshotB = new SessionReadSnapshot(new SessionAddress(_agentId, _sessionId), _branchId, new SessionVersion(2), new SessionSequence(2));
        var rebasePage = 0;
        coordinator.ConditionalReadOverride = request =>
        {
            if (request.FromSequenceExclusive.Value == 0)
            {
                // History loading (from sequence 0) uses the normal fake behavior.
                return null;
            }

            // The rebase read reports two pages with different pinned snapshots, which the loop must reject.
            return ++rebasePage switch
            {
                1 => new SessionPage(
                    [TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, 2)], new SessionSequence(1), true, snapshotA),
                _ => new SessionPage(
                    [], new SessionSequence(2), false, snapshotB),
            };
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_WhenTheModelAdapterThrowsANonCancellationException_PropagatesItAndLogsTheFault()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var fault = new InvalidOperationException("adapter fault");
        var loop = CreateLoop(out var coordinator, out _, _ => throw fault, logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => loop.RunAsync(request, _services, TestContext.Current.CancellationToken));

        exception.ShouldBeSameAs(fault);
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1004);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Error);
        entry.State["RunId"].ShouldBe(request.RunId);
        entry.State["ErrorType"].ShouldBe(fault.GetType().FullName);
    }

    [Fact]
    public async Task RunAsync_WhenNoObserverIsSuppliedButTheModelStreams_DeliversToTheNoOpObserverWithoutFailing()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var streamed = new ModelPartDelta(requestId, 1, 0, new TextContentDelta("partial"));
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), modelEvent: streamed);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
    }

    // ---- Structured logging: these scenarios exist elsewhere without an enabled logger; here they additionally
    // exercise LoopLog's generated formatters through a RecordingLogger that reports IsEnabled(true). ----

    [Fact]
    public async Task RunAsync_WhenAToolBatchIsInvoked_LogsItsStartAndCompletionWithSafeFields()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var started = logger.Snapshot().Single(static entry => entry.EventId.Id == 1030);
        started.State["RunId"].ShouldBe(request.RunId);
        started.State["ToolCount"].ShouldBe(1);
        var completed = logger.Snapshot().Single(static entry => entry.EventId.Id == 1031);
        completed.State["ToolCount"].ShouldBe(1);
        logger.Snapshot().SelectMany(static entry => entry.State.Values.Select(static value => value?.ToString()).Append(entry.Message))
            .ShouldNotContain("search");
    }

    [Fact]
    public async Task RunAsync_WhenATurnSettlesWithoutSuccess_LogsTurnFailedWithTheOutcomeNameOnly()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var failure = TestFactory.Failure("do-not-log-this-safe-message");
        var loop = CreateLoop(
            out var coordinator, out _, _ => new ModelAttemptFailed(failure, [], null), logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1012);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.State["RunId"].ShouldBe(request.RunId);
        entry.State["Outcome"].ShouldBe(nameof(AgentRunFailed));
        logger.Snapshot().SelectMany(static entry => entry.State.Values.Select(static value => value?.ToString()).Append(entry.Message))
            .ShouldNotContain("do-not-log-this-safe-message");
    }

    [Fact]
    public async Task RunAsync_WhenTheToolMessageCommitFails_LogsToolBatchFailed()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithToolCall(requestId, callId), logger: logger,
            options: new AgentLoopOptions { SettlementTimeout = TimeSpan.FromMilliseconds(50) });
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        coordinator.ConditionalAppendOverride = static request =>
            request.IdempotencyKey.Value.EndsWith(":tools", StringComparison.Ordinal) ? new SessionAppendFailed("store fault") : null;

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1032);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.State["Outcome"].ShouldBe(nameof(SessionAppendFailed));
    }

    [Fact]
    public async Task RunAsync_WhenCancellationInterruptsAToolBatch_LogsToolBatchInterrupted()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var callId = new ToolCallId(Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithToolCall(requestId, callId),
            toolHandler: _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token);

        _ = result.Outcome.ShouldBeOfType<AgentRunCancelled>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1033);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Information);
    }

    [Fact]
    public async Task RunAsync_WhenAToolInvokerFaults_LogsToolCallFaultedWithTheCallIdAndErrorTypeOnly()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var callId = new ToolCallId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            toolHandler: _ => throw new InvalidOperationException("do-not-log-this-detail"),
            logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1034);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Error);
        entry.State["ToolCallId"].ShouldBe(callId);
        entry.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
        logger.Snapshot().SelectMany(static entry => entry.State.Values.Select(static value => value?.ToString()).Append(entry.Message))
            .ShouldNotContain("do-not-log-this-detail");
    }

    [Fact]
    public async Task RunAsync_WhenOnTheFinalPermittedTurn_LogsFinalTurnToolsDisabled()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var calls = 0;
        var loop = CreateLoop(
            out var coordinator, out _,
            _ => ++calls == 1 ? TestFactory.CompletedWithToolCall(requestId, callId) : TestFactory.CompletedWithText(requestId),
            logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var tool = new LlmToolDefinition(new ToolId("search"), "search", null, default);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 2) with { Tools = [tool] };

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1036);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Debug);
        entry.State["MaxTurns"].ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenRunObserverFails_LogsTheEventTypeOnly()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingAgentRunObserver { ThrowAfterRecording = true };
        var streamed = new ModelPartDelta(requestId, 1, 0, new TextContentDelta("partial"));
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), modelEvent: streamed, logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer };

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1070);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.State["RunId"].ShouldBe(request.RunId);
        entry.State["EventType"].ShouldBe(nameof(AgentRunModelResponseEvent));
    }

    [Fact]
    public async Task RunAsync_WhenModelSelectionFails_LogsModelSelectionFailedWithoutTheSafeReason()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(TestFactory.Model())),
            new FakeModelSelector(new InvalidModelPolicy("do-not-log-this-detail")),
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))),
            logger: logger);
        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunModelSelectionFailed>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1050);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.State["RunId"].ShouldBe(request.RunId);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledBeforeAnyCommit_LogsRunCancelled()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        using var cts = new CancellationTokenSource();
        var loop = CreateLoop(
            out var coordinator, out _,
            _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
            logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token));

        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1003);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Information);
        _ = entry.State["RunId"].ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflictsOnce_LogsSessionAppendConflictRetried()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var conflicted = false;
        coordinator.ConditionalAppendOverride = request =>
        {
            if (conflicted)
            {
                return null;
            }

            conflicted = true;
            coordinator.Seed([TestFactory.SeedToolFactEntry(_agentId, _sessionId, _branchId, request.ExpectedVersion.Value + 1)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1041);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Information);
        entry.State["Attempt"].ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenAssistantAppendConflictsWithAnInterleavedMessage_LogsSessionAppendStaleAfterInterleavedMessage()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), logger: logger);
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
            coordinator.SimulateConcurrentAppend([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2, "steer")]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1042);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public async Task RunAsync_WhenADanglingToolCallIsSettledBeforeTheFirstTurn_LogsDanglingToolCallsSettled()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(out var coordinator, out _, _ => TestFactory.CompletedWithText(requestId), logger: logger);
        var dangling = TestFactory.SeedAssistantToolCallEntry(_agentId, _sessionId, _branchId, 3, callId);
        coordinator.Seed([
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1),
            TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2),
            dangling,
            TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 4, coveredStart: 1, coveredEnd: 2, retainedSuffixStart: 3),
        ]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1090);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.State["ToolCount"].ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenMaxTurnsReachedWithPendingToolCalls_LogsToolBatchRejectedAtTurnLimit()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = CreateLoop(
            out var coordinator, out _, _ => TestFactory.CompletedWithToolCall(requestId, callId), maxTurns: 1,
            options: new AgentLoopOptions { DisableToolsOnFinalTurn = false }, logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunTurnLimitReached>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1035);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Information);
        entry.State["ToolCount"].ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenModelReportsToolUseWithoutAnyToolCall_LogsModelResponseNotAccepted()
    {
        var logger = new TestSupport.RecordingLogger<DefaultAgentLoop>();
        var requestId = new ModelRequestId(Guid.NewGuid());
        var textPart = new TextPart("I will call a tool", TextSemantics.Plain, ExtensionData.Empty);
        var loop = CreateLoop(out var coordinator, out _, _ => new ModelAttemptCompleted(
            TestFactory.Response(requestId, [textPart], NormalizedStopReason.ToolUse)), logger: logger);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        var entry = logger.Snapshot().Single(static entry => entry.EventId.Id == 1024);
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.State["Reason"].ShouldBe("tool-use stop without any tool call");
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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunInvalidState>();
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenExactEvidenceIsPresent_PropagatesSnapshotAndUsesAgentInsteadOfMutableMirrors()
    {
        // AgentRunRequest's ModelPolicy/Instructions init accessors now reject any value that
        // diverges from the pinned Agent's own (see AgentRunRequestTests), so the request can no
        // longer be constructed in a "poisoned" state to prove the loop ignores the mirrors at run
        // time; the mirrors are provably identical to Agent's own values for every request that
        // exists. This test now only confirms the loop reads the exact evidence (Agent,
        // Configuration) through to selection and context assembly.
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var descriptor = TestFactory.Model();
        var selector = FakeModelSelector.Selecting(descriptor);
        var assembler = new RecordingContextAssembler();
        var request = TestFactory.ExactRunRequest(_agentId, _sessionId, _branchId);
        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            selector,
            new FakeLlmModelResolver(new RespondingLlmModel(
                new ModelAlias("chat"),
                modelRequest => TestFactory.CompletedWithText(modelRequest.Context.ModelRequestId))),
            assembler);

        _ = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services,
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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services,
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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services,
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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services,
            TestContext.Current.CancellationToken);

        callCount.ShouldBe(2);
        selector.SelectCount.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ReusesTheSelectionModelRequestIdForTheFirstAttemptAndAllocatesFreshIdsAfterwards()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);
        var descriptor = TestFactory.Model();
        var selector = FakeModelSelector.Selecting(descriptor);
        var assembler = new RecordingContextAssembler();
        var attemptRequestIds = new List<ModelRequestId>();
        var callId = new ToolCallId(Guid.NewGuid());
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), modelRequest =>
        {
            attemptRequestIds.Add(modelRequest.Context.ModelRequestId);
            return attemptRequestIds.Count == 1
                ? TestFactory.CompletedWithToolCall(modelRequest.Context.ModelRequestId, callId)
                : TestFactory.CompletedWithText(modelRequest.Context.ModelRequestId);
        });
        var loop = CreateLoopWith(
            coordinator,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            selector,
            new FakeLlmModelResolver(adapter),
            assembler);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        var selectionRequestId = selector.LastRequest.ShouldNotBeNull().ModelRequestId;
        attemptRequestIds.Count.ShouldBe(2);
        attemptRequestIds[0].ShouldBe(selectionRequestId);
        attemptRequestIds[1].ShouldNotBe(selectionRequestId);
        assembler.Requests[0].ModelRequestId.ShouldBe(selectionRequestId);
        result.NewMessages[0].ShouldBeOfType<AssistantMessage>().Response.RequestId.ShouldBe(selectionRequestId);
    }

    [Fact]
    public async Task RunAsync_WhenContextAssemblyFails_ReturnsAgentRunContextPreparationFailed()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        coordinator.Seed([TestFactory.SeedIncompleteMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        _ = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        // The loop must project the invoker's own resolution (never the unresolved reference the model's call
        // carried) into the committed ToolResultPart, together with projection provenance for that invocation.
        toolResult.Tool.IsResolved.ShouldBeTrue();
        toolResult.Tool.Id.ShouldBe(new ToolId("search"));
        toolResult.Tool.ProviderAlias.ShouldBe(new ToolAlias("search"));
        toolResult.Projection.Policy.ShouldBe(ToolResultProjectionPolicyReference.Default);
        toolResult.Projection.Losses.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenToolInvokerCannotResolveTheRequestedAlias_CommitsUnresolvedReferenceWithUnknownToolOutcome()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var requestId = new ModelRequestId(Guid.NewGuid());
        var secondRequestId = new ModelRequestId(Guid.NewGuid());
        var callCount = 0;

        var loop = CreateLoop(
            out var coordinator,
            out var toolInvoker,
            _ =>
            {
                callCount++;
                return callCount == 1
                    ? TestFactory.CompletedWithToolCall(requestId, callId, "hallucinated_tool")
                    : TestFactory.CompletedWithText(secondRequestId);
            },
            resolvedToolHandler: static request => new ResolvedToolInvocation(
                request.Tool,
                ToolResultProjectionPolicyReference.Default,
                new ToolInvocationResult(
                    new ToolCallOutcome(
                        ToolCallOutcomeKind.Rejected,
                        ToolTerminalStatus.UnknownTool,
                        SideEffectCertainty.DefinitelyNotPerformed,
                        retryable: false,
                        $"Tool '{request.Tool.ProviderAlias}' is not registered.",
                        ExtensionData.Empty),
                    [])));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var result = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        _ = toolInvoker.ReceivedRequests.ShouldHaveSingleItem();
        var toolMessage = result.NewMessages[1].ShouldBeOfType<ToolMessage>();
        var toolResult = toolMessage.Parts[0].ShouldBeOfType<ToolResultPart>();
        toolResult.CallId.ShouldBe(callId);
        toolResult.Tool.IsResolved.ShouldBeFalse();
        toolResult.Tool.Id.ShouldBeNull();
        toolResult.Tool.ProviderAlias.ShouldBe(new ToolAlias("hallucinated_tool"));
        toolResult.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        toolResult.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.UnknownTool);
        toolResult.Projection.Policy.ShouldBe(ToolResultProjectionPolicyReference.Default);
        toolResult.Projection.Losses.ShouldBeEmpty();
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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        _ = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(request, _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer }, _services,
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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer }, _services,
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
                new ToolCallPart(callId1, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                new ToolCallPart(callId2, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                new ToolCallPart(callId3, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token);

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
                new ToolCallPart(callId1, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                new ToolCallPart(callId2, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 1), _services, TestContext.Current.CancellationToken);
        _ = first.Outcome.ShouldBeOfType<AgentRunTurnLimitReached>();

        var second = await loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId, maxTurns: 4), _services, TestContext.Current.CancellationToken);

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
            TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token);

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
            () => loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token));

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, cts.Token);

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
                new ToolCallPart(callId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
                new ToolCallPart(callId, new ToolReference(new ToolAlias("search"), null, null), default, null, ExtensionData.Empty),
            ],
            NormalizedStopReason.ToolUse));
        var loop = CreateLoop(
            out var coordinator, out var invoker, _ => ++calls == 1 ? response : TestFactory.CompletedWithText(requestId));
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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

        var result = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);

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
            _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), _services, TestContext.Current.CancellationToken);
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
        Func<ToolCallRequest, ResolvedToolInvocation>? resolvedToolHandler = null,
        ModelResponseEvent? modelEvent = null,
        IContextAssembler? contextAssembler = null,
        IRunContinuationPolicy? continuationPolicy = null,
        FakeSecurityProfileSelector? securityProfileSelector = null,
        AgentLoopOptions? options = null,
        TimeProvider? timeProvider = null,
        Microsoft.Extensions.Logging.ILogger<DefaultAgentLoop>? logger = null)
    {
        _ = maxTurns;
        coordinator = new FakeSessionCoordinator(_branchId);
        toolInvoker = resolvedToolHandler is not null
            ? new FakeToolInvoker(resolvedToolHandler)
            : new FakeToolInvoker(toolHandler ?? (_ => toolResult ?? TestFactory.SuccessResult()));
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), respond, modelEvent);
        var descriptor = TestFactory.Model();

        _services = new AgentRunServices(
            coordinator,
            securityProfileSelector ?? new FakeSecurityProfileSelector(),
            contextAssembler ?? new DefaultContextAssembler(),
            toolInvoker,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            FakeModelSelector.Selecting(descriptor),
            new FakeLlmModelResolver(adapter),
            continuationPolicy ?? new DefaultRunContinuationPolicy(TimeProvider.System));

        return new DefaultAgentLoop(
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            timeProvider ?? TimeProvider.System,
            new FakeOptionsMonitor<AgentLoopOptions>(options ?? new AgentLoopOptions()),
            TestLoopKey,
            logger);
    }

    private DefaultAgentLoop CreateLoopWith(
        FakeSessionCoordinator coordinator,
        IModelCatalog catalog,
        IModelSelector selector,
        ILlmModelResolver resolver,
        IContextAssembler? contextAssembler = null,
        Microsoft.Extensions.Logging.ILogger<DefaultAgentLoop>? logger = null)
    {
        _services = new AgentRunServices(
            coordinator,
            new FakeSecurityProfileSelector(),
            contextAssembler ?? new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            catalog,
            selector,
            resolver,
            new DefaultRunContinuationPolicy(TimeProvider.System));

        return new DefaultAgentLoop(
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            new FakeOptionsMonitor<AgentLoopOptions>(new AgentLoopOptions()),
            TestLoopKey,
            logger);
    }

    private static GuidIdentifierGenerator<T> IdGenerator<T>(Func<Guid, T> factory)
        where T : struct =>
        new(factory);

    /// <summary>Builds the canonical JSON extension value the projector records for <paramref name="value"/>.</summary>
    private static ExtensionValue Json<T>(T value) => new([.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value)]);

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
