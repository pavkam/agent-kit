// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies asynchronous ownership settlement and operation rejection.</summary>
public sealed class AsyncOwnedConversationSessionTests
{
    [Fact]
    public void SessionId_WhenRead_ForwardsToTheWrappedConversation()
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var owned = new AsyncOwnedConversationSession(
            new StubSession { SessionId = sessionId, BranchId = branchId },
            new RecordingAsyncDisposable());

        owned.SessionId.ShouldBe(sessionId);
        owned.BranchId.ShouldBe(branchId);
    }

    [Fact]
    public async Task ReadHistoryAsync_WhenInnerUsesCompatibleDefault_ReturnsTypedUnavailableFallback()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);

        var result = await sut.ReadHistoryAsync(
            new SessionSequence(0),
            10,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationHistoryUnavailable>().SafeMessage.ShouldContain("does not support");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenNoObserverIsSupplied_ForwardsThePlainOverload()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);

        var result = await sut.SendAsync("hi", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenRepeated_SharesCompletionAndInvokesOwnerOnce()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);

        var first = sut.DisposeAsync();
        var second = sut.DisposeAsync();
        owner.Completion.SetResult();
        await first;
        await second;

        owner.DisposeCount.ShouldBe(1);
        _ = await Should.ThrowAsync<ObjectDisposedException>(() => sut.SendAsync("later"));
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnerFails_RepeatedCallsObserveSameFailureWithoutRetry()
    {
        var expected = new InvalidOperationException("dispose failed");
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);
        var first = sut.DisposeAsync();
        owner.Completion.SetException(expected);

        var firstFailure = await Should.ThrowAsync<InvalidOperationException>(async () => await first);
        var secondFailure = await Should.ThrowAsync<InvalidOperationException>(async () => await sut.DisposeAsync());

        firstFailure.ShouldBeSameAs(expected);
        secondFailure.ShouldBeSameAs(expected);
        owner.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task OpenAsync_WhenInnerDoesNotOverrideIt_ReturnsRejectionFromTheCompatibleDefault()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);

        var result = await sut.OpenAsync(new SessionId(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationSessionOpenRejected>().SafeMessage.ShouldContain("does not support");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task ListAsync_WhenInnerDoesNotOverrideIt_ReturnsUnavailableFromTheCompatibleDefault()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);

        var result = await sut.ListAsync(null, 10, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ConversationSessionListUnavailable>().SafeMessage.ShouldContain("does not support");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task PresentToolAsync_WhenInnerDoesNotOverrideIt_ReturnsNullFromTheCompatibleDefault()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);
        var call = FakeMessages.ToolCall("search", "{}");

        var presentation = await sut.PresentToolAsync(call, TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task PresentToolAsync_WhenInnerDoesNotOverrideItAndPartIsAToolResult_ReturnsNullFromTheCompatibleDefault()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);
        var result = FakeMessages.ToolSuccess(FakeMessages.ToolCall("search", "{}"), "found it");

        var presentation = await sut.PresentToolAsync(result, TestContext.Current.CancellationToken);

        presentation.ShouldBeNull();
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task PresentToolAsync_WhenInnerDoesNotOverrideItAndPartIsNeitherACallNorAResult_ThrowsArgumentException()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);
        var part = new TextPart("not a tool part", TextSemantics.Plain, ExtensionData.Empty);

        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await sut.PresentToolAsync(part, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("part");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WithObserver_WhenInnerDoesNotOverrideIt_DeliversFallbackEventsThenSettledTerminal()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);
        var observer = new RecordingConversationEventObserver();

        var result = await sut.SendAsync("hi", observer, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeTrue();
        terminal.Outcome.ShouldBe("settled");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WithObserver_WhenInnerReportsFailure_DeliversAFailedTerminalEvent()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(
            new StubSession { SendOverride = static (_, _) => Task.FromResult(new ConversationTurnResult(false, [])) },
            owner);
        var observer = new RecordingConversationEventObserver();

        var result = await sut.SendAsync("hi", observer, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeFalse();
        terminal.Outcome.ShouldBe("failed");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WithObserver_WhenInnerIsCancelled_DeliversACancelledTerminalEventAndPropagates()
    {
        var owner = new RecordingAsyncDisposable();
        using var cts = new CancellationTokenSource();
        var sut = new AsyncOwnedConversationSession(
            new StubSession
            {
                SendOverride = (_, token) =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(token.IsCancellationRequested ? token : cts.Token);
                },
            },
            owner);
        var observer = new RecordingConversationEventObserver();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await sut.SendAsync("hi", observer, cts.Token));

        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeFalse();
        terminal.Outcome.ShouldBe("cancelled");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WithObserver_WhenInnerFaults_DeliversAFaultedTerminalEventAndPropagates()
    {
        var owner = new RecordingAsyncDisposable();
        var fault = new InvalidOperationException("inner fault");
        var sut = new AsyncOwnedConversationSession(
            new StubSession { SendOverride = (_, _) => throw fault },
            owner);
        var observer = new RecordingConversationEventObserver();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.SendAsync("hi", observer, TestContext.Current.CancellationToken));

        exception.ShouldBeSameAs(fault);
        var terminal = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        terminal.Succeeded.ShouldBeFalse();
        terminal.Outcome.ShouldBe("faulted");
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WithObserver_WhenFallbackDeliveryFaults_StillReturnsTheCommittedResult()
    {
        var owner = new RecordingAsyncDisposable();
        var sut = new AsyncOwnedConversationSession(new StubSession(), owner);
        var observer = new RecordingConversationEventObserver { ThrowAfterRecording = true };

        var result = await sut.SendAsync("hi", observer, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        _ = observer.Events.OfType<ConversationTurnCompletedEvent>().ShouldHaveSingleItem();
        owner.Completion.SetResult();
        await sut.DisposeAsync();
    }

    private sealed class StubSession: IConversationSession
    {
        public SessionId? SessionId { get; init; }

        public BranchId? BranchId { get; init; }

        internal Func<string, CancellationToken, Task<ConversationTurnResult>>? SendOverride { get; set; }

        public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default) =>
            SendOverride?.Invoke(userText, cancellationToken) ?? Task.FromResult(new ConversationTurnResult(true, []));
    }

    private sealed class RecordingAsyncDisposable: IAsyncDisposable
    {
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return new ValueTask(Completion.Task);
        }
    }
}
