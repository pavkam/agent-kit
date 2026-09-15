// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

using Microsoft.Extensions.Options;

/// <summary>Verifies DefaultConversationSession behavior and contracts.</summary>
public sealed class DefaultConversationSessionTests
{
    [Fact]
    public async Task SendAsync_WhenSnapshotVersionDiffersFromUpperSequence_UsesEachExactCoordinate()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = request => new SessionPage(
                [],
                request.FromSequenceExclusive,
                hasMore: false,
                new SessionReadSnapshot(request.Context.ToAddress(), request.BranchId, new SessionVersion(1), new SessionSequence(2))),
        };
        var session = CreateSession(coordinator);

        _ = await session.SendAsync("hello", TestContext.Current.CancellationToken);

        var append = coordinator.LastAppendRequest.ShouldNotBeNull();
        append.ExpectedVersion.ShouldBe(new SessionVersion(1));
        append.Entries.ShouldHaveSingleItem().Sequence.ShouldBe(new SessionSequence(3));
    }

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
                FakeMessages.Tool(request, [
                    FakeMessages.ToolSuccess(call1, "42"),
                    FakeMessages.ToolFailure(call2, "boom", "stderr details"),
                ]),
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
        toolCallEvent.CallId.ShouldBe(call1.CallId);
        toolCallEvent.ArgumentsJson.ShouldBe(/*lang=json,strict*/ "{\"q\":\"test\"}");
        var successEvent = result.Events[2].ShouldBeOfType<ConversationToolResultEvent>();
        successEvent.ToolName.ShouldBe("search");
        successEvent.CallId.ShouldBe(call1.CallId);
        successEvent.Succeeded.ShouldBeTrue();
        successEvent.Summary.ShouldBe("42");
        var failureEvent = result.Events[3].ShouldBeOfType<ConversationToolResultEvent>();
        failureEvent.ToolName.ShouldBe("write");
        failureEvent.CallId.ShouldBe(call2.CallId);
        failureEvent.Succeeded.ShouldBeFalse();
        failureEvent.Summary.ShouldBe("boom\nstderr details");
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
    public async Task SendAsync_WhenLoopFailsWithProviderFailureCarryingDiagnosticCause_DoesNotExposeItInEvents()
    {
        // ProviderFailure.DiagnosticCause "may carry sensitive transport detail and is intended for logs and diagnostics only".
        const string secret = "Bearer sk-live-SECRET-TOKEN";
        var failure = new ProviderFailure(
            ProviderFailureKind.Unavailable,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: 503,
            providerCode: null,
            retryAfter: null,
            "The provider is unavailable.",
            new HttpRequestException($"connection refused while sending {secret}"),
            ExtensionData.Empty);
        var loop = new FakeAgentLoop
        {
            ResultFactory = request => new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId, new AgentRunFailed(failure), [], new SessionVersion(1)),
        };
        using var session = CreateSession(loop: loop);

        var result = await session.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        foreach (var text in result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text))
        {
            text.ShouldNotContain(secret);
            text.ShouldNotContain("HttpRequestException");
            text.ShouldNotContain("DiagnosticCause");
        }
    }

    [Fact]
    public async Task Dispose_WhenCalledDuringAnInFlightTurn_DoesNotReplaceTheTurnResultWithObjectDisposedException()
    {
        var loop = new FakeAgentLoop { Gate = new TaskCompletionSource(), EnteredSignal = new TaskCompletionSource() };
        var session = CreateSession(loop: loop);
        var pending = session.SendAsync("hi", TestContext.Current.CancellationToken);
        await loop.EnteredSignal.Task;

        session.Dispose();
        loop.Gate.SetResult();

        // The loop ran and committed; the caller must receive that result (or a typed failure), never a disposal fault.
        var exception = await Record.ExceptionAsync(async () => await pending);
        exception.ShouldNotBeOfType<ObjectDisposedException>();
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

    [Fact]
    public async Task SendAsync_WhenObserved_ForwardsIncrementalTextBeforeCompletionAndOneTerminalEvent()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var loop = new FakeAgentLoop
        {
            Gate = new TaskCompletionSource(),
            EnteredSignal = new TaskCompletionSource(),
            ProgressEvent = new AgentRunModelResponseEvent(
                new TurnId(Guid.NewGuid()),
                new ModelPartDelta(requestId, 1, 0, new TextContentDelta("hello "))),
        };
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession(loop: loop);

        var pending = session.SendAsync("hi", observer, TestContext.Current.CancellationToken);
        await loop.EnteredSignal.Task;

        pending.IsCompleted.ShouldBeFalse();
        observer.Events.ShouldHaveSingleItem().ShouldBeOfType<ConversationAssistantTextDeltaEvent>()
            .Text.ShouldBe("hello ");

        loop.Gate.SetResult();
        var result = await pending;

        result.Succeeded.ShouldBeTrue();
        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeTrue();
        observer.Events[^1].ShouldBeSameAs(terminal);
    }

    [Fact]
    public async Task SendAsync_WhenObservedTurnIsCancelled_EmitsOneCancelledTerminalEventAndPropagatesCancellation()
    {
        var observer = new RecordingConversationEventObserver();
        using var session = CreateSession();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await session.SendAsync("hi", observer, cts.Token));

        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeFalse();
        terminal.Outcome.ShouldBe("cancelled");
        _ = observer.Events.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task OpenAsync_WhenSessionIsVisible_ReusesAuthoritativeSessionAndBranch()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);

        var opened = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        _ = await session.SendAsync("continue", TestContext.Current.CancellationToken);

        var success = opened.ShouldBeOfType<ConversationSessionOpened>();
        success.SessionId.ShouldBe(sessionId);
        success.BranchId.ShouldBe(coordinator.BranchId);
        coordinator.CreateCallCount.ShouldBe(0);
        coordinator.LastAppendRequest.ShouldNotBeNull().Context.SessionId.ShouldBe(sessionId);
    }

    [Fact]
    public async Task OpenAsync_WhenConversationAlreadyRan_RejectsWithoutRebinding()
    {
        var coordinator = new FakeSessionCoordinator();
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.SendAsync("first", TestContext.Current.CancellationToken);

        var result = await session.OpenAsync(new SessionId(Guid.NewGuid()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ConversationSessionOpenRejected>();
        coordinator.LoadCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task OpenAsync_WhenLoadedOwnerDiffers_RejectsWithoutBindingSession()
    {
        var coordinator = new FakeSessionCoordinator();
        var requested = new SessionId(Guid.NewGuid());
        coordinator.LoadResult = new SessionLoaded(new SessionDescriptor(
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, requested),
            null,
            ConversationSessionOptionsFactory.Identity.TenantId,
            new PrincipalId("another-owner"),
            new SessionStoreKey("test-store"),
            coordinator.BranchId,
            new SessionVersion(1),
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            ExtensionData.Empty));
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.OpenAsync(requested, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ConversationSessionOpenRejected>();
        coordinator.CreateCallCount.ShouldBe(0);
        coordinator.AppendCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenSessionIsNotBound_ReturnsUnavailableWithoutCreatingOrReading()
    {
        var coordinator = new FakeSessionCoordinator();
        var selector = new FakeSecurityProfileSelector();
        using var session = CreateSession(coordinator: coordinator, selector: selector);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("Open");
        coordinator.CreateCallCount.ShouldBe(0);
        coordinator.ReadCallCount.ShouldBe(0);
        selector.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenPagesRemain_UsesStableExclusiveCursorAndCompleteness()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var firstMessage = HistoryEntry(sessionId, coordinator.BranchId, 1, "first");
        var secondMessage = HistoryEntry(sessionId, coordinator.BranchId, 2, "second");
        coordinator.ReadResultFactory = request => request.FromSequenceExclusive.Value switch
        {
            0 => new SessionPage([firstMessage], new SessionSequence(1), hasMore: true),
            1 => new SessionPage([secondMessage], new SessionSequence(2), hasMore: false),
            _ => new SessionPage([], request.FromSequenceExclusive, hasMore: false),
        };
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var first = (await session.ReadHistoryAsync(
            new SessionSequence(0),
            1,
            TestContext.Current.CancellationToken)).ShouldBeOfType<ConversationHistoryPage>();
        var second = (await session.ReadHistoryAsync(
            first.NextCursor,
            1,
            TestContext.Current.CancellationToken)).ShouldBeOfType<ConversationHistoryPage>();

        first.Messages.ShouldHaveSingleItem().ShouldBeSameAs(firstMessage.Message);
        first.NextCursor.ShouldBe(new SessionSequence(1));
        first.Complete.ShouldBeFalse();
        second.Messages.ShouldHaveSingleItem().ShouldBeSameAs(secondMessage.Message);
        second.NextCursor.ShouldBe(new SessionSequence(2));
        second.Complete.ShouldBeTrue();
        coordinator.ReadCallCount.ShouldBe(2);
        coordinator.LastReadRequest.ShouldNotBeNull().FromSequenceExclusive.ShouldBe(first.NextCursor);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenPageContainsOperationalEntries_ProjectsOnlyMessagesAndAdvancesCursor()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var message = HistoryEntry(sessionId, coordinator.BranchId, 2, "visible");
        var admissionId = new AdmissionId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var operationalEntry = new InputPromotedSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, new TurnId(Guid.NewGuid())),
            coordinator.BranchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new ExecutionLaneId(Guid.NewGuid()),
            admissionId,
            new SessionSequence(1),
            [admissionId]);
        coordinator.ReadResultFactory = _ => new SessionPage(
            [operationalEntry, message],
            new SessionSequence(2),
            hasMore: false);
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            2,
            TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<ConversationHistoryPage>();
        page.Messages.ShouldHaveSingleItem().ShouldBeSameAs(message.Message);
        page.NextCursor.ShouldBe(new SessionSequence(2));
        page.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCancelled_PropagatesWithoutReading()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await session.ReadHistoryAsync(new SessionSequence(0), 10, cancellation.Token));

        coordinator.ReadCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorReadFails_ReturnsContentSafeUnavailableResult()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = _ => new SessionReadFailed("History storage is temporarily unavailable."),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage
            .ShouldBe("History storage is temporarily unavailable.");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorThrows_DoesNotExposeExceptionContent()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = _ => throw new InvalidOperationException("stored conversation secret"),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        var unavailable = result.ShouldBeOfType<ConversationHistoryUnavailable>();
        unavailable.SafeMessage.ShouldBe("Conversation history is temporarily unavailable.");
        unavailable.SafeMessage.ShouldNotContain("secret");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenCoordinatorReturnsStalledPage_ReturnsUnavailableInsteadOfLooping()
    {
        var coordinator = new FakeSessionCoordinator
        {
            ReadResultFactory = request => new SessionPage([], request.FromSequenceExclusive, hasMore: true),
        };
        var sessionId = new SessionId(Guid.NewGuid());
        using var session = CreateSession(coordinator: coordinator);
        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);

        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("pagination");
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenSessionWasReopened_ReadsBoundBranchWithCapturedAuthorizationAndProfile()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        var retainedMessage = HistoryEntry(sessionId, coordinator.BranchId, 1, "retained after restart");
        coordinator.ReadResultFactory = _ => new SessionPage(
            [retainedMessage],
            new SessionSequence(1),
            hasMore: false);
        using var session = CreateSession(coordinator: coordinator);

        _ = await session.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        var result = await session.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryPage>().Messages.ShouldHaveSingleItem()
            .ShouldBeSameAs(retainedMessage.Message);
        var request = coordinator.LastReadRequest.ShouldNotBeNull();
        request.Context.SessionId.ShouldBe(sessionId);
        request.BranchId.ShouldBe(coordinator.BranchId);
        request.Context.Identity.ShouldBe(ConversationSessionOptionsFactory.Identity);
        request.Context.Authorization.Scope.SessionId.ShouldBe(sessionId);
        request.Context.Authorization.Scope.Correlation.ShouldBe(request.Context.Correlation);
        coordinator.LastReadProfile.ShouldBe(TestSecurityEvidence.SessionProfile());
    }

    [Fact]
    public async Task ListAsync_WhenDirectoryReturnsRoutes_ProjectsBoundedSummaries()
    {
        var coordinator = new FakeSessionCoordinator();
        var sessionId = new SessionId(Guid.NewGuid());
        coordinator.ListResult = new SessionDirectoryPage(
            [new SessionLocation(
                new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
                ConversationSessionOptionsFactory.Identity.TenantId,
                new SessionStoreKey("sqlite"),
                new SessionDirectoryRevision(1),
                DateTimeOffset.UnixEpoch,
                new SchemaVersion("1"))],
            sessionId);
        using var session = CreateSession(coordinator: coordinator);

        var result = await session.ListAsync(null, 10, TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<ConversationSessionPage>();
        page.Sessions.ShouldHaveSingleItem().SessionId.ShouldBe(sessionId);
        page.NextCursor.ShouldBe(sessionId);
    }

    private static MessageSessionEntry HistoryEntry(
        SessionId sessionId,
        BranchId branchId,
        long sequence,
        string text)
    {
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var message = new UserMessage(
            new MessageId(Guid.NewGuid()),
            ConversationSessionOptionsFactory.AgentId,
            sessionId,
            null,
            branchId,
            runId,
            turnId,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        return new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(ConversationSessionOptionsFactory.AgentId, sessionId),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId),
            branchId,
            new SessionSequence(sequence),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            message);
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
