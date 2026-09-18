// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

using Microsoft.Extensions.Logging;

/// <summary>Verifies that JSON session-store diagnostics stay isolated from committed results and carry no protected content.</summary>
public sealed class SessionStoreObservabilityTests
{
    /// <summary>Verifies a throwing logger does not change the semantic outcome of a successful operation.</summary>
    [Fact]
    public async Task CreateAsync_WhenLoggerThrows_PreservesCommittedResult()
    {
        using var directory = new TempDirectory();
        var logger = new RecordingLogger<JsonSessionStore> { ThrowOnWrite = true };
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger);

        var result = await harness.Store.CreateAsync(
            await harness.AuthorizeAsync(Harness.CreateStoreRequest()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
    }

    /// <summary>Verifies an activity listener that throws on start or stop does not change the committed result or the ambient parent.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_WhenActivityListenerThrows_PreservesCommittedResultAndParent(bool throwOnStart)
    {
        using var directory = new TempDirectory();
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger: null);
        using var parent = new Activity("session.json.store.test.parent").Start();
        var parentTraceId = parent.TraceId;
        var parentSpanId = parent.SpanId;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStarted = activity =>
            {
                if (throwOnStart
                    && activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                    && activity.TraceId == parentTraceId
                    && activity.ParentSpanId == parentSpanId)
                {
                    throw new InvalidOperationException("listener start failure");
                }
            },
            ActivityStopped = activity =>
            {
                if (!throwOnStart
                    && activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                    && activity.TraceId == parentTraceId
                    && activity.ParentSpanId == parentSpanId)
                {
                    throw new InvalidOperationException("listener stop failure");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        var result = await harness.Store.CreateAsync(
            await harness.AuthorizeAsync(Harness.CreateStoreRequest()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    /// <summary>Verifies a throwing meter listener does not change the committed result.</summary>
    [Fact]
    public async Task CreateAsync_WhenMeterListenerThrows_PreservesCommittedResult()
    {
        using var directory = new TempDirectory();
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger: null);
        using var parent = new Activity("session.json.store.meter.test.parent").Start();
        var parentTraceId = parent.TraceId;
        using var listener = new MeterListener
        {
            InstrumentPublished = static (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionStoreOperationCount)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.TraceId == parentTraceId)
            {
                throw new InvalidOperationException("meter listener failure");
            }
        });
        listener.Start();

        var result = await harness.Store.CreateAsync(
            await harness.AuthorizeAsync(Harness.CreateStoreRequest()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
    }

    /// <summary>Verifies a successful operation emits exactly the documented event, activity, and metric with no protected content.</summary>
    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsOnlyBoundedContentFreeDiagnostics()
    {
        const string protectedMarker = "never-export-this-request-marker";
        using var directory = new TempDirectory();
        var logger = new RecordingLogger<JsonSessionStore>();
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger);
        var request = Harness.CreateStoreRequest(protectedMarker);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(request.Request.AgentId.ToString()) == true);
        var measurements = new ConcurrentQueue<ImmutableArray<KeyValuePair<string, object?>>>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionStoreOperationCount)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Enqueue([.. tags]));
        meterListener.Start();

        _ = (await harness.Store.CreateAsync(await harness.AuthorizeAsync(request), TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("succeeded");
        foreach (var value in activity.Tags.Values)
        {
            string.Equals(value?.ToString(), protectedMarker, StringComparison.Ordinal).ShouldBeFalse();
        }

        var events = logger.Snapshot();
        events.ShouldContain(static item => item.EventId.Id == 19300);
        foreach (var item in events)
        {
            item.Message.Contains(protectedMarker, StringComparison.Ordinal).ShouldBeFalse();
        }

        measurements.Any(tags =>
            tags.Any(static tag => tag.Key == AgentKitTagNames.SessionOperation
                && tag.Value?.ToString() == "create")
            && tags.Any(static tag => tag.Key == AgentKitTagNames.Outcome
                && tag.Value?.ToString() == "succeeded")).ShouldBeTrue();
        foreach (var tags in measurements)
        {
            tags.All(static tag =>
                tag.Key is AgentKitTagNames.SessionOperation or AgentKitTagNames.Outcome).ShouldBeTrue();
        }
    }

    /// <summary>Verifies an unexpected failure after successful authorization propagates and reports the failed event, without protected content.</summary>
    [Fact]
    public async Task CreateAsync_WhenCoreOperationThrowsAfterSuccessfulAuthorization_PropagatesAndReportsFailed()
    {
        using var directory = new TempDirectory();
        var logger = new RecordingLogger<JsonSessionStore>();
        var harness = Harness.Create(directory.Path, new ThrowsAfterTimeProvider(TimeProvider.System, throwOnCall: 2), logger);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Store.CreateAsync(
                await harness.AuthorizeAsync(Harness.CreateStoreRequest()), TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Simulated clock failure.");
        logger.Snapshot().ShouldContain(static item => item.EventId.Id == 19301);
    }

    /// <summary>Verifies a rejected operation (denied authorization) is observed with the same content-free contract as a success.</summary>
    [Fact]
    public async Task LoadAsync_WhenGrantDoesNotBindExactRequest_EmitsRejectedOutcome()
    {
        using var directory = new TempDirectory();
        var logger = new RecordingLogger<JsonSessionStore>();
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger);
        var create = Harness.CreateStoreRequest();
        var descriptor = (await harness.Store.CreateAsync(
            await harness.AuthorizeAsync(create), TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>().Descriptor;
        var context = Harness.LoadContext(descriptor.Address);
        var authorized = await harness.AuthorizeAsync(context);
        var mismatched = new AuthorizedSessionStoreRequest<SessionOperationContext>(
            Harness.LoadContext(new SessionAddress(Harness.Identifier<AgentId>(9999), descriptor.Address.SessionId)),
            authorized.StoreKey, authorized.Grant, authorized.Intent);

        var result = await harness.Store.LoadAsync(mismatched, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLoadFailed>();
        logger.Snapshot().ShouldContain(static item => item.EventId.Id == 19300);
    }

    /// <summary>Composes a standalone <see cref="JsonSessionStore"/> with independently controllable diagnostics and clock collaborators.</summary>
    private sealed class Harness: ISecurityAuditDispatcher
    {
        private readonly InMemorySecurityGrantStore _grants;
        private long _nextIdentity;

        private Harness(JsonSessionStore store, InMemorySecurityGrantStore grants)
        {
            Store = store;
            _grants = grants;
        }

        public JsonSessionStore Store { get; }

        public static Harness Create(string directory, TimeProvider storeClock, ILogger<JsonSessionStore>? logger)
        {
            var grants = new InMemorySecurityGrantStore(TimeProvider.System);
            var services = new ServiceCollection();
            _ = services.AddSingleton(storeClock);
            _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
            _ = services.AddSingleton<ISecurityGrantStore>(grants);
            _ = services.AddJsonSessionStore(new JsonSessionStoreTarget(
                Path.Combine(directory, "sessions"),
                new JsonSessionStoreInstanceId(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing,
                JsonStoreRecoveryMode.RecoverTornAppends));
            if (logger is not null)
            {
                // Registered last so it wins resolution over AddJsonSessionStore's own TryAdd default.
                _ = services.AddSingleton(logger);
            }

            // Intentionally not disposed: the harness's store must outlive this factory call, and these
            // short-lived unit tests do not depend on provider disposal semantics.
            var provider = services.BuildServiceProvider();
            var store = (JsonSessionStore) provider.GetRequiredService<ISessionStore>();
            store.InitializeAsync(TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();
            return new Harness(store, grants);
        }

        public static SessionStoreCreateRequest CreateStoreRequest(string idempotencyKey = "observability-create")
        {
            var agentId = Identifier<AgentId>(1);
            var identity = Identity();
            var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
            var authorization = Authorization(agentId, null, correlation, identity);
            var logical = new SessionCreateRequest(
                agentId, identity, authorization, Identifier<ConversationId>(3),
                new IdempotencyKey(idempotencyKey), ExtensionData.Empty);
            var address = new SessionAddress(agentId, Identifier<SessionId>(4));
            var context = new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
            return new SessionStoreCreateRequest(logical, address, context);
        }

        public static SessionOperationContext LoadContext(SessionAddress address)
        {
            var identity = Identity();
            var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
            return new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
        }

        public ValueTask<AuthorizedSessionStoreRequest<SessionStoreCreateRequest>> AuthorizeAsync(
            SessionStoreCreateRequest request) =>
            AuthorizeCoreAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                SessionStoreSecurityBinding.Resource(Store.Descriptor.Key, request.Context.ToAddress()),
                SessionStoreSecurityBinding.Fingerprint(request));

        public ValueTask<AuthorizedSessionStoreRequest<SessionOperationContext>> AuthorizeAsync(
            SessionOperationContext context) =>
            AuthorizeCoreAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                SessionStoreSecurityBinding.Resource(Store.Descriptor.Key, context.ToAddress()),
                SessionStoreSecurityBinding.Fingerprint(context));

        private async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeCoreAsync<TRequest>(
            TRequest request, SecurityOperationKind kind, SecurityEffect effect, ProtectedResource resource,
            InputFingerprint fingerprint)
            where TRequest : class
        {
            var context = request is SessionStoreCreateRequest create ? create.Context : (SessionOperationContext) (object) request;
            var grant = new SecurityGrant(
                new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), context.Authorization.Scope,
                context.Identity, context.Authorization, Store.SecurityAudience, kind, effect, [resource],
                fingerprint, new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
                TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow().AddDays(1), 1);
            await _grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
            return new AuthorizedSessionStoreRequest<TRequest>(
                request, Store.Descriptor.Key, grant, new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
        }

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());

        private Guid NextGuid()
        {
            var value = Interlocked.Increment(ref _nextIdentity);
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, value);
            bytes[15] = 9;
            return new Guid(bytes);
        }

        public static T Identifier<T>(int value)
        {
            var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9);
            return typeof(T) switch
            {
                var t when t == typeof(AgentId) => (T) (object) new AgentId(guid),
                var t when t == typeof(SessionId) => (T) (object) new SessionId(guid),
                var t when t == typeof(ConversationId) => (T) (object) new ConversationId(guid),
                var t when t == typeof(OperationId) => (T) (object) new OperationId(guid),
                var t when t == typeof(SecurityPolicySnapshotId) => (T) (object) new SecurityPolicySnapshotId(guid),
                _ => throw new NotSupportedException(typeof(T).Name),
            };
        }

        private static ExecutionIdentity Identity() =>
            TestExecutionIdentity.Create(new TenantId("tenant-observability"), new PrincipalId("owner"), ExecutionSubjectKind.Human);

        private static SecurityAuthorizationContext Authorization(
            AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
            new(new SecurityProfileKey("observability"), new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:observability-policy")),
                new ComponentKey<ISecurityAuthority>("observability"), new AgentDefinitionRevision(1),
                new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

        private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
        {
            public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
        }
    }

    private sealed class TempDirectory: IDisposable
    {
        public TempDirectory() => Path = TestTemporaryDirectory.Create();

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class ThrowsAfterTimeProvider(TimeProvider inner, int throwOnCall): TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => Interlocked.Increment(ref _calls) >= throwOnCall
            ? throw new InvalidOperationException("Simulated clock failure.")
            : inner.GetUtcNow();
    }
}
