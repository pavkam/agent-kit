// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class ReadAsyncTests
{
    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionOperationContext Context)> SeedAsync(int entryCount)
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        for (var i = 0; i < entryCount; i++)
        {
            var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, i + 1, $"entry-{i}");
            _ = await store.AppendAsync(
                new SessionAppendRequest(
                    context, descriptor.ActiveBranchId, new SessionVersion(i), new IdempotencyKey($"seed-{i}"), [entry]),
                TestContext.Current.CancellationToken);
        }

        return (store, descriptor, context);
    }

    [Fact]
    public async Task ReadAsync_FromBeginning_ReturnsAllEntriesWhenPageSizeIsLargeEnough()
    {
        var (store, descriptor, context) = await SeedAsync(3);

        var result = await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<SessionPage>();
        page.Entries.Length.ShouldBe(3);
        page.HasMore.ShouldBeFalse();
        page.ThroughSequence.Value.ShouldBe(3);
    }

    [Fact]
    public async Task ReadAsync_WhenPageSizeSmallerThanTotal_ReturnsPartialPageWithHasMoreTrue()
    {
        var (store, descriptor, context) = await SeedAsync(5);

        var result = await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2),
            TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<SessionPage>();
        page.Entries.Length.ShouldBe(2);
        page.HasMore.ShouldBeTrue();
        page.ThroughSequence.Value.ShouldBe(2);
    }

    [Fact]
    public async Task ReadAsync_WhenContinuingFromPreviousPage_ReturnsRemainingEntries()
    {
        var (store, descriptor, context) = await SeedAsync(5);

        var firstPage = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 2),
            TestContext.Current.CancellationToken);
        var secondPage = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 2),
            TestContext.Current.CancellationToken);

        secondPage.Entries.Length.ShouldBe(2);
        secondPage.HasMore.ShouldBeTrue();
        secondPage.ThroughSequence.Value.ShouldBe(4);
    }

    [Fact]
    public async Task ReadAsync_WhenBranchIsEmpty_ReturnsEmptyPageWithHasMoreFalse()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        var result = await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<SessionPage>();
        page.Entries.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenBranchDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        var result = await store.ReadAsync(
            new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadNotFound>();
    }

    [Fact]
    public void SessionReadRequest_WhenPageSizeIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var context = TestFactory.OperationContext(
            new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 0));
    }
}
