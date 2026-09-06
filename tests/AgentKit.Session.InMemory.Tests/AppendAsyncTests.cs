// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class AppendAsyncTests
{
    [Fact]
    public async Task AppendAsync_WhenExpectedVersionMatches_AdvancesVersionAndCommitsEntries()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);

        var result = await store.AppendAsync(
            new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("a1"), [entry]),
            TestContext.Current.CancellationToken);

        var appended = result.ShouldBeOfType<SessionAppended>();
        appended.NewVersion.Value.ShouldBe(1);
        _ = appended.CommittedEntries.ShouldHaveSingleItem();
        appended.CommittedEntries[0].Id.ShouldBe(entry.Id);
    }

    [Fact]
    public async Task AppendAsync_WhenAppendedTwiceInSequence_OrdersEntriesByAppendOrder()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var first = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "first");
        var second = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 2, "second");

        _ = await store.AppendAsync(
            new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("a1"), [first]),
            TestContext.Current.CancellationToken);
        var secondResult = (SessionAppended) await store.AppendAsync(
            new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("a2"), [second]),
            TestContext.Current.CancellationToken);

        secondResult.NewVersion.Value.ShouldBe(2);

        var page = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);
        page.Entries.Length.ShouldBe(2);
        ((MessageSessionEntry) page.Entries[0]).Message.Parts.OfType<TextPart>().Single().Text.ShouldBe("first");
        ((MessageSessionEntry) page.Entries[1]).Message.Parts.OfType<TextPart>().Single().Text.ShouldBe("second");
    }

    [Fact]
    public async Task AppendAsync_WhenExpectedVersionIsStale_ReturnsConflictAndDoesNotMutate()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        _ = await store.AppendAsync(
            new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("a1"), [entry]),
            TestContext.Current.CancellationToken);

        // Retry against the now-stale version 0.
        var staleEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "stale");
        var result = await store.AppendAsync(
            new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("a2"), [staleEntry]),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionAppendConflict>();
        conflict.ExpectedVersion.Value.ShouldBe(0);
        conflict.ActualVersion.Value.ShouldBe(1);

        var page = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);
        page.Entries.Length.ShouldBe(1);
    }

    [Fact]
    public async Task AppendAsync_WhenRetriedWithSameIdempotencyKey_DoesNotDuplicateEntries()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var request = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("dupe"), [entry]);

        var first = (SessionAppended) await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var second = (SessionAppended) await store.AppendAsync(request, TestContext.Current.CancellationToken);

        second.NewVersion.ShouldBe(first.NewVersion);
        var page = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);
        page.Entries.Length.ShouldBe(1);
    }

    [Fact]
    public async Task AppendAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var entry = TestFactory.MessageEntry(address, new BranchId(Guid.NewGuid()), 1);

        var result = await store.AppendAsync(
            new SessionAppendRequest(context, new BranchId(Guid.NewGuid()), new SessionVersion(0), new IdempotencyKey("x"), [entry]),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendNotFound>();
    }

    [Fact]
    public async Task AppendAsync_WhenBranchDoesNotExist_ReturnsNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var unknownBranch = new BranchId(Guid.NewGuid());
        var entry = TestFactory.MessageEntry(descriptor.Address, unknownBranch, 1);

        var result = await store.AppendAsync(
            new SessionAppendRequest(context, unknownBranch, new SessionVersion(0), new IdempotencyKey("x"), [entry]),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendNotFound>();
    }

    [Fact]
    public async Task AppendAsync_WhenEntrySequenceDoesNotMatchExpectedVersion_ReturnsFailed()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var wronglySequencedEntry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 5);

        var result = await store.AppendAsync(
            new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("x"), [wronglySequencedEntry]),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public void SessionAppendRequest_WhenEntriesEmpty_ThrowsArgumentException()
    {
        var context = TestFactory.OperationContext(
            new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));

        var exception = Should.Throw<ArgumentException>(() => new SessionAppendRequest(
            context, new BranchId(Guid.NewGuid()), new SessionVersion(0), new IdempotencyKey("x"), []));

        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public async Task AppendAsync_ConcurrentAppendsAtSameExpectedVersion_YieldsOneSuccessAndOneConflict()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        var entryA = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "a");
        var entryB = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "b");

        var requestA = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("racer-a"), [entryA]);
        var requestB = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("racer-b"), [entryB]);

        var results = await Task.WhenAll(
            store.AppendAsync(requestA, TestContext.Current.CancellationToken).AsTask(),
            store.AppendAsync(requestB, TestContext.Current.CancellationToken).AsTask());

        results.OfType<SessionAppended>().Count().ShouldBe(1);
        results.OfType<SessionAppendConflict>().Count().ShouldBe(1);
    }
}
