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
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))),
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
        result.NewMessages.Length.ShouldBe(1);
        _ = result.NewMessages[0].ShouldBeOfType<AssistantMessage>();
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

    private DefaultAgentLoop CreateLoop(
        out FakeSessionCoordinator coordinator,
        out FakeToolInvoker toolInvoker,
        Func<LlmModelRequest, ModelAttemptResult> respond,
        int maxTurns = 8,
        ToolInvocationResult? toolResult = null)
    {
        _ = maxTurns;
        coordinator = new FakeSessionCoordinator(_branchId);
        toolInvoker = new FakeToolInvoker(_ => toolResult ?? TestFactory.SuccessResult());
        var adapter = new RespondingLlmModel(new ModelAlias("chat"), respond);
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
        ILlmModelResolver resolver) =>
        new(
            coordinator,
            new FakeSecurityProfileSelector(),
            new DefaultContextAssembler(),
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

        public RespondingLlmModel(ModelAlias alias, Func<LlmModelRequest, ModelAttemptResult> respond)
        {
            Alias = alias;
            _respond = respond;
        }

        public ModelAlias Alias { get; }

        public Task<ModelAttemptResult> ExecuteAsync(
            LlmModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default) =>
            Task.FromResult(_respond(request));
    }
}
