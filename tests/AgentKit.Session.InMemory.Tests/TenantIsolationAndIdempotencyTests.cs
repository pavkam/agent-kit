// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class TenantIsolationAndIdempotencyTests
{
    [Fact]
    public async Task Operations_WhenContextTenantDiffers_DoNotDiscloseOrMutateSession()
    {
        var store = TestFactory.CreateStore();
        var created = await store.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);
        var descriptor = ((SessionCreated) created).Descriptor;
        var foreign = TestFactory.OperationContext(descriptor.Address, tenant: "tenant-2");
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);

        _ = (await store.LoadAsync(foreign, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionNotFound>();
        _ = (await store.AppendAsync(new SessionAppendRequest(foreign, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("foreign-append"), [entry]), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendNotFound>();
        _ = (await store.ReadAsync(new SessionReadRequest(foreign, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionReadNotFound>();
        _ = (await store.CreateBranchAsync(new SessionBranchRequest(foreign, descriptor.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("foreign-branch")), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionBranchParentNotFound>();
        _ = (await store.DeleteAsync(new SessionDeleteRequest(foreign, new IdempotencyKey("foreign-delete")), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleted>();

        var page = (SessionPage) await store.ReadAsync(new SessionReadRequest(TestFactory.OperationContext(descriptor.Address), descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        page.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenIdenticalReconstructedRequestReplays_ReturnsOriginalReceipt()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("create-replay");
        var first = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        var replay = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);

        replay.ShouldBe(first);
        replay.Existing.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedEvidence_RejectsBeforeVersionConflict()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var key = new IdempotencyKey("append-replay");
        var first = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), key, [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "one")]);
        _ = await store.AppendAsync(first, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(TestFactory.OperationContext(descriptor.Address, principal: "user-2"), descriptor.ActiveBranchId, new SessionVersion(99), key, [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 100, "two")]);

        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedPayload_Rejects()
    {
        var (store, descriptor, request) = await CreateAppendRequestAsync();
        _ = await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(request.Context, request.BranchId, request.ExpectedVersion, request.IdempotencyKey, [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "changed")]);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedPrincipal_Rejects()
    {
        var (store, descriptor, request) = await CreateAppendRequestAsync();
        _ = await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(TestFactory.OperationContext(descriptor.Address, principal: "other"), request.BranchId, request.ExpectedVersion, request.IdempotencyKey, request.Entries);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenIdempotencyKeyHasChangedCorrelation_Rejects()
    {
        var (store, descriptor, request) = await CreateAppendRequestAsync();
        _ = await store.AppendAsync(request, TestContext.Current.CancellationToken);
        var changed = new SessionAppendRequest(TestFactory.OperationContext(descriptor.Address), request.BranchId, request.ExpectedVersion, request.IdempotencyKey, request.Entries);
        _ = (await store.AppendAsync(changed, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenEquivalentReconstructedPayloadReplays_ReturnsOriginalReceiptBeforeVersionConflict()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test.payload", new ExtensionValue([.. /*lang=json,strict*/ "{\"nested\":[1,2]}"u8])));
        var message = new UserMessage(new MessageId(Guid.NewGuid()), descriptor.Address.AgentId, descriptor.Address.SessionId, null, descriptor.ActiveBranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("payload", TextSemantics.Plain, extensions)], extensions);
        var entry = new MessageSessionEntry(new SessionEntryId(Guid.NewGuid()), descriptor.Address, TestFactory.Correlation(), descriptor.ActiveBranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), message);
        var key = new IdempotencyKey("reconstructed");
        var first = (SessionAppended) await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), key, [entry]), TestContext.Current.CancellationToken);
        var rebuiltExtensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test.payload", new ExtensionValue([.. /*lang=json,strict*/ "{\"nested\":[1,2]}"u8])));
        var rebuiltMessage = new UserMessage(message.Id, descriptor.Address.AgentId, descriptor.Address.SessionId, null, descriptor.ActiveBranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("payload", TextSemantics.Plain, rebuiltExtensions)], rebuiltExtensions);
        var rebuiltEntry = new MessageSessionEntry(entry.Id, descriptor.Address, entry.Correlation, descriptor.ActiveBranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), rebuiltMessage);

        var replay = (SessionAppended) await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), key, [rebuiltEntry]), TestContext.Current.CancellationToken);
        replay.ShouldBe(first);
    }

    [Fact]
    public async Task CreateAsync_WhenSameKeyIsUsedByDifferentTenants_CreatesIndependentSessions()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("tenant-scoped");
        var first = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, key), TestContext.Current.CancellationToken);
        var second = (SessionCreated) await store.CreateAsync(
            TestFactory.CreateRequest(agentId, key, identity: TestFactory.Identity("tenant-2")),
            TestContext.Current.CancellationToken);

        second.Descriptor.Address.ShouldNotBe(first.Descriptor.Address);
    }

    [Fact]
    public async Task CreateAsync_WhenOriginalCreateWasDeleted_DoesNotResurrectAndNewKeyCreatesFreshSession()
    {
        var store = TestFactory.CreateStore();
        var agentId = new AgentId(Guid.NewGuid());
        var key = new IdempotencyKey("original");
        var request = TestFactory.CreateRequest(agentId, key);
        var original = (SessionCreated) await store.CreateAsync(request, TestContext.Current.CancellationToken);
        _ = await store.DeleteAsync(new SessionDeleteRequest(TestFactory.OperationContext(original.Descriptor.Address), new IdempotencyKey("delete")), TestContext.Current.CancellationToken);

        _ = (await store.CreateAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreateFailed>();
        var recreated = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(agentId, new IdempotencyKey("fresh")), TestContext.Current.CancellationToken);
        recreated.Descriptor.Address.ShouldNotBe(original.Descriptor.Address);
    }

    [Fact]
    public async Task DeleteAsync_WhenEquivalentRequestReplays_ReturnsOriginalReceipt()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionDeleteRequest(context, new IdempotencyKey("delete-replay"));
        var first = (SessionDeleted) await store.DeleteAsync(request, TestContext.Current.CancellationToken);

        var replay = (SessionDeleted) await store.DeleteAsync(request, TestContext.Current.CancellationToken);
        replay.ShouldBe(first);
    }

    [Fact]
    public async Task DeleteAsync_WhenIdempotencyKeyHasChangedPrincipal_Rejects()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var key = new IdempotencyKey("delete-principal");
        _ = await store.DeleteAsync(new SessionDeleteRequest(TestFactory.OperationContext(descriptor.Address), key), TestContext.Current.CancellationToken);

        _ = (await store.DeleteAsync(new SessionDeleteRequest(TestFactory.OperationContext(descriptor.Address, principal: "other"), key), TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleteFailed>();
    }

    private static async Task<(InMemorySessionStore Store, SessionDescriptor Descriptor, SessionAppendRequest Request)> CreateAppendRequestAsync()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("independent"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "original")]);
        return (store, descriptor, request);
    }
}
