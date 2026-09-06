// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class LoadAsyncTests
{
    [Fact]
    public async Task LoadAsync_WhenSessionExists_ReturnsCurrentDescriptor()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);

        var result = await store.LoadAsync(
            TestFactory.OperationContext(descriptor.Address), TestContext.Current.CancellationToken);

        var loaded = result.ShouldBeOfType<SessionLoaded>();
        loaded.Descriptor.Address.ShouldBe(descriptor.Address);
    }

    [Fact]
    public async Task LoadAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));

        var result = await store.LoadAsync(
            TestFactory.OperationContext(address), TestContext.Current.CancellationToken);

        var notFound = result.ShouldBeOfType<SessionNotFound>();
        notFound.Address.ShouldBe(address);
    }

    [Fact]
    public async Task LoadAsync_AfterDelete_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        _ = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("del-1")), TestContext.Current.CancellationToken);
        var result = await store.LoadAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionNotFound>();
    }
}
