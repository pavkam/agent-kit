// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies synchronous ownership and explicit conversation forwarding.</summary>
public sealed class OwnedConversationSessionTests
{
    [Fact]
    public async Task SendAsync_WhenObserverIsSupplied_ForwardsLiveOverloadAndToken()
    {
        var inner = new RecordingSession();
        var owner = new RecordingDisposable();
        using var sut = new OwnedConversationSession(inner, owner);
        var observer = new RecordingConversationEventObserver();
        using var cancellation = new CancellationTokenSource();

        _ = await sut.SendAsync("hello", observer, cancellation.Token);

        inner.LastOperation.ShouldBe("observed-send");
        inner.Observer.ShouldBeSameAs(observer);
        inner.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task SendAsync_WhenNoObserverIsSupplied_ForwardsThePlainOverload()
    {
        var inner = new RecordingSession();
        using var sut = new OwnedConversationSession(inner, new RecordingDisposable());

        var result = await sut.SendAsync("hello", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        inner.LastOperation.ShouldBe("send");
    }

    [Fact]
    public async Task PresentToolAsync_WhenCalled_ForwardsToTheInnerConversation()
    {
        var inner = new RecordingSession();
        using var sut = new OwnedConversationSession(inner, new RecordingDisposable());
        var call = FakeMessages.ToolCall("search", "{}");

        var presentation = await sut.PresentToolAsync(call, TestContext.Current.CancellationToken);

        presentation.ShouldBeSameAs(inner.Presentation);
        inner.LastPresentedPart.ShouldBeSameAs(call);
    }

    [Fact]
    public async Task PresentToolAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var sut = new OwnedConversationSession(new RecordingSession(), new RecordingDisposable());
        sut.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await sut.PresentToolAsync(FakeMessages.ToolCall("search", "{}")));
    }

    [Fact]
    public async Task OpenAsyncListAsyncAndReadHistoryAsync_WhenCalled_ForwardWithoutDefaultFallback()
    {
        var inner = new RecordingSession();
        using var sut = new OwnedConversationSession(inner, new RecordingDisposable());
        var sessionId = new SessionId(Guid.NewGuid());

        var opened = await sut.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        var listed = await sut.ListAsync(sessionId, 7, TestContext.Current.CancellationToken);
        var history = await sut.ReadHistoryAsync(new SessionSequence(3), 11, TestContext.Current.CancellationToken);

        opened.ShouldBeOfType<ConversationSessionOpened>().SessionId.ShouldBe(sessionId);
        _ = listed.ShouldBeOfType<ConversationSessionPage>();
        _ = history.ShouldBeOfType<ConversationHistoryPage>();
        inner.OpenedSessionId.ShouldBe(sessionId);
        inner.ListCursor.ShouldBe(sessionId);
        inner.MaximumResults.ShouldBe(7);
        inner.HistoryCursor.ShouldBe(new SessionSequence(3));
        inner.MaximumHistoryEntries.ShouldBe(11);
    }

    [Fact]
    public void Dispose_WhenRepeated_DisposesOwnerOnceAndRejectsLaterOperations()
    {
        var owner = new RecordingDisposable();
        var sut = new OwnedConversationSession(new RecordingSession(), owner);

        sut.Dispose();
        sut.Dispose();

        owner.DisposeCount.ShouldBe(1);
        _ = Should.Throw<ObjectDisposedException>(() => sut.SendAsync("later"));
    }

    private sealed class RecordingSession: IConversationSession
    {
        internal string? LastOperation { get; private set; }
        internal IConversationEventObserver? Observer { get; private set; }
        internal CancellationToken CancellationToken { get; private set; }
        internal SessionId? OpenedSessionId { get; private set; }
        internal SessionId? ListCursor { get; private set; }
        internal int MaximumResults { get; private set; }
        internal SessionSequence HistoryCursor { get; private set; }
        internal int MaximumHistoryEntries { get; private set; }
        internal ContentPart? LastPresentedPart { get; private set; }
        internal ToolPresentation Presentation { get; } = new([], ToolPresentationDisposition.Fallback);

        public ValueTask<ConversationHistoryReadResult> ReadHistoryAsync(
            SessionSequence afterSequence,
            int maximumEntries,
            CancellationToken cancellationToken = default)
        {
            HistoryCursor = afterSequence;
            MaximumHistoryEntries = maximumEntries;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult<ConversationHistoryReadResult>(
                new ConversationHistoryPage([], afterSequence, complete: true));
        }

        public ValueTask<ConversationSessionOpenResult> OpenAsync(
            SessionId sessionId,
            CancellationToken cancellationToken = default)
        {
            OpenedSessionId = sessionId;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult<ConversationSessionOpenResult>(
                new ConversationSessionOpened(sessionId, new BranchId(Guid.NewGuid())));
        }

        public ValueTask<ConversationSessionListResult> ListAsync(
            SessionId? afterSessionId,
            int maximumResults,
            CancellationToken cancellationToken = default)
        {
            ListCursor = afterSessionId;
            MaximumResults = maximumResults;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult<ConversationSessionListResult>(new ConversationSessionPage([], null));
        }

        public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default)
        {
            LastOperation = "send";
            CancellationToken = cancellationToken;
            return Task.FromResult(new ConversationTurnResult(true, []));
        }

        public Task<ConversationTurnResult> SendAsync(
            string userText,
            IConversationEventObserver observer,
            CancellationToken cancellationToken = default)
        {
            LastOperation = "observed-send";
            Observer = observer;
            CancellationToken = cancellationToken;
            return Task.FromResult(new ConversationTurnResult(true, []));
        }

        public ValueTask<ToolPresentation?> PresentToolAsync(ContentPart part, CancellationToken cancellationToken = default)
        {
            LastPresentedPart = part;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult<ToolPresentation?>(Presentation);
        }
    }

    private sealed class RecordingDisposable: IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
