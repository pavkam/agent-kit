// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

public sealed class AgentEngineTests
{
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AgentEngine(null!, ownedProvider: null));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrently_DisposesOwnerOnceAndSharesCompletion()
    {
        var owner = new BlockingAsyncDisposable();
        var engine = new AgentEngine(TimeProvider.System, owner);

        var firstDisposal = engine.DisposeAsync().AsTask();
        var secondDisposal = engine.DisposeAsync().AsTask();

        firstDisposal.ShouldBeSameAs(secondDisposal);
        owner.DisposeCount.ShouldBe(1);
        firstDisposal.IsCompleted.ShouldBeFalse();

        owner.Complete();
        await Task.WhenAll(firstDisposal, secondDisposal);

        owner.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnerThrowsSynchronously_CachesFailureWithoutRetrying()
    {
        var owner = new ThrowingAsyncDisposable();
        var engine = new AgentEngine(TimeProvider.System, owner);

        var firstDisposal = engine.DisposeAsync().AsTask();
        var secondDisposal = engine.DisposeAsync().AsTask();

        firstDisposal.ShouldBeSameAs(secondDisposal);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => firstDisposal);
        owner.DisposeCount.ShouldBe(1);
    }

    private sealed class BlockingAsyncDisposable: IAsyncDisposable
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public void Complete() => _completion.SetResult();

        public ValueTask DisposeAsync()
        {
            _ = Interlocked.Increment(ref _disposeCount);
            return new ValueTask(_completion.Task);
        }
    }

    private sealed class ThrowingAsyncDisposable: IAsyncDisposable
    {
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public ValueTask DisposeAsync()
        {
            _ = Interlocked.Increment(ref _disposeCount);
            throw new InvalidOperationException("Synthetic disposal failure.");
        }
    }
}
