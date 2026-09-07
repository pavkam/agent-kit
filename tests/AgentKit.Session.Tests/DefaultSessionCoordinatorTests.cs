// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

public sealed class DefaultSessionCoordinatorTests
{
    private static DefaultSessionCoordinator CreateCoordinator(
        ISessionStore store,
        IEnumerable<ISessionEventSink>? sinks = null,
        TimeProvider? timeProvider = null,
        AgentSessionOptions? options = null) =>
        new(store, sinks ?? [], timeProvider ?? new FakeTimeProvider(), Options.Create(options ?? new AgentSessionOptions()));

    [Fact]
    public void Constructor_WhenStoreIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultSessionCoordinator(
            null!, [], new FakeTimeProvider(), Options.Create(new AgentSessionOptions())));

        exception.ParamName.ShouldBe("store");
    }

    [Fact]
    public async Task CreateAsync_WhenStoreReturnsCreated_PublishesSessionCreatedEvent()
    {
        var store = new FakeSessionStore();
        var descriptor = TestFactory.Descriptor();
        store.OnCreate = _ => new SessionCreated(descriptor, existing: false);
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
        var published = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionCreatedEvent>();
        published.Descriptor.ShouldBe(descriptor);
    }

    [Fact]
    public async Task CreateAsync_WhenStoreReturnsFailed_DoesNotPublishEvent()
    {
        var store = new FakeSessionStore { OnCreate = _ => new SessionCreateFailed("boom") };
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);

        _ = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestContext.Current.CancellationToken);

        sink.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenEntryCountExceedsMaximum_ReturnsFailedWithoutCallingStore()
    {
        var store = new FakeSessionStore();
        var coordinator = CreateCoordinator(store, options: new AgentSessionOptions { MaximumAppendEntries = 1 });
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var branchId = new BranchId(Guid.NewGuid());
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, branchId, 1),
            TestFactory.MessageEntry(address, branchId, 2));

        var result = await coordinator.AppendAsync(
            new SessionAppendRequest(context, branchId, new SessionVersion(0), new IdempotencyKey("k"), entries),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendFailed>();
        store.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenStoreReturnsAppended_PublishesSessionAppendedEvent()
    {
        var store = new FakeSessionStore
        {
            OnAppend = _ => new SessionAppended(new SessionVersion(1), [TestFactory.MessageEntry(
                new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new BranchId(Guid.NewGuid()), 1)]),
        };
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var branchId = new BranchId(Guid.NewGuid());

        _ = await coordinator.AppendAsync(
            new SessionAppendRequest(context, branchId, new SessionVersion(0), new IdempotencyKey("k"), [TestFactory.MessageEntry(address, branchId, 1)]),
            TestContext.Current.CancellationToken);

        var published = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionAppendedEvent>();
        published.NewVersion.Value.ShouldBe(1);
    }

    [Fact]
    public async Task AppendAsync_WhenStoreReturnsConflict_DoesNotPublishEvent()
    {
        var store = new FakeSessionStore { OnAppend = _ => new SessionAppendConflict(new SessionVersion(0), new SessionVersion(1)) };
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var branchId = new BranchId(Guid.NewGuid());

        var result = await coordinator.AppendAsync(
            new SessionAppendRequest(context, branchId, new SessionVersion(0), new IdempotencyKey("k"), [TestFactory.MessageEntry(address, branchId, 1)]),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendConflict>();
        sink.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WhenPageSizeExceedsMaximum_ReturnsFailedWithoutCallingStore()
    {
        var readCalled = false;
        var store = new FakeSessionStore { OnRead = _ => { readCalled = true; return new SessionPage([], new SessionSequence(0), false); } };
        var coordinator = CreateCoordinator(store, options: new AgentSessionOptions { MaximumPageSize = 10 });
        var context = TestFactory.OperationContext(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));

        var result = await coordinator.ReadAsync(
            new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 20),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadFailed>();
        readCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenWithinLimit_DelegatesToStore()
    {
        var store = new FakeSessionStore { OnRead = _ => new SessionPage([], new SessionSequence(0), false) };
        var coordinator = CreateCoordinator(store);
        var context = TestFactory.OperationContext(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));

        var result = await coordinator.ReadAsync(
            new SessionReadRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(0), 5),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionPage>();
    }

    [Fact]
    public async Task LoadAsync_DelegatesDirectlyToStoreWithoutPublishing()
    {
        var descriptor = TestFactory.Descriptor();
        var store = new FakeSessionStore { OnLoad = _ => new SessionLoaded(descriptor) };
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);

        var result = await coordinator.LoadAsync(
            TestFactory.OperationContext(descriptor.Address), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLoaded>();
        sink.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenObserved_EmitsCorrelatedSuccessfulActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var descriptor = TestFactory.Descriptor();
        var store = new FakeSessionStore { OnLoad = _ => new SessionLoaded(descriptor) };
        var coordinator = CreateCoordinator(store);

        _ = await coordinator.LoadAsync(
            TestFactory.OperationContext(descriptor.Address),
            TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.SessionLoad);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(descriptor.Address.SessionId.ToString());
    }

    [Fact]
    public async Task BranchAsync_WhenStoreReturnsBranched_PublishesSessionBranchedEvent()
    {
        var newBranchId = new BranchId(Guid.NewGuid());
        var store = new FakeSessionStore { OnBranch = _ => new SessionBranched(newBranchId, new SessionSequence(3)) };
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);
        var context = TestFactory.OperationContext(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())));

        _ = await coordinator.BranchAsync(
            new SessionBranchRequest(context, new BranchId(Guid.NewGuid()), new SessionSequence(3), new IdempotencyKey("b")),
            TestContext.Current.CancellationToken);

        var published = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionBranchedEvent>();
        published.NewBranchId.ShouldBe(newBranchId);
    }

    [Fact]
    public async Task DeleteAsync_WhenStoreReturnsDeleted_PublishesSessionDeletedEvent()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var store = new FakeSessionStore { OnDelete = _ => new SessionDeleted(address) };
        var sink = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [sink]);
        var context = TestFactory.OperationContext(address);

        _ = await coordinator.DeleteAsync(
            new SessionDeleteRequest(context, new IdempotencyKey("d")), TestContext.Current.CancellationToken);

        _ = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionDeletedEvent>();
    }

    [Fact]
    public async Task AppendAsync_WhenMultipleSinksRegistered_AllReceiveEventInOrder()
    {
        var store = new FakeSessionStore
        {
            OnAppend = _ => new SessionAppended(new SessionVersion(1), []),
        };
        var first = new FakeSessionEventSink();
        var second = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [first, second]);
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var context = TestFactory.OperationContext(address);
        var branchId = new BranchId(Guid.NewGuid());

        _ = await coordinator.AppendAsync(
            new SessionAppendRequest(context, branchId, new SessionVersion(0), new IdempotencyKey("k"), [TestFactory.MessageEntry(address, branchId, 1)]),
            TestContext.Current.CancellationToken);

        _ = first.Received.ShouldHaveSingleItem();
        _ = second.Received.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AppendAsync_WhenSinkFailsAfterCommit_ReturnsCommittedResultAndContinuesDelivery()
    {
        var store = new FakeSessionStore
        {
            OnAppend = _ => new SessionAppended(new SessionVersion(1), []),
        };
        var failing = new FakeSessionEventSink
        {
            OnPublish = static (_, _) => ValueTask.FromException(new InvalidOperationException("observer failed")),
        };
        var succeeding = new FakeSessionEventSink();
        var coordinator = CreateCoordinator(store, [failing, succeeding]);
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var branchId = new BranchId(Guid.NewGuid());
        var request = new SessionAppendRequest(
            TestFactory.OperationContext(address),
            branchId,
            new SessionVersion(0),
            new IdempotencyKey("append"),
            [TestFactory.MessageEntry(address, branchId, 1)]);

        var result = await coordinator.AppendAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppended>();
        store.ReceivedAppends.ShouldHaveSingleItem().ShouldBe(request);
        _ = failing.Received.ShouldHaveSingleItem();
        _ = succeeding.Received.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AppendAsync_WhenCallerCancelsDuringPostCommitDelivery_ReturnsCommittedResultAndPreservesState()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemorySessionStore();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var store = provider.GetRequiredService<ISessionStore>();
        var created = (SessionCreated) await store.CreateAsync(TestFactory.CreateRequest(), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var sink = new FakeSessionEventSink
        {
            OnPublish = (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            },
        };
        var coordinator = CreateCoordinator(store, [sink]);
        var descriptor = created.Descriptor;
        var context = TestFactory.OperationContext(descriptor.Address);
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var request = new SessionAppendRequest(
            context,
            descriptor.ActiveBranchId,
            descriptor.Version,
            new IdempotencyKey("append"),
            [entry]);

        var result = await coordinator.AppendAsync(request, cancellation.Token);

        _ = result.ShouldBeOfType<SessionAppended>();
        var page = await store.ReadAsync(
            new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            CancellationToken.None);
        page.ShouldBeOfType<SessionPage>().Entries.ShouldHaveSingleItem().ShouldBe(entry);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
