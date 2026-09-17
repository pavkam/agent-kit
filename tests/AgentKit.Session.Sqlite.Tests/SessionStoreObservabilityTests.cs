// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Verifies that SQLite session-store diagnostics stay isolated from committed results.</summary>
public sealed class SessionStoreObservabilityTests
{
    [Fact]
    public async Task CreateAsync_WhenLoggerThrows_PreservesCommittedResult()
    {
        using var directory = new TempDirectory();
        var harness = Harness.Create(directory.Path, TimeProvider.System, new ThrowingSessionStoreLogger());

        var result = await harness.Store.CreateAsync(
            await harness.AuthorizeAsync(Harness.CreateStoreRequest()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionCreated>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_WhenActivityListenerThrows_PreservesCommittedResultAndParent(bool throwOnStart)
    {
        using var directory = new TempDirectory();
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger: null);
        using var parent = new Activity("session.sqlite.store.test.parent").Start();
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

    [Fact]
    public async Task CreateAsync_WhenMeterListenerThrows_PreservesCommittedResult()
    {
        using var directory = new TempDirectory();
        var harness = Harness.Create(directory.Path, TimeProvider.System, logger: null);
        using var parent = new Activity("session.sqlite.store.meter.test.parent").Start();
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

    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsOnlyBoundedContentFreeDiagnostics()
    {
        const string protectedMarker = "never-export-this-request-marker";
        using var directory = new TempDirectory();
        var logger = new RecordingSessionStoreLogger();
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
        events.ShouldContain(static item => item.EventId.Id == 25000);
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

    [Fact]
    public async Task CreateAsync_WhenCoreOperationThrowsAfterSuccessfulAuthorization_PropagatesAndReportsFaulted()
    {
        using var directory = new TempDirectory();
        var logger = new RecordingSessionStoreLogger();
        var harness = Harness.Create(directory.Path, new ThrowsAfterTimeProvider(TimeProvider.System, throwOnCall: 2), logger);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Store.CreateAsync(
                await harness.AuthorizeAsync(Harness.CreateStoreRequest()), TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Simulated clock failure.");
        logger.Snapshot().ShouldContain(static item => item.EventId.Id == 25001);
    }

    /// <summary>Composes a standalone <see cref="SqliteSessionStore"/> with independently controllable diagnostics
    /// and clock collaborators, decoupled from the grant store's own timestamping clock.</summary>
    private sealed class Harness: ISecurityAuditDispatcher
    {
        private readonly InMemorySecurityGrantStore _grants;
        private long _nextIdentity;

        private Harness(SqliteSessionStore store, InMemorySecurityGrantStore grants)
        {
            Store = store;
            _grants = grants;
        }

        public SqliteSessionStore Store { get; }

        public static Harness Create(string directory, TimeProvider storeClock, ILogger<SqliteSessionStore>? logger)
        {
            var grants = new InMemorySecurityGrantStore(TimeProvider.System);
            var services = new ServiceCollection();
            _ = services.AddSingleton(storeClock);
            var dispatcher = new AcceptingAuditDispatcher();
            _ = services.AddSingleton<ISecurityAuditDispatcher>(dispatcher);
            _ = services.AddSingleton<ISecurityGrantStore>(grants);
            _ = services.AddSqliteSessionStore(new SqliteSessionStoreTarget(
                Path.Combine(directory, "sessions.db"),
                new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing,
                SqliteSchemaMode.ApplyKnownMigrations));
            if (logger is not null)
            {
                // Registered last so it wins resolution over AddSqliteSessionStore's own TryAdd default.
                _ = services.AddSingleton(logger);
            }

            // Intentionally not disposed: the harness's store must outlive this factory call, and these
            // short-lived unit tests do not depend on provider disposal semantics.
            var provider = services.BuildServiceProvider();
            var store = (SqliteSessionStore) provider.GetRequiredService<ISessionStore>();
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

        public async ValueTask<AuthorizedSessionStoreRequest<SessionStoreCreateRequest>> AuthorizeAsync(
            SessionStoreCreateRequest request)
        {
            var resource = SessionStoreSecurityBinding.Resource(Store.Descriptor.Key, request.Context.ToAddress());
            var grant = new SecurityGrant(
                new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), request.Context.Authorization.Scope,
                request.Context.Identity, request.Context.Authorization, Store.SecurityAudience,
                SecurityOperationKind.StateMutation, SecurityEffect.Create, [resource],
                SessionStoreSecurityBinding.Fingerprint(request), new SecurityPolicyVersion(1),
                new SecurityRevocationVersion(1), TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow().AddDays(1), 1);
            await _grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
            return new AuthorizedSessionStoreRequest<SessionStoreCreateRequest>(
                request, Store.Descriptor.Key, grant,
                new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
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

        private static T Identifier<T>(int value)
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
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentkit-session-observability-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

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
