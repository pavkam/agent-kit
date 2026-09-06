// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class CreateAsyncTests
{
    [Fact]
    public async Task CreateAsync_WhenCalledOnce_ReturnsNewActiveSession()
    {
        var store = TestFactory.CreateStore();

        var result = await store.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<SessionCreated>();
        created.Existing.ShouldBeFalse();
        created.Descriptor.State.ShouldBe(SessionLifecycleState.Active);
        created.Descriptor.Version.Value.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_WhenCalledTwiceWithSameIdempotencyKey_ReturnsSameSession()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("retry-key");

        var first = (SessionCreated) await store.CreateAsync(
            TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        var second = (SessionCreated) await store.CreateAsync(
            TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);

        second.Existing.ShouldBeTrue();
        second.Descriptor.Address.ShouldBe(first.Descriptor.Address);
    }

    [Fact]
    public async Task CreateAsync_WhenCalledWithDifferentIdempotencyKeys_ReturnsDifferentSessions()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());

        var first = (SessionCreated) await store.CreateAsync(
            TestFactory.CreateRequest(agentId), TestContext.Current.CancellationToken);
        var second = (SessionCreated) await store.CreateAsync(
            TestFactory.CreateRequest(agentId), TestContext.Current.CancellationToken);

        second.Descriptor.Address.ShouldNotBe(first.Descriptor.Address);
    }

    [Fact]
    public async Task CreateAsync_WhenCalled_RecordsConfiguredStoreKey()
    {
        var store = TestFactory.CreateStore();

        var created = (SessionCreated) await store.CreateAsync(
            TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        created.Descriptor.StoreKey.ShouldBe(store.Descriptor.Key);
    }

    [Fact]
    public async Task CreateAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var store = TestFactory.CreateStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await store.CreateAsync(TestFactory.CreateRequest(), cts.Token));
    }
}
