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
    public async Task RunAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var loop = CreateLoop(out _, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => loop.RunAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task RunAsync_WhenHistoryCannotBeLoaded_ReturnsAgentRunSessionOperationFailed()
    {
        var loop = CreateLoop(out var coordinator, out _, static _ => TestFactory.CompletedWithText(new ModelRequestId(Guid.NewGuid())));
        coordinator.ReadOverride = static request => new SessionReadFailed("store unavailable");

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunSessionOperationFailed>();
        result.NewMessages.ShouldBeEmpty();
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
        turn.ParentSpanId.ShouldBe(run.SpanId);
        context.ParentSpanId.ShouldBe(turn.SpanId);
        model.ParentSpanId.ShouldBe(turn.SpanId);
        commit.ParentSpanId.ShouldBe(turn.SpanId);
        stopped.SelectMany(static activity => activity.TagObjects)
            .Select(static tag => tag.Value?.ToString())
            .ShouldNotContain(protectedOutput);
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

        var loop = CreateLoop(
            out var coordinator, out var toolInvoker, _ => TestFactory.CompletedWithToolCall(requestId, callId), maxTurns: 1);
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

            // Simulate a tool invoked mid-turn (the plan/todo tool, for one) committing its own session entry
            // directly through the coordinator, independently of this run's own version tracking.
            coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, request.ExpectedVersion.Value + 1)]);
            return new SessionAppendConflict(request.ExpectedVersion, coordinator.Version);
        };

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        _ = result.Outcome.ShouldBeOfType<AgentRunCompleted>();
        coordinator.ReceivedAppends.Count.ShouldBe(2);
        coordinator.ReceivedAppends[1].ExpectedVersion.Value.ShouldBe(2);
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

        _ = await Should.ThrowAsync<OperationCanceledException>(() => loop.RunAsync(
            TestFactory.RunRequest(_agentId, _sessionId, _branchId) with { Observer = observer },
            cts.Token));

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
    public async Task RunAsync_WhenAToolCallIsCancelledMidBatch_SettlesEveryRequestedCallBeforePropagatingCancellation()
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

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token));

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
    public async Task RunAsync_WhenAToolAbsorbsCancellationIntoASettledResult_StopsRemainingCallsAndStillPropagatesCancellation()
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

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token));

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
            // A concurrent writer commits two entries in one version bump: Version 1 -> 2, NextSequence 1 -> 3.
            coordinator.SimulateConcurrentAppend(
            [
                TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 2, "concurrent-a"),
                TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 3, "concurrent-b"),
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

        try
        {
            _ = await loop.RunAsync(TestFactory.RunRequest(_agentId, _sessionId, _branchId), cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Either a typed AgentRunCancelled or a thrown OCE is acceptable for the caller, but
            // the partial output must have been committed either way.
        }

        var interrupted = coordinator.Entries.OfType<MessageSessionEntry>()
            .Select(static entry => entry.Message)
            .OfType<AssistantMessage>()
            .ToArray();
        _ = interrupted.ShouldHaveSingleItem();
        interrupted[0].State.ShouldBe(MessageState.Interrupted);
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
        ModelResponseEvent? modelEvent = null)
    {
        _ = maxTurns;
        coordinator = new FakeSessionCoordinator(_branchId);
        toolInvoker = new FakeToolInvoker(toolHandler ?? (_ => toolResult ?? TestFactory.SuccessResult()));
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), respond, modelEvent);
        var descriptor = TestFactory.Model();

        return new DefaultAgentLoop(
            coordinator,
            new FakeSecurityProfileSelector(),
            new DefaultContextAssembler(),
            toolInvoker,
            new FakeModelCatalog(TestFactory.Catalog(descriptor)),
            FakeModelSelector.Selecting(descriptor),
            new FakeLlmModelResolver(adapter),
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions()));
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
