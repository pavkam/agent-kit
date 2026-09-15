// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies asynchronous ownership settlement and operation rejection.</summary>
public sealed class AsyncOwnedConversationSessionTests
{
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

    private sealed class StubSession: IConversationSession
    {
        public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConversationTurnResult(true, []));
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
