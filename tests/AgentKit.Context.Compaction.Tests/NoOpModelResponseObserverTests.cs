// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

public sealed class NoOpModelResponseObserverTests
{
    [Fact]
    public void Instance_WhenAccessedTwice_ReturnsTheSameSharedInstance()
    {
        var first = NoOpModelResponseObserver.Instance;

        var second = NoOpModelResponseObserver.Instance;

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public async Task OnEventAsync_WhenGivenAStreamedEvent_CompletesWithoutObservableEffect()
    {
        var responseEvent = new ModelResponseStarted(new ModelRequestId(Guid.NewGuid()), 1);

        var task = NoOpModelResponseObserver.Instance.OnEventAsync(responseEvent, TestContext.Current.CancellationToken);

        task.IsCompletedSuccessfully.ShouldBeTrue();
        await task;
    }

    [Fact]
    public async Task OnEventAsync_WhenTokenIsAlreadyCancelled_StillCompletesWithoutThrowing()
    {
        var responseEvent = new ModelResponseStarted(new ModelRequestId(Guid.NewGuid()), 1);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var task = NoOpModelResponseObserver.Instance.OnEventAsync(responseEvent, cancellation.Token);

        task.IsCompletedSuccessfully.ShouldBeTrue();
        await task;
    }
}
