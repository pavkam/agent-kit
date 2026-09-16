// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

public sealed class DefaultSessionCoordinatorTests
{
    [Fact]
    public void Constructor_WhenDirectoryIsNull_ThrowsExactArgumentNullException()
    {
        var harness = new Harness();

        var exception = Should.Throw<ArgumentNullException>(() => harness.CreateCoordinator(directory: null!));

        exception.ParamName.ShouldBe("directory");
    }

    [Fact]
    public async Task CreateAsync_WhenRouteIsNew_LocatesBeforeAllocationAndSeparatelyAuthorizesEveryEffect()
    {
        var order = new List<string>();
        var harness = new Harness(order);
        harness.Directory.OnLocateCreate = _ =>
        {
            harness.SessionIds.Count.ShouldBe(0);
            order.Add("locate");
            return new SessionCreationLocationNotFound();
        };
        harness.Directory.OnRecordCreate = wrapper =>
        {
            order.Add("record");
            return new SessionLocationRecorded(wrapper.Request.Location, existing: false);
        };
        harness.Store.OnCreate = request =>
        {
            order.Add("store");
            return new SessionCreated(TestFactory.Descriptor(request.Address), existing: false);
        };
        var coordinator = harness.CreateCoordinator();
        var request = TestFactory.CreateRequest(idempotencyKey: new IdempotencyKey("create"));

        var result = await coordinator.CreateAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<SessionCreated>();
        harness.SessionIds.Count.ShouldBe(1);
        order.ShouldBe(["locate", "record", "capture", "store"]);
        harness.Authority.Requests.Select(static item => (item.Audience, item.Kind, item.Effect)).ShouldBe([
            (harness.Directory.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            (harness.Directory.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            (harness.Store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Create),
        ]);
        harness.Directory.LocateCreateRequests.ShouldHaveSingleItem().Grant
            .ShouldNotBeSameAs(harness.Directory.RecordCreateRequests.ShouldHaveSingleItem().Grant);
        var storeRequest = harness.Store.ReceivedCreates.ShouldHaveSingleItem();
        storeRequest.Request.Address.ShouldBe(created.Descriptor.Address);
        storeRequest.Request.Context.Authorization.Scope.SessionId.ShouldBe(created.Descriptor.Address.SessionId);
        storeRequest.Grant.ShouldBeSameAs(harness.Authority.Grants[^1]);
    }

    [Fact]
    public async Task CreateAsync_WhenRouteAlreadyExists_DoesNotAllocateOrProbeDefaultStore()
    {
        var winner = Location("other-store");
        var harness = new Harness(stores: [new FakeSessionStore(), new FakeSessionStoreWithKey("other-store")]);
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationLocated(winner);
        var winningStore = (FakeSessionStoreWithKey) harness.Stores[1];
        winningStore.OnCreate = request => new SessionCreated(TestFactory.Descriptor(request.Address) with
        {
            StoreKey = new SessionStoreKey("other-store"),
        }, existing: true);
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(winner.Address.AgentId),
            TestFactory.Profile("fake"), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
        harness.SessionIds.Count.ShouldBe(0);
        harness.Directory.RecordCreateRequests.ShouldBeEmpty();
        harness.Store.ReceivedCreates.ShouldBeEmpty();
        winningStore.ReceivedCreates.ShouldHaveSingleItem().Request.Address.ShouldBe(winner.Address);
    }

    [Fact]
    public async Task LoadAsync_WhenRouteIsLocated_ForwardsDistinctDirectoryAndStoreGrantsWithoutConsuming()
    {
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnLoad = _ => new SessionLoaded(descriptor);
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.LoadAsync(context, TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLoaded>();
        harness.Authority.Requests.Count.ShouldBe(2);
        harness.Directory.LocateRequests.ShouldHaveSingleItem().Grant.ShouldBeSameAs(harness.Authority.Grants[0]);
        harness.Store.ReceivedLoads.ShouldHaveSingleItem().Grant.ShouldBeSameAs(harness.Authority.Grants[1]);
        harness.Authority.Grants[0].ShouldNotBeSameAs(harness.Authority.Grants[1]);
    }

    [Fact]
    public async Task LoadAsync_WhenDirectoryAuthorizationIsDenied_DoesNotTouchDirectoryOrStore()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("Session route lookup was not authorized.");
        harness.Directory.LocateRequests.ShouldBeEmpty();
        harness.Store.ReceivedLoads.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenProfileLimitIsExceeded_ValidatesBeforeAuthorizationOrRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [
                TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1),
                TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 2),
            ]);

        var result = await coordinator.AppendAsync(request,
            TestFactory.Profile(maximumAppendEntries: 1), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppendFailed>();
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenProfileLimitIsExceededAndAlreadyCancelled_PreservesCancellationBeforeEffects()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [
                TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1),
                TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 2),
            ]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await coordinator.AppendAsync(
                request, TestFactory.Profile(maximumAppendEntries: 1), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenOptionsMutateAfterConstruction_UsesCapturedSecurityRequestLifetime()
    {
        var options = new AgentSessionOptions { SecurityRequestLifetime = TimeSpan.FromSeconds(10) };
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnLoad = _ => new SessionLoaded(descriptor);
        var coordinator = harness.CreateCoordinator(options);
        options.SecurityRequestLifetime = TimeSpan.FromSeconds(20);

        _ = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        harness.Authority.Requests.ShouldAllBe(
            request => request.Deadline == DateTimeOffset.UnixEpoch.AddSeconds(10));
    }

    [Fact]
    public async Task AppendAsync_WhenPostCommitEventSinkThrows_PreservesCommittedSuccess()
    {
        var harness = new Harness(eventSinks: [new ThrowingSessionEventSink()]);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnAppend = request => new SessionAppended(
            new SessionVersion(request.ExpectedVersion.Value + request.Entries.Length), request.Entries);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1)]);

        var result = await harness.CreateCoordinator().AppendAsync(
            request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppended>();
        harness.Store.ReceivedAppends.ShouldHaveSingleItem().ShouldBeSameAs(request);
    }

    [Fact]
    public async Task AppendAsync_WhenCancelledAfterStoreCommit_ReturnsAppendedAndPublishesEvent()
    {
        // A store result is a committed effect; caller cancellation observed afterward must not hide it or skip publication.
        var sink = new FakeSessionEventSink();
        var harness = new Harness(eventSinks: [sink]);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        using var cancellation = new CancellationTokenSource();
        harness.Store.OnAppend = request =>
        {
            cancellation.Cancel();
            return new SessionAppended(
                new SessionVersion(request.ExpectedVersion.Value + request.Entries.Length), request.Entries);
        };
        CancellationToken? publishedToken = null;
        sink.OnPublish = (_, token) =>
        {
            publishedToken = token;
            return ValueTask.CompletedTask;
        };
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1)]);

        var result = await harness.CreateCoordinator().AppendAsync(request, TestFactory.Profile(), cancellation.Token);

        result.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        var published = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionAppendedEvent>();
        published.NewVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        publishedToken.ShouldNotBeNull().IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendAsync_WhenStoreThrowsCancellation_PreservesOriginalException()
    {
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        using var cancellation = new CancellationTokenSource();
        var original = new OperationCanceledException("store cancelled", cancellation.Token);
        harness.Store.OnAppend = _ =>
        {
            cancellation.Cancel();
            throw original;
        };
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1)]);

        // Awaited directly: Shouldly's task-based helper reports a canceled task as a fresh TaskCanceledException.
        OperationCanceledException? observed = null;
        try
        {
            _ = await harness.CreateCoordinator().AppendAsync(request, TestFactory.Profile(), cancellation.Token);
        }
        catch (OperationCanceledException exception)
        {
            observed = exception;
        }

        observed.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task AppendAsync_WhenPostCommitEventClockThrows_PreservesCommittedSuccess()
    {
        var timeProvider = new ArmableThrowingTimeProvider();
        var harness = new Harness(timeProvider: timeProvider);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnAppend = request =>
        {
            timeProvider.Arm();
            return new SessionAppended(
                new SessionVersion(request.ExpectedVersion.Value + request.Entries.Length),
                request.Entries);
        };
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1)]);

        var result = await harness.CreateCoordinator().AppendAsync(
            request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionAppended>();
        timeProvider.ArmedReadAttempted.ShouldBeTrue();
        harness.Store.ReceivedAppends.ShouldHaveSingleItem().ShouldBeSameAs(request);
    }

    [Fact]
    public async Task LoadAsync_WhenAlreadyCancelled_PreservesCancellationBeforeEffects()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await coordinator.LoadAsync(TestFactory.OperationContext(TestFactory.Descriptor().Address),
                TestFactory.Profile(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenLoggerThrows_PreservesSuccessfulResult()
    {
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnLoad = _ => new SessionLoaded(descriptor);
        var coordinator = harness.CreateCoordinator(logger: new ThrowingLogger());

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLoaded>();
    }

    [Fact]
    public async Task LookupInputAsync_WhenCapabilitySelectedDifferentCoordinator_RejectsBeforeAuthorityOrRouting()
    {
        using var parent = new Activity("input-lookup-parent").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref options) =>
                options.Name == AgentKitActivityNames.SessionInputLookup
                    ? ActivitySamplingResult.AllData
                    : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionInputLookup
                    && activity.TraceId == parent.TraceId)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var profile = TestFactory.Profile();
        var alternate = new FakeRunStateSessionCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(profile, alternate, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var identity = TestFactory.Identity();
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var context = new SessionOperationContext(address.AgentId, address.SessionId,
            new ExecutionLaneId(Guid.NewGuid()), correlation, identity,
            TestFactory.Authorization(address.AgentId, address.SessionId, correlation, identity));
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp,
            [new TextPart("content", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

        var result = await coordinator.LookupInputAsync(
            new SessionInputLookupRequest(context, input, new InputFingerprint("sha256:input")),
            capability, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionInputLookupRejected>();
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Directory.LocateRequests.ShouldBeEmpty();
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(identity.TenantId.ToString());
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(address.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(address.SessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.ExecutionLaneId).ShouldBe(context.ExecutionLaneId?.ToString());
        activity.GetTagItem(AgentKitTagNames.OperationId).ShouldBe(correlation.OperationId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBeNull();
        activity.GetTagItem(AgentKitTagNames.TurnId).ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_WhenAuthorized_ForwardsExactRequestAndReturnsDirectoryPage()
    {
        var harness = new Harness();
        var page = new SessionDirectoryPage([], null);
        harness.Directory.OnList = _ => page;
        var coordinator = harness.CreateCoordinator();
        var request = TestFactory.DirectoryListRequest();

        var result = await coordinator.ListAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(page);
        harness.Directory.ListRequests.ShouldHaveSingleItem().Request.ShouldBeSameAs(request);
    }

    [Fact]
    public async Task ListAsync_WhenAuthorizationIsDenied_ReturnsUnavailableWithoutCallingDirectory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.ListAsync(TestFactory.DirectoryListRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionDirectoryListUnavailable>().SafeMessage
            .ShouldBe("Session discovery was not authorized.");
        harness.Directory.ListRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WhenPageSizeExceedsMaximum_ReturnsFailedWithoutRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 500);

        var result = await coordinator.ReadAsync(request, TestFactory.Profile(maximumPageSize: 10),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionReadFailed>().SafeMessage
            .ShouldBe("Requested page size 500 exceeds the configured maximum of 10.");
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task BranchAsync_WhenBranchSucceeds_PublishesBranchedEvent()
    {
        var sink = new FakeSessionEventSink();
        var harness = new Harness(eventSinks: [sink]);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var newBranchId = new BranchId(Guid.NewGuid());
        harness.Store.OnBranch = _ => new SessionBranched(newBranchId, new SessionSequence(3));
        var request = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(3),
            new IdempotencyKey("branch"));

        var result = await harness.CreateCoordinator()
            .BranchAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        var branched = result.ShouldBeOfType<SessionBranched>();
        branched.NewBranchId.ShouldBe(newBranchId);
        var published = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionBranchedEvent>();
        published.NewBranchId.ShouldBe(newBranchId);
        published.ParentBranchId.ShouldBe(descriptor.ActiveBranchId);
    }

    [Fact]
    public async Task BranchAsync_WhenBranchFails_DoesNotPublishEvent()
    {
        var sink = new FakeSessionEventSink();
        var harness = new Harness(eventSinks: [sink]);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnBranch = _ => new SessionBranchFailed("no parent");
        var request = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(3),
            new IdempotencyKey("branch"));

        var result = await harness.CreateCoordinator()
            .BranchAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionBranchFailed>();
        sink.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WhenDeleteSucceeds_PublishesDeletedEvent()
    {
        var sink = new FakeSessionEventSink();
        var harness = new Harness(eventSinks: [sink]);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnDelete = _ => new SessionDeleted(descriptor.Address);
        var request = new SessionDeleteRequest(context, new IdempotencyKey("delete"));

        var result = await harness.CreateCoordinator()
            .DeleteAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDeleted>();
        var published = sink.Received.ShouldHaveSingleItem().ShouldBeOfType<SessionDeletedEvent>();
        published.Address.ShouldBe(descriptor.Address);
    }

    [Fact]
    public async Task DeleteAsync_WhenDeleteFails_DoesNotPublishEvent()
    {
        var sink = new FakeSessionEventSink();
        var harness = new Harness(eventSinks: [sink]);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnDelete = _ => new SessionDeleteFailed("unavailable");
        var request = new SessionDeleteRequest(context, new IdempotencyKey("delete"));

        var result = await harness.CreateCoordinator()
            .DeleteAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDeleteFailed>();
        sink.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenCapabilitySelectedDifferentCoordinator_RejectsBeforeRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var alternate = new FakeRunStateSessionCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), alternate, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var context = TestFactory.BeforeRunLaneContext(address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.ProvisionLaneAsync(TestFactory.LaneProvisionRequest(context), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>();
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenCapabilityMatchesThisCoordinator_ForwardsToStore()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.ProvisionLaneAsync(TestFactory.LaneProvisionRequest(context), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>();
        _ = harness.Directory.LocateRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AdmitInputAsync_WhenCapabilitySelectedDifferentCoordinator_RejectsBeforeRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var alternate = new FakeRunStateSessionCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), alternate, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var context = TestFactory.BeforeRunLaneContext(address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.AdmitInputAsync(TestFactory.InputAdmissionRequest(context), capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.Unauthorized);
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AdmitInputAsync_WhenCapabilityMatchesThisCoordinator_ForwardsToStore()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.AdmitInputAsync(TestFactory.InputAdmissionRequest(context), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<RejectedInput>();
        _ = harness.Directory.LocateRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AcceptRunAsync_WhenCapabilitySelectedDifferentCoordinator_RejectsBeforeRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var alternate = new FakeRunStateSessionCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), alternate, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var context = TestFactory.BeforeRunLaneContext(address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.AcceptRunAsync(
            TestFactory.RunStartRequest(context, new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid())), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunStartRejected>();
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptRunAsync_WhenCapabilityMatchesThisCoordinator_ForwardsToStore()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.AcceptRunAsync(
            TestFactory.RunStartRequest(context, new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid())), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunStartRejected>();
        _ = harness.Directory.LocateRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task LoadRunStateAsync_WhenCapabilitySelectedDifferentCoordinator_RejectsBeforeRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var alternate = new FakeRunStateSessionCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), alternate, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var context = TestFactory.InRunLaneContext(address, new ExecutionLaneId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

        var result = await coordinator.LoadRunStateAsync(TestFactory.RunStateRequest(context), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunStateUnavailable>();
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadRunStateAsync_WhenCapabilityMatchesThisCoordinator_ForwardsToStore()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var context = TestFactory.InRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

        var result = await coordinator.LoadRunStateAsync(TestFactory.RunStateRequest(context), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunStateUnavailable>();
        _ = harness.Directory.LocateRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReleaseRunAsync_WhenCapabilitySelectedDifferentCoordinator_RejectsBeforeRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var alternate = new FakeRunStateSessionCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), alternate, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var context = TestFactory.InRunLaneContext(address, new ExecutionLaneId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

        var result = await coordinator.ReleaseRunAsync(TestFactory.RunReleaseRequest(context), capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().Kind.ShouldBe(SessionRunReleaseRejectionKind.Unsupported);
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseRunAsync_WhenCapabilityMatchesThisCoordinator_ForwardsToStore()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var context = TestFactory.InRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

        var result = await coordinator.ReleaseRunAsync(TestFactory.RunReleaseRequest(context), capability,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunReleaseRejected>();
        _ = harness.Directory.LocateRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReadAsync_WhenRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 8);

        var result = await coordinator.ReadAsync(request, TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionReadFailed>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task BranchAsync_WhenRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionBranchRequest(context, descriptor.ActiveBranchId, new SessionSequence(0),
            new IdempotencyKey("branch"));

        var result = await coordinator.BranchAsync(request, TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionBranchFailed>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task DeleteAsync_WhenRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionDeleteRequest(context, new IdempotencyKey("delete"));

        var result = await coordinator.DeleteAsync(request, TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionDeleteFailed>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task LookupInputAsync_WhenCapabilityMatchesAndRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var address = TestFactory.Descriptor().Address;
        var context = TestFactory.BeforeRunLaneContext(address, new ExecutionLaneId(Guid.NewGuid()));
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp,
            [new TextPart("content", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var request = new SessionInputLookupRequest(context, input, new InputFingerprint("sha256:input"));

        var result = await coordinator.LookupInputAsync(request, capability, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionInputLookupRejected>().SafeReason
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenCapabilityMatchesAndRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.ProvisionLaneAsync(TestFactory.LaneProvisionRequest(context), capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task AdmitInputAsync_WhenCapabilityMatchesAndRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.AdmitInputAsync(TestFactory.InputAdmissionRequest(context), capability,
            TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<RejectedInput>();
        rejected.Rejection.Kind.ShouldBe(InputRejectionKind.Unauthorized);
        rejected.Rejection.SafeReason.ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task AcceptRunAsync_WhenCapabilityMatchesAndRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var result = await coordinator.AcceptRunAsync(
            TestFactory.RunStartRequest(context, new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid())), capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task LoadRunStateAsync_WhenCapabilityMatchesAndRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.InRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

        var result = await coordinator.LoadRunStateAsync(TestFactory.RunStateRequest(context), capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStateUnavailable>().SafeReason
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task ReleaseRunAsync_WhenCapabilityMatchesAndRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.InRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

        var result = await coordinator.ReleaseRunAsync(TestFactory.RunReleaseRequest(context), capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().SafeReason
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenAlreadyCancelled_RecordsCorrelatedCancellationBeforeRouting()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await coordinator.ProvisionLaneAsync(TestFactory.LaneProvisionRequest(context), capability,
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProvisionLaneAsync_WhenDirectoryThrows_RecordsCorrelatedFaultAndPropagates()
    {
        var harness = new Harness();
        harness.Directory.OnLocate = static _ => throw new InvalidOperationException("directory outage");
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await coordinator.ProvisionLaneAsync(TestFactory.LaneProvisionRequest(context), capability,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("directory outage");
    }

    [Fact]
    public async Task CreateAsync_WhenProfileRequiresDurableStoreAndDirectoryIsNotDurable_ReturnsFailed()
    {
        var harness = new Harness();
        harness.Directory.DurableOverride = false;
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(),
            TestFactory.Profile(requiresDurableStore: true), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("The session directory cannot satisfy the profile's durability requirement.");
    }

    [Fact]
    public async Task CreateAsync_WhenLookupGrantIsDenied_ReturnsFailed()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session creation-route lookup was not authorized.");
    }

    [Fact]
    public async Task CreateAsync_WhenLookupReturnsConflict_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationConflict("replay conflict");
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("replay conflict");
    }

    [Fact]
    public async Task CreateAsync_WhenLookupIsDeniedByDirectory_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionDirectoryCreationLookupDenied("denied lookup");
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("denied lookup");
    }

    [Fact]
    public async Task CreateAsync_WhenLookupIsUnavailable_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionDirectoryCreationLookupUnavailable("store outage");
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("store outage");
    }

    [Fact]
    public async Task CreateAsync_WhenInitialStoreSelectionFails_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile("missing-store"),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("The selected session store is unavailable.");
        harness.SessionIds.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_WhenRecordGrantIsDenied_ReturnsFailed()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.Authority.DenyWhen = request =>
            request.Audience == harness.Directory.SecurityAudience
            && request.Kind == SecurityOperationKind.StateMutation
            && request.Effect == SecurityEffect.Mutate;
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session creation-route recording was not authorized.");
    }

    [Fact]
    public async Task CreateAsync_WhenRecordCreateConflicts_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.Directory.OnRecordCreate = wrapper =>
            new SessionLocationConflict(wrapper.Request.Location, wrapper.Request.Location.StoreKey);
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("The session creation route conflicts with an existing route.");
    }

    [Fact]
    public async Task CreateAsync_WhenRecordCreateIsDenied_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.Directory.OnRecordCreate = _ => new SessionDirectoryWriteDenied("write denied");
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("write denied");
    }

    [Fact]
    public async Task CreateAsync_WhenRecordCreateIsUnavailable_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.Directory.OnRecordCreate = _ => new SessionDirectoryWriteUnavailable("write outage");
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldBe("write outage");
    }

    [Fact]
    public async Task CreateAsync_WhenCaptureCreateContextReturnsUnavailable_ReturnsFailed()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.ProfileSelector.Override = static request =>
            new SecurityAuthorizationCaptureUnavailable("capture failed");
        harness.Directory.OnRecordCreate = wrapper => new SessionLocationRecorded(wrapper.Request.Location, existing: false);
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session-bound authorization capture is unavailable.");
    }

    [Fact]
    public async Task CreateAsync_WhenCapturedAuthorizationDoesNotMatchRequest_ReturnsFailed()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.ProfileSelector.Override = request => new SecurityAuthorizationCaptured(
            new SecurityAuthorizationContext(request.ProfileKey, new SecurityProfileVersion(999),
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
                new ComponentKey<ISecurityAuthority>("authority"), request.AgentDefinitionRevision,
                request.ConfigurationVersion, request.Scope, request.Identity));
        harness.Directory.OnRecordCreate = wrapper => new SessionLocationRecorded(wrapper.Request.Location, existing: false);
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session-bound authorization capture is unavailable.");
    }

    [Fact]
    public async Task CreateAsync_WhenFinalStoreGrantIsDenied_ReturnsFailed()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationNotFound();
        harness.Authority.DenyWhen = request =>
            request.Audience == harness.Store.SecurityAudience
            && request.Kind == SecurityOperationKind.StateMutation
            && request.Effect == SecurityEffect.Create;
        harness.Directory.OnRecordCreate = wrapper => new SessionLocationRecorded(wrapper.Request.Location, existing: false);
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session store creation was not authorized.");
        harness.Store.ReceivedCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenExistingRouteStoreSelectionFails_ReturnsFailedWithSafeMessage()
    {
        var winner = Location("fake");
        var harness = new Harness();
        harness.Directory.OnLocateCreate = _ => new SessionCreationLocationLocated(winner);
        var coordinator = harness.CreateCoordinator();
        var profile = TestFactory.Profile(requiredStoreCapabilities: SessionStoreCapabilities.Snapshots);

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(winner.Address.AgentId), profile,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("The selected session store cannot satisfy the profile's capability requirement.");
        harness.Store.ReceivedCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteExistingAsync_WhenProfileRequiresDurableStoreAndDirectoryIsNotDurable_ReturnsFailed()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(requiresDurableStore: true), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage
            .ShouldBe("The session directory cannot satisfy the profile's durability requirement.");
        harness.Directory.LocateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteExistingAsync_WhenRouteLookupReturnsNotFound_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocationNotFound(descriptor.Address);

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("The session route is unavailable.");
    }

    [Fact]
    public async Task ExecuteExistingAsync_WhenRouteLookupIsDenied_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionDirectoryLookupDenied("route denied");

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("route denied");
    }

    [Fact]
    public async Task ExecuteExistingAsync_WhenRouteLookupIsUnavailable_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionDirectoryLookupUnavailable("route outage");

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage.ShouldBe("route outage");
    }

    [Fact]
    public async Task ExecuteExistingAsync_WhenStoreSelectionFails_ReturnsFailedWithSafeMessage()
    {
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var coordinator = harness.CreateCoordinator();
        var profile = TestFactory.Profile(requiredStoreCapabilities: SessionStoreCapabilities.Snapshots);

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address), profile,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage
            .ShouldBe("The selected session store cannot satisfy the profile's capability requirement.");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAuthoritySelectionIsUnavailable_ReturnsFailed()
    {
        var harness = new Harness();
        harness.AuthoritySelector.ReturnUnavailable = true;
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSelectedAuthorizationDiffersFromRequested_ReturnsFailed()
    {
        var harness = new Harness();
        harness.AuthoritySelector.ReturnMismatchedAuthorization = true;
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task AppendAsync_WhenRoutingIsDenied_InvokesTypedFailureFactory()
    {
        var harness = new Harness();
        harness.Authority.Allow = false;
        var descriptor = TestFactory.Descriptor();
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1)]);

        var result = await harness.CreateCoordinator()
            .AppendAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage
            .ShouldBe("Session route lookup was not authorized.");
    }

    [Fact]
    public async Task ReadAsync_WhenRoutingSucceeds_InvokesStoreOperation()
    {
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var page = new SessionPage([], new SessionSequence(0), hasMore: false);
        harness.Store.OnRead = _ => page;
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 8);

        var result = await harness.CreateCoordinator()
            .ReadAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(page);
    }

    [Fact]
    public async Task LookupInputAsync_WhenCapabilityMatchesAndRoutingSucceeds_ForwardsToStore()
    {
        var harness = new Harness();
        var coordinator = harness.CreateCoordinator();
        var runCoordinator = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(TestFactory.Profile(), coordinator, runCoordinator);
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        var context = TestFactory.BeforeRunLaneContext(descriptor.Address, new ExecutionLaneId(Guid.NewGuid()));
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp,
            [new TextPart("content", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var request = new SessionInputLookupRequest(context, input, new InputFingerprint("sha256:input"));

        var result = await coordinator.LookupInputAsync(request, capability, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionInputLookupRejected>();
        _ = harness.Directory.LocateRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AppendAsync_WhenStoreThrowsException_RecordsFaultAndPropagates()
    {
        var harness = new Harness();
        var descriptor = TestFactory.Descriptor();
        harness.Directory.OnLocate = _ => new SessionLocated(Location("fake", descriptor.Address));
        harness.Store.OnAppend = static _ => throw new InvalidOperationException("store faulted");
        var context = TestFactory.OperationContext(descriptor.Address);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1)]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.CreateCoordinator()
                .AppendAsync(request, TestFactory.Profile(), TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("store faulted");
    }

    [Fact]
    public async Task CreateAsync_WhenLookupReturnsUnsupportedResult_ReturnsFailedWithGenericMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = static _ => new UnknownCreationLocationResult();
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session creation-route lookup returned an unsupported result.");
    }

    [Fact]
    public async Task CreateAsync_WhenRecordCreateReturnsUnsupportedResult_ReturnsFailedWithGenericMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocateCreate = static _ => new SessionCreationLocationNotFound();
        harness.Directory.OnRecordCreate = static _ => new UnknownDirectoryWriteResult();
        var coordinator = harness.CreateCoordinator();

        var result = await coordinator.CreateAsync(TestFactory.CreateRequest(), TestFactory.Profile(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage
            .ShouldBe("Session creation-route recording returned an unsupported result.");
    }

    [Fact]
    public async Task ExecuteExistingAsync_WhenRouteLookupReturnsUnsupportedResult_ReturnsFailedWithGenericMessage()
    {
        var harness = new Harness();
        harness.Directory.OnLocate = static _ => new UnknownLocationResult();
        var coordinator = harness.CreateCoordinator();
        var descriptor = TestFactory.Descriptor();

        var result = await coordinator.LoadAsync(TestFactory.OperationContext(descriptor.Address),
            TestFactory.Profile(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionLoadFailed>().SafeMessage
            .ShouldBe("Session route lookup returned an unsupported result.");
    }

    private sealed record UnknownCreationLocationResult: SessionCreationLocationResult;

    private sealed record UnknownDirectoryWriteResult: SessionDirectoryWriteResult;

    private sealed record UnknownLocationResult: SessionLocationResult;

    private static SessionLocation Location(string storeKey, SessionAddress? address = null) => new(
        address ?? new SessionAddress(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"))),
        new TenantId("tenant-1"), new SessionStoreKey(storeKey), new SessionDirectoryRevision(1),
        DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));

    private sealed class Harness
    {
        private readonly TimeProvider _time;

        public Harness(List<string>? order = null, IReadOnlyList<ISessionStore>? stores = null,
            IReadOnlyList<ISessionEventSink>? eventSinks = null, TimeProvider? timeProvider = null)
        {
            _time = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            Stores = stores ?? [new FakeSessionStore()];
            Store = (FakeSessionStore) Stores[0];
            Directory = new RecordingDirectory();
            Authority = new RecordingAuthority(_time);
            ProfileSelector = new RecordingProfileSelector(order);
            AuthoritySelector = new RecordingAuthoritySelector(Authority);
            EventSinks = eventSinks ?? [];
        }

        public RecordingDirectory Directory { get; }
        public RecordingAuthority Authority { get; }
        public RecordingProfileSelector ProfileSelector { get; }
        public RecordingAuthoritySelector AuthoritySelector { get; }
        public SequenceGenerator<SessionId> SessionIds { get; } = new(static value => new SessionId(value));
        public FakeSessionStore Store { get; }
        public IReadOnlyList<ISessionStore> Stores { get; }
        public IReadOnlyList<ISessionEventSink> EventSinks { get; }

        public DefaultSessionCoordinator CreateCoordinator(ILogger<DefaultSessionCoordinator>? logger = null) =>
            CreateCoordinator(Directory, new AgentSessionOptions(), logger);

        public DefaultSessionCoordinator CreateCoordinator(AgentSessionOptions options,
            ILogger<DefaultSessionCoordinator>? logger = null) => CreateCoordinator(Directory, options, logger);

        public DefaultSessionCoordinator CreateCoordinator(ISessionDirectory directory,
            ILogger<DefaultSessionCoordinator>? logger = null) =>
            CreateCoordinator(directory, new AgentSessionOptions(), logger);

        private DefaultSessionCoordinator CreateCoordinator(ISessionDirectory directory, AgentSessionOptions options,
            ILogger<DefaultSessionCoordinator>? logger) => new(
            directory,
            new DefaultSessionStoreSelector(Stores, NullLogger<DefaultSessionStoreSelector>.Instance),
            ProfileSelector,
            AuthoritySelector,
            SessionIds,
            new SequenceGenerator<SecurityRequestId>(static value => new SecurityRequestId(value)),
            new SequenceGenerator<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value)),
            EventSinks, _time, Options.Create(options), logger);
    }

    private sealed class RecordingDirectory: ISessionDirectory
    {
        public bool Durable => DurableOverride;
        public ComponentId SecurityAudience { get; } = new("agentkit.session.tests.directory");
        public List<AuthorizedSessionDirectoryRequest<SessionOperationContext>> LocateRequests { get; } = [];
        public List<AuthorizedSessionDirectoryRequest<SessionCreateRequest>> LocateCreateRequests { get; } = [];
        public List<AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>> RecordCreateRequests { get; } = [];
        public List<AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>> ListRequests { get; } = [];
        public Func<AuthorizedSessionDirectoryRequest<SessionOperationContext>, SessionLocationResult>? OnLocate { get; set; }
        public Func<AuthorizedSessionDirectoryRequest<SessionCreateRequest>, SessionCreationLocationResult>? OnLocateCreate { get; set; }
        public Func<AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>, SessionDirectoryWriteResult>? OnRecordCreate { get; set; }
        public Func<AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>, SessionDirectoryListResult>? OnList { get; set; }
        public bool DurableOverride { get; set; }

        public ValueTask<SessionLocationResult> LocateAsync(
            AuthorizedSessionDirectoryRequest<SessionOperationContext> request, CancellationToken cancellationToken = default)
        {
            LocateRequests.Add(request);
            return ValueTask.FromResult(OnLocate?.Invoke(request) ?? new SessionLocationNotFound(request.Request.ToAddress()));
        }

        public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(
            AuthorizedSessionDirectoryRequest<SessionCreateRequest> request, CancellationToken cancellationToken = default)
        {
            LocateCreateRequests.Add(request);
            return ValueTask.FromResult(OnLocateCreate?.Invoke(request) ?? new SessionCreationLocationNotFound());
        }

        public ValueTask<SessionDirectoryWriteResult> RecordAsync(
            AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SessionDirectoryWriteResult>(new SessionDirectoryWriteUnavailable("not configured"));

        public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(
            AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request,
            CancellationToken cancellationToken = default)
        {
            RecordCreateRequests.Add(request);
            return ValueTask.FromResult(OnRecordCreate?.Invoke(request)
                ?? new SessionDirectoryWriteUnavailable("not configured"));
        }

        public ValueTask<SessionDirectoryListResult> ListAsync(
            AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest> request,
            CancellationToken cancellationToken = default)
        {
            ListRequests.Add(request);
            return ValueTask.FromResult(OnList?.Invoke(request)
                ?? new SessionDirectoryListUnavailable("not configured"));
        }
    }

    private sealed class RecordingProfileSelector(List<string>? order): ISecurityProfileSelector
    {
        public Func<SecurityAuthorizationCaptureRequest, SecurityAuthorizationCaptureResult>? Override { get; set; }

        public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(SecurityAuthorizationCaptureRequest request,
            CancellationToken cancellationToken = default)
        {
            order?.Add("capture");
            return ValueTask.FromResult(Override?.Invoke(request) ?? DefaultCapture(request));
        }

        private static SecurityAuthorizationCaptured DefaultCapture(SecurityAuthorizationCaptureRequest request)
        {
            return new SecurityAuthorizationCaptured(
                new SecurityAuthorizationContext(request.ProfileKey, new SecurityProfileVersion(1),
                    new SecurityPolicySnapshotReference(
                        new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                        new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
                    new ComponentKey<ISecurityAuthority>("authority"), request.AgentDefinitionRevision,
                    request.ConfigurationVersion, request.Scope, request.Identity));
        }
    }

    private sealed class RecordingAuthoritySelector(RecordingAuthority authority): ISecurityAuthoritySelector
    {
        public bool ReturnUnavailable { get; set; }
        public bool ReturnMismatchedAuthorization { get; set; }

        public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(SecurityAuthorizationContext authorization,
            CancellationToken cancellationToken = default)
        {
            if (ReturnUnavailable)
            {
                return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
                    new SecurityAuthoritySelectionUnavailable(authorization, "selection unavailable"));
            }
            if (ReturnMismatchedAuthorization)
            {
                var different = new SecurityAuthorizationContext(authorization.ProfileKey,
                    new SecurityProfileVersion(999), authorization.PolicySnapshot, authorization.AuthorityKey,
                    authorization.AgentDefinitionRevision, authorization.ConfigurationVersion, authorization.Scope,
                    authorization.Identity);
                return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
                    new SecurityAuthoritySelected(different, authority));
            }
            return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
                new SecurityAuthoritySelected(authorization, authority));
        }
    }

    private sealed class RecordingAuthority(TimeProvider time): ISecurityAuthority
    {
        public bool Allow { get; set; } = true;
        public Func<SecurityRequest, bool>? DenyWhen { get; set; }
        public List<SecurityRequest> Requests { get; } = [];
        public List<SecurityGrant> Grants { get; } = [];

        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (!Allow || (DenyWhen?.Invoke(request) ?? false))
            {
                return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(request.Id,
                    new SecurityPolicyVersion(1), new SecurityDenial("policy_denied", "denied")));
            }
            var grant = new SecurityGrant(new GrantId(Guid.NewGuid()), request.Id, request.Scope, request.Identity,
                request.Authorization!, request.Audience, request.Kind, request.Effect, request.Resources,
                request.InputFingerprint, new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
                time.GetUtcNow(), time.GetUtcNow().AddMinutes(1), 1);
            Grants.Add(grant);
            return ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant));
        }
    }

    private sealed class SequenceGenerator<T>(Func<Guid, T> factory): IIdentifierGenerator<T>
        where T : struct
    {
        public int Count { get; private set; }
        public T Create()
        {
            Count++;
            return factory(new Guid(Count, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
        }
    }

    private sealed class FakeSessionStoreWithKey: ISessionStore
    {
        public FakeSessionStoreWithKey(string key)
        {
            Descriptor = new SessionStoreDescriptor(new SessionStoreKey(key), SessionStoreCapabilities.None,
                SessionConsistencyModel.Strong, durable: false, supportsDistributedFencing: false);
        }
        public ComponentId SecurityAudience { get; } = new("agentkit.session.tests.other-store");
        public SessionStoreDescriptor Descriptor { get; }
        public Func<SessionStoreCreateRequest, SessionCreateResult>? OnCreate { get; set; }
        public List<AuthorizedSessionStoreRequest<SessionStoreCreateRequest>> ReceivedCreates { get; } = [];
        public ValueTask<SessionCreateResult> CreateAsync(AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request,
            CancellationToken cancellationToken = default)
        {
            ReceivedCreates.Add(request);
            return ValueTask.FromResult(OnCreate?.Invoke(request.Request) ?? new SessionCreateFailed("not configured"));
        }
        public ValueTask<SessionLoadResult> LoadAsync(AuthorizedSessionStoreRequest<SessionOperationContext> context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(AuthorizedSessionStoreRequest<SessionAppendRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(AuthorizedSessionStoreRequest<SessionReadRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionBranchResult> CreateBranchAsync(AuthorizedSessionStoreRequest<SessionBranchRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(AuthorizedSessionStoreRequest<SessionDeleteRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionInputLookupResult> LookupInputAsync(AuthorizedSessionStoreRequest<SessionInputLookupRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<InputAdmissionResult> AdmitInputAsync(AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunStartResult> AcceptRunAsync(AuthorizedSessionStoreRequest<SessionRunStartRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunStateResult> LoadRunStateAsync(AuthorizedSessionStoreRequest<SessionRunStateRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingLogger: ILogger<DefaultSessionCoordinator>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("observer");
    }

    private sealed class ThrowingSessionEventSink: ISessionEventSink
    {
        public ValueTask PublishAsync(SessionEvent sessionEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("observer");
    }

    private sealed class ArmableThrowingTimeProvider: TimeProvider
    {
        private bool _armed;

        public bool ArmedReadAttempted { get; private set; }

        public void Arm() => _armed = true;

        public override DateTimeOffset GetUtcNow()
        {
            if (_armed)
            {
                ArmedReadAttempted = true;
                throw new InvalidOperationException("Event clock failed.");
            }

            return DateTimeOffset.UnixEpoch;
        }
    }
}
