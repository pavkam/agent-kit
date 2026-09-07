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
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            [],
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
    public void Constructor_WhenChatModelsShareAlias_ThrowsArgumentException()
    {
        var alias = new ModelAlias("dup");
        var modelA = new FakeChatModel(alias);
        var modelB = new FakeChatModel(alias);

        var exception = Should.Throw<ArgumentException>(() => new DefaultAgentLoop(
            new FakeSessionCoordinator(_branchId),
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            [modelA, modelB],
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions())));

        exception.ParamName.ShouldBe("chatModels");
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
    public async Task RunAsync_WhenNoModelRegisteredForAlias_ReturnsAgentRunFailed()
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        coordinator.Seed([TestFactory.SeedUserMessageEntry(_agentId, _sessionId, _branchId, 1)]);

        var loop = new DefaultAgentLoop(
            coordinator,
            new DefaultContextAssembler(),
            new FakeToolInvoker(_ => TestFactory.SuccessResult()),
            [],
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions()));

        var request = TestFactory.RunRequest(_agentId, _sessionId, _branchId);
        var result = await loop.RunAsync(request, TestContext.Current.CancellationToken);

        var failed = result.Outcome.ShouldBeOfType<AgentRunFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
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
                failure, [new TextPart("partial", TextSemantics.Plain, ExtensionData.Empty)], ModelUsage.Empty));
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

    private DefaultAgentLoop CreateLoop(
        out FakeSessionCoordinator coordinator,
        out FakeToolInvoker toolInvoker,
        Func<ChatModelRequest, ModelAttemptResult> respond,
        int maxTurns = 8)
    {
        _ = maxTurns;
        coordinator = new FakeSessionCoordinator(_branchId);
        toolInvoker = new FakeToolInvoker(_ => TestFactory.SuccessResult());
        var model = new RespondingChatModel(new ModelAlias("chat"), respond);

        return new DefaultAgentLoop(
            coordinator,
            new DefaultContextAssembler(),
            toolInvoker,
            [model],
            IdGenerator(static v => new OperationId(v)),
            IdGenerator(static v => new TurnId(v)),
            IdGenerator(static v => new ModelRequestId(v)),
            IdGenerator(static v => new MessageId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new AgentLoopOptions()));
    }

    private static GuidIdentifierGenerator<T> IdGenerator<T>(Func<Guid, T> factory)
        where T : struct =>
        new(factory);

    private sealed class RespondingChatModel: IChatModel
    {
        private readonly Func<ChatModelRequest, ModelAttemptResult> _respond;

        public RespondingChatModel(ModelAlias alias, Func<ChatModelRequest, ModelAttemptResult> respond)
        {
            Alias = alias;
            _respond = respond;
        }

        public ModelAlias Alias { get; }

        public Task<ModelAttemptResult> ExecuteAsync(
            ChatModelRequest request, IModelResponseObserver observer, CancellationToken cancellationToken = default) =>
            Task.FromResult(_respond(request));
    }
}
