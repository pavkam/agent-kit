// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class BranchAsyncTests
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
    public async Task CreateBranchAsync_WhenForkingMidway_CreatesBranchWithOnlyEntriesUpToForkPoint()
    {
        var (store, descriptor, context) = await SeedAsync(4);

        var result = await store.CreateBranchAsync(
            new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("b1")),
            TestContext.Current.CancellationToken);

        var branched = result.ShouldBeOfType<SessionBranched>();
        var branchContext = TestFactory.OperationContext(descriptor.Address);
        var page = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(branchContext, branched.NewBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        page.Entries.Length.ShouldBe(2);
    }

    [Fact]
    public async Task CreateBranchAsync_LeavesOriginalBranchUnchanged()
    {
        var (store, descriptor, context) = await SeedAsync(4);

        var branched = (SessionBranched) await store.CreateBranchAsync(
            new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), new IdempotencyKey("b1")),
            TestContext.Current.CancellationToken);

        // Append to the new branch only.
        var newEntry = TestFactory.MessageEntry(descriptor.Address, branched.NewBranchId, 3, "new-branch-only");
        _ = await store.AppendAsync(
            new SessionAppendRequest(context, branched.NewBranchId, new SessionVersion(2), new IdempotencyKey("nb1"), [newEntry]),
            TestContext.Current.CancellationToken);

        var originalPage = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        originalPage.Entries.Length.ShouldBe(4);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenRetriedWithSameIdempotencyKey_ReturnsSameBranch()
    {
        var (store, descriptor, context) = await SeedAsync(2);
        var request = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(1), new IdempotencyKey("dupe"));

        var first = (SessionBranched) await store.CreateBranchAsync(request, TestContext.Current.CancellationToken);
        var second = (SessionBranched) await store.CreateBranchAsync(request, TestContext.Current.CancellationToken);

        second.NewBranchId.ShouldBe(first.NewBranchId);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenReplayCarriesChangedForkPoint_RejectsAndPreservesOriginalReceipt()
    {
        var (store, descriptor, context) = await SeedAsync(2);
        var key = new IdempotencyKey("branch-evidence");
        var first = (SessionBranched) await store.CreateBranchAsync(
            new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(1), key),
            TestContext.Current.CancellationToken);

        var changed = await store.CreateBranchAsync(
            new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(2), key),
            TestContext.Current.CancellationToken);

        _ = changed.ShouldBeOfType<SessionBranchFailed>();
        first.ForkedAtSequence.ShouldBe(new SessionSequence(1));
    }

    [Fact]
    public async Task CreateBranchAsync_WhenForkPointExceedsParentLength_ReturnsParentNotFound()
    {
        var (store, descriptor, context) = await SeedAsync(2);

        var result = await store.CreateBranchAsync(
            new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(10), new IdempotencyKey("b1")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionBranchParentNotFound>();
    }

    [Fact]
    public async Task CreateBranchAsync_WhenParentBranchDoesNotExist_ReturnsParentNotFound()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);

        var result = await store.CreateBranchAsync(
            new SessionBranchRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), new IdempotencyKey("b1")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionBranchParentNotFound>();
    }

    [Fact]
    public async Task CreateBranchAsync_WhenForkingAtZero_CreatesEmptyBranch()
    {
        var (store, descriptor, context) = await SeedAsync(3);

        var branched = (SessionBranched) await store.CreateBranchAsync(
            new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("b1")),
            TestContext.Current.CancellationToken);

        var page = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(context, branched.NewBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        page.Entries.ShouldBeEmpty();
    }
}
