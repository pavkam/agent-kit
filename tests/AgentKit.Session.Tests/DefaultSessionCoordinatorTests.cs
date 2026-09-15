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

    private static SessionLocation Location(string storeKey, SessionAddress? address = null) => new(
        address ?? new SessionAddress(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"))),
        new TenantId("tenant-1"), new SessionStoreKey(storeKey), new SessionDirectoryRevision(1),
        DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));

    private sealed class Harness
    {
        private readonly TimeProvider _time;
        private readonly RecordingProfileSelector _profileSelector;
        private readonly RecordingAuthoritySelector _authoritySelector;

        public Harness(List<string>? order = null, IReadOnlyList<ISessionStore>? stores = null,
            IReadOnlyList<ISessionEventSink>? eventSinks = null, TimeProvider? timeProvider = null)
        {
            _time = timeProvider ?? new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            Stores = stores ?? [new FakeSessionStore()];
            Store = (FakeSessionStore) Stores[0];
            Directory = new RecordingDirectory();
            Authority = new RecordingAuthority(_time);
            _profileSelector = new RecordingProfileSelector(order);
            _authoritySelector = new RecordingAuthoritySelector(Authority);
            EventSinks = eventSinks ?? [];
        }

        public RecordingDirectory Directory { get; }
        public RecordingAuthority Authority { get; }
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
            _profileSelector,
            _authoritySelector,
            SessionIds,
            new SequenceGenerator<SecurityRequestId>(static value => new SecurityRequestId(value)),
            new SequenceGenerator<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value)),
            EventSinks, _time, Options.Create(options), logger);
    }

    private sealed class RecordingDirectory: ISessionDirectory
    {
        public bool Durable => false;
        public ComponentId SecurityAudience { get; } = new("agentkit.session.tests.directory");
        public List<AuthorizedSessionDirectoryRequest<SessionOperationContext>> LocateRequests { get; } = [];
        public List<AuthorizedSessionDirectoryRequest<SessionCreateRequest>> LocateCreateRequests { get; } = [];
        public List<AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>> RecordCreateRequests { get; } = [];
        public Func<AuthorizedSessionDirectoryRequest<SessionOperationContext>, SessionLocationResult>? OnLocate { get; set; }
        public Func<AuthorizedSessionDirectoryRequest<SessionCreateRequest>, SessionCreationLocationResult>? OnLocateCreate { get; set; }
        public Func<AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>, SessionDirectoryWriteResult>? OnRecordCreate { get; set; }

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
    }

    private sealed class RecordingProfileSelector(List<string>? order): ISecurityProfileSelector
    {
        public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(SecurityAuthorizationCaptureRequest request,
            CancellationToken cancellationToken = default)
        {
            order?.Add("capture");
            return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
                new SecurityAuthorizationContext(request.ProfileKey, new SecurityProfileVersion(1),
                    new SecurityPolicySnapshotReference(
                        new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                        new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
                    new ComponentKey<ISecurityAuthority>("authority"), request.AgentDefinitionRevision,
                    request.ConfigurationVersion, request.Scope, request.Identity)));
        }
    }

    private sealed class RecordingAuthoritySelector(RecordingAuthority authority): ISecurityAuthoritySelector
    {
        public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(SecurityAuthorizationContext authorization,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuthoritySelectionResult>(new SecurityAuthoritySelected(authorization, authority));
    }

    private sealed class RecordingAuthority(TimeProvider time): ISecurityAuthority
    {
        public bool Allow { get; set; } = true;
        public List<SecurityRequest> Requests { get; } = [];
        public List<SecurityGrant> Grants { get; } = [];

        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (!Allow)
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
