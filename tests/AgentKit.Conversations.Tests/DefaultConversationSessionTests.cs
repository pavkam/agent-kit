// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

using Microsoft.Extensions.Options;

/// <summary>Verifies DefaultConversationSession behavior and contracts.</summary>
public sealed class DefaultConversationSessionTests
{
    [Fact]
    public void Constructor_WhenSessionCoordinatorIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            null!, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, args.OperationIds, args.MessageIds,
            args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("sessionCoordinator");
    }

    [Fact]
    public void Constructor_WhenSecurityProfileSelectorIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, null!, args.AgentLoop, args.RunIds, args.OperationIds, args.MessageIds,
            args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("securityProfileSelector");
    }

    [Fact]
    public void Constructor_WhenAgentLoopIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, null!, args.RunIds, args.OperationIds,
            args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("agentLoop");
    }

    [Fact]
    public void Constructor_WhenRunIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, null!, args.OperationIds,
            args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("runIds");
    }

    [Fact]
    public void Constructor_WhenOperationIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, null!,
            args.MessageIds, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("operationIds");
    }

    [Fact]
    public void Constructor_WhenMessageIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, args.OperationIds,
            null!, args.SessionEntryIds, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("messageIds");
    }

    [Fact]
    public void Constructor_WhenSessionEntryIdsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, args.OperationIds,
            args.MessageIds, null!, args.TimeProvider, args.Options));

        exception.ParamName.ShouldBe("sessionEntryIds");
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, args.OperationIds,
            args.MessageIds, args.SessionEntryIds, null!, args.Options));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, args.OperationIds,
            args.MessageIds, args.SessionEntryIds, args.TimeProvider, null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenOptionsValueIsNull_ThrowsArgumentNullException()
    {
        var args = ValidArgs();

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultConversationSession(
            args.SessionCoordinator, args.SecurityProfileSelector, args.AgentLoop, args.RunIds, args.OperationIds,
            args.MessageIds, args.SessionEntryIds, args.TimeProvider, new NullValueOptions()));

        exception.ParamName.ShouldBe("optionValues");
    }

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.AgentId = default));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => CreateSession(configureOptions: options => options.Identity = null));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenSecurityProfileKeyIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.SecurityProfileKey = default));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenConfigurationVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.ConfigurationVersion = default));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenSessionProfileIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => CreateSession(configureOptions: options => options.SessionProfile = null));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenModelSelectionPolicyIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => CreateSession(configureOptions: options => options.ModelSelectionPolicy = null));

        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.MaxTurns = maxTurns));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.AttemptTimeout = TimeSpan.Zero));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateSession(configureOptions: options => options.AttemptTimeout = TimeSpan.FromSeconds(-1)));

        exception.ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenUserTextIsBlank_ThrowsArgumentException(string? userText)
    {
        using var session = CreateSession();

        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await session.SendAsync(userText!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("userText");
    }

    [Fact]
    public async Task SendAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var session = CreateSession();
        session.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        var session = CreateSession();

        session.Dispose();
        Should.NotThrow(session.Dispose);
    }

    [Fact]
    public async Task SendAsync_WhenFirstCalled_CreatesSessionAppendsMessageAndProjectsLoopEvents()
    {
        var coordinator = new FakeSessionCoordinator();
        var loop = new FakeAgentLoop();
        var call1 = FakeMessages.ToolCall("search", /*lang=json,strict*/ "{\"q\":\"test\"}");
        var call2 = FakeMessages.ToolCall("write", /*lang=json,strict*/ "{\"path\":\"a\"}");
        loop.ResultFactory = request => new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new AgentRunCompleted(FakeMessages.Assistant(
                request,
                [
                    new TextPart("   ", TextSemantics.Plain, ExtensionData.Empty),
                    new TextPart("Hello there", TextSemantics.Plain, ExtensionData.Empty),
                    call1,
                ])),
            [
                FakeMessages.Assistant(
                    request,
                    [
                        new TextPart("   ", TextSemantics.Plain, ExtensionData.Empty),
                        new TextPart("Hello there", TextSemantics.Plain, ExtensionData.Empty),
                        call1,
                    ]),
                FakeMessages.Tool(request, [FakeMessages.ToolSuccess(call1, "42"), FakeMessages.ToolFailure(call2, "boom")]),
            ],
            new SessionVersion(2));
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(1);
        loop.CallCount.ShouldBe(1);

        var committedEntry = coordinator.LastAppendRequest.ShouldNotBeNull().Entries[0].ShouldBeOfType<MessageSessionEntry>();
        var committedMessage = committedEntry.Message.ShouldBeOfType<UserMessage>();
        committedMessage.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("hi");

        result.Events.Length.ShouldBe(4);
        result.Events[0].ShouldBeOfType<ConversationAssistantTextEvent>().Text.ShouldBe("Hello there");
        var toolCallEvent = result.Events[1].ShouldBeOfType<ConversationToolCallEvent>();
        toolCallEvent.ToolName.ShouldBe("search");
        toolCallEvent.ArgumentsJson.ShouldBe(/*lang=json,strict*/ "{\"q\":\"test\"}");
        var successEvent = result.Events[2].ShouldBeOfType<ConversationToolResultEvent>();
        successEvent.ToolName.ShouldBe("search");
        successEvent.Succeeded.ShouldBeTrue();
        successEvent.Summary.ShouldBe("42");
        var failureEvent = result.Events[3].ShouldBeOfType<ConversationToolResultEvent>();
        failureEvent.ToolName.ShouldBe("write");
        failureEvent.Succeeded.ShouldBeFalse();
        failureEvent.Summary.ShouldBe("boom");
    }

    [Fact]
    public async Task SendAsync_WhenAssistantResponseReportsUsage_ProjectsAUsageEventAfterItsContent()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 120, 45, null, null, 0.002m, "USD", ExtensionData.Empty);
        var loop = new FakeAgentLoop
        {
            ResultFactory = request =>
            {
                var assistant = FakeMessages.Assistant(
                    request,
                    [new TextPart("Hello there", TextSemantics.Plain, ExtensionData.Empty)],
                    usage);
                return new AgentLoopResult(
                    request.AgentId,
                    request.SessionId,
                    request.BranchId,
                    request.RunId,
                    new AgentRunCompleted(assistant),
                    [assistant],
                    new SessionVersion(1));
            },
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.Length.ShouldBe(2);
        result.Events[0].ShouldBeOfType<ConversationAssistantTextEvent>().Text.ShouldBe("Hello there");
        result.Events[1].ShouldBeOfType<ConversationUsageEvent>().Usage.ShouldBe(usage);
    }

    [Fact]
    public async Task SendAsync_WhenAssistantResponseDoesNotReportUsage_ProjectsNoUsageEvent()
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request =>
            {
                var assistant = FakeMessages.Assistant(request, "Hello there");
                return new AgentLoopResult(
                    request.AgentId,
                    request.SessionId,
                    request.BranchId,
                    request.RunId,
                    new AgentRunCompleted(assistant),
                    [assistant],
                    new SessionVersion(1));
            },
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Events.ShouldNotContain(conversationEvent => conversationEvent is ConversationUsageEvent);
    }

    [Fact]
    public async Task SendAsync_WhenLoopOutcomeIsNotCompleted_ReturnsUnsuccessfulResultDescribingWhy()
    {
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId,
                request.SessionId,
                request.BranchId,
                request.RunId,
                new AgentRunTurnLimitReached(request.MaxTurns),
                [],
                new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        var description = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextEvent>();
        description.Text.ShouldContain("turn limit");
    }

    [Fact]
    public async Task SendAsync_WhenCalledASecondTime_ReusesTheExistingSessionAndBranch()
    {
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator);

        _ = await session.SendAsync("first", TestContext.Current.CancellationToken);
        _ = await session.SendAsync("second", TestContext.Current.CancellationToken);

        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(2);
        coordinator.LoadCallCount.ShouldBe(2);
        var secondAppend = coordinator.LastAppendRequest.ShouldNotBeNull();
        secondAppend.Context.SessionId.ShouldBe(coordinator.SessionId);
        secondAppend.BranchId.ShouldBe(coordinator.BranchId);
    }

    [Fact]
    public async Task SendAsync_WhenAppendDoesNotSucceed_ReturnsFailureWithoutRunningTheLoop()
    {
        var coordinator = new FakeSessionCoordinator { AppendResult = new SessionAppendFailed("no capacity") };
        var loop = new FakeAgentLoop();
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Events.Length.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ConversationAssistantTextEvent>().Text.ShouldContain("Could not record the message");
        loop.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenSessionCreationDoesNotSucceed_ThrowsInvalidOperationException()
    {
        var coordinator = new FakeSessionCoordinator { CreateResult = new SessionCreateFailed("test failure") };
        using var session = CreateSession(coordinator: coordinator);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_WhenAuthorizationCaptureDoesNotSucceed_ThrowsInvalidOperationException()
    {
        var selector = new FakeSecurityProfileSelector { Result = new SecurityAuthorizationCaptureUnavailable("unavailable") };
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator, selector: selector);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await session.SendAsync("hi", TestContext.Current.CancellationToken));

        coordinator.CreateCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenCancellationTokenIsAlreadyCancelled_PropagatesOperationCanceledException()
    {
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.SendAsync("hi", cts.Token));

        coordinator.CreateCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenCalledWhileAPriorTurnIsInFlight_SerializesRatherThanInterleaving()
    {
        var coordinator = new FakeSessionCoordinator();
        var loop = new FakeAgentLoop { Gate = new TaskCompletionSource(), EnteredSignal = new TaskCompletionSource() };
        using var session = CreateSession(coordinator: coordinator, loop: loop);

        var firstTurn = session.SendAsync("first", TestContext.Current.CancellationToken);
        await loop.EnteredSignal.Task;
        var secondTurn = session.SendAsync("second", TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(1);
        loop.CallCount.ShouldBe(1);

        loop.Gate.SetResult();
        var results = await Task.WhenAll(firstTurn, secondTurn);

        coordinator.CreateCallCount.ShouldBe(1);
        coordinator.AppendCallCount.ShouldBe(2);
        loop.CallCount.ShouldBe(2);
        results[0].Succeeded.ShouldBeTrue();
        results[1].Succeeded.ShouldBeTrue();
    }

    private static DefaultConversationSession CreateSession(
        ISessionCoordinator? coordinator = null,
        ISecurityProfileSelector? selector = null,
        IAgentLoop? loop = null,
        Action<ConversationSessionOptions>? configureOptions = null,
        TimeProvider? timeProvider = null) =>
        new(
            coordinator ?? new FakeSessionCoordinator(),
            selector ?? new FakeSecurityProfileSelector(),
            loop ?? new FakeAgentLoop(),
            new GuidIdentifierGenerator<RunId>(static guid => new RunId(guid)),
            new GuidIdentifierGenerator<OperationId>(static guid => new OperationId(guid)),
            new GuidIdentifierGenerator<MessageId>(static guid => new MessageId(guid)),
            new GuidIdentifierGenerator<SessionEntryId>(static guid => new SessionEntryId(guid)),
            timeProvider ?? new FakeTimeProvider(),
            Options.Create(ConversationSessionOptionsFactory.Valid(configureOptions)));

    private static (
        ISessionCoordinator SessionCoordinator,
        ISecurityProfileSelector SecurityProfileSelector,
        IAgentLoop AgentLoop,
        IIdentifierGenerator<RunId> RunIds,
        IIdentifierGenerator<OperationId> OperationIds,
        IIdentifierGenerator<MessageId> MessageIds,
        IIdentifierGenerator<SessionEntryId> SessionEntryIds,
        TimeProvider TimeProvider,
        IOptions<ConversationSessionOptions> Options) ValidArgs() => (
            new FakeSessionCoordinator(),
            new FakeSecurityProfileSelector(),
            new FakeAgentLoop(),
            new GuidIdentifierGenerator<RunId>(static guid => new RunId(guid)),
            new GuidIdentifierGenerator<OperationId>(static guid => new OperationId(guid)),
            new GuidIdentifierGenerator<MessageId>(static guid => new MessageId(guid)),
            new GuidIdentifierGenerator<SessionEntryId>(static guid => new SessionEntryId(guid)),
            new FakeTimeProvider(),
            Options.Create(ConversationSessionOptionsFactory.Valid()));

    private sealed class NullValueOptions: IOptions<ConversationSessionOptions>
    {
        public ConversationSessionOptions Value => null!;
    }
}
