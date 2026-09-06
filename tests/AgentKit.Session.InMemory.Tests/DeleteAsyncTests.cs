// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class DeleteAsyncTests
{
    [Fact]
    public async Task DeleteAsync_WhenSessionExists_RemovesIt()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        var result = await store.DeleteAsync(
            new SessionDeleteRequest(context, new IdempotencyKey("d1")), TestContext.Current.CancellationToken);

        var deleted = result.ShouldBeOfType<SessionDeleted>();
        deleted.Address.ShouldBe(descriptor.Address);

        var loadResult = await store.LoadAsync(context, TestContext.Current.CancellationToken);
        _ = loadResult.ShouldBeOfType<SessionNotFound>();
    }

    [Fact]
    public async Task DeleteAsync_WhenCalledTwice_IsIdempotent()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        _ = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("d1")), TestContext.Current.CancellationToken);
        var second = await store.DeleteAsync(new SessionDeleteRequest(context, new IdempotencyKey("d2")), TestContext.Current.CancellationToken);

        _ = second.ShouldBeOfType<SessionDeleted>();
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionNeverExisted_StillReportsDeleted()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);

        var result = await store.DeleteAsync(
            new SessionDeleteRequest(context, new IdempotencyKey("d1")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDeleted>();
    }
}
