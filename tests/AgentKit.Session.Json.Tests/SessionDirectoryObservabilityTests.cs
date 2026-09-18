// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

using Microsoft.Extensions.Logging;

/// <summary>Verifies that JSON session-directory diagnostics stay isolated from committed results and carry no protected content.</summary>
public sealed class SessionDirectoryObservabilityTests
{
    private static readonly ComponentId _audience = new("agentkit.session.directory.json");

    /// <summary>Verifies a throwing logger does not change the semantic outcome of a successful operation.</summary>
    [Fact]
    public async Task RecordAsync_WhenLoggerThrows_PreservesCommittedResult()
    {
        using var root = new TestDirectoryRoot();
        var logger = new RecordingLogger<JsonSessionDirectory> { ThrowOnWrite = true };
        var harness = Harness.Create(root, TimeProvider.System, logger);
        await harness.Directory.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Harness.Context(1);
        var write = new SessionDirectoryWriteRequest(context, Harness.Location(context), new IdempotencyKey("write-1"));

        var result = await harness.Directory.RecordAsync(
            await harness.AuthorizeAsync(write, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLocationRecorded>();
    }

    /// <summary>Verifies a successful operation emits exactly the documented event, activity, and metric with no protected content.</summary>
    [Fact]
    public async Task RecordAsync_WhenObserved_EmitsOnlyBoundedContentFreeDiagnostics()
    {
        const string protectedMarker = "never-export-this-directory-marker";
        using var root = new TestDirectoryRoot();
        var logger = new RecordingLogger<JsonSessionDirectory>();
        var harness = Harness.Create(root, TimeProvider.System, logger);
        await harness.Directory.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Harness.Context(2, protectedMarker);
        var write = new SessionDirectoryWriteRequest(context, Harness.Location(context), new IdempotencyKey("write-2"));
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionDirectoryOperation
                && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(context.AgentId.ToString()) == true);
        var measurements = new ConcurrentQueue<ImmutableArray<KeyValuePair<string, object?>>>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionDirectoryOperationCount)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Enqueue([.. tags]));
        meterListener.Start();

        _ = (await harness.Directory.RecordAsync(
            await harness.AuthorizeAsync(write, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("succeeded");
        foreach (var value in activity.Tags.Values)
        {
            string.Equals(value?.ToString(), protectedMarker, StringComparison.Ordinal).ShouldBeFalse();
        }

        var events = logger.Snapshot();
        events.ShouldContain(static item => item.EventId.Id == 19310);
        foreach (var item in events)
        {
            item.Message.Contains(protectedMarker, StringComparison.Ordinal).ShouldBeFalse();
        }

        measurements.Any(tags =>
            tags.Any(static tag => tag.Key == AgentKitTagNames.SessionOperation && tag.Value?.ToString() == "record")
            && tags.Any(static tag => tag.Key == AgentKitTagNames.Outcome && tag.Value?.ToString() == "succeeded")).ShouldBeTrue();
        foreach (var tags in measurements)
        {
            tags.All(static tag => tag.Key is AgentKitTagNames.SessionOperation or AgentKitTagNames.Outcome).ShouldBeTrue();
        }
    }

    /// <summary>Verifies a required-audit failure surfaces as a typed unavailable result and reports the failed event.</summary>
    [Fact]
    public async Task LocateAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailableAndLogsFailure()
    {
        using var root = new TestDirectoryRoot();
        var logger = new RecordingLogger<JsonSessionDirectory>();
        var harness = Harness.Create(root, TimeProvider.System, logger, acceptAudit: false);
        await harness.Directory.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Harness.Context(3);

        var result = await harness.Directory.LocateAsync(
            await harness.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDirectoryLookupUnavailable>();
        logger.Snapshot().ShouldContain(static item => item.EventId.Id == 19310);
    }

    /// <summary>Verifies an unexpected failure inside the observed operation propagates and reports the documented failed event.</summary>
    /// <remarks>
    /// Every directory core method is clock- and I/O-free, so the only way to observe the failure branch without bypassing
    /// authorization is a precondition failure raised inside the observed delegate itself, such as use after disposal.
    /// </remarks>
    [Fact]
    public async Task LocateAsync_WhenDirectoryIsDisposedAfterGrantIsIssued_PropagatesAndReportsFailed()
    {
        using var root = new TestDirectoryRoot();
        var logger = new RecordingLogger<JsonSessionDirectory>();
        var harness = Harness.Create(root, TimeProvider.System, logger);
        await harness.Directory.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Harness.Context(4);
        var authorized = await harness.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe);
        harness.Directory.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await harness.Directory.LocateAsync(authorized, TestContext.Current.CancellationToken));

        logger.Snapshot().ShouldContain(static item => item.EventId.Id == 19311);
    }

    /// <summary>Composes a standalone <see cref="JsonSessionDirectory"/> with independently controllable diagnostics and clock collaborators.</summary>
    private sealed class Harness: ISecurityAuditDispatcher, IIdentifierGenerator<SecurityAuditRecordId>
    {
        private readonly InMemorySecurityGrantStore _grants;
        private readonly bool _acceptAudit;
        private long _nextIdentity;

        private Harness(JsonSessionDirectory directory, InMemorySecurityGrantStore grants, bool acceptAudit)
        {
            Directory = directory;
            _grants = grants;
            _acceptAudit = acceptAudit;
        }

        public JsonSessionDirectory Directory { get; }

        public static Harness Create(
            TestDirectoryRoot root, TimeProvider timeProvider, ILogger<JsonSessionDirectory>? logger, bool acceptAudit = true)
        {
            var grants = new InMemorySecurityGrantStore(TimeProvider.System);
            var harness = new Harness(null!, grants, acceptAudit);
            var directory = root.Open(harness, grants, harness, timeProvider, logger: logger);
            return new Harness(directory, grants, acceptAudit);
        }

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SecurityAuditDispatchResult>(
                _acceptAudit ? new SecurityAuditAccepted() : new SecurityAuditFailed("audit unavailable for test"));
        }

        public SecurityAuditRecordId Create() => new(NextGuid());

        public static SessionOperationContext Context(int seed, string principalSuffix = "")
        {
            var agentId = Identifier<AgentId>(seed);
            var sessionId = Identifier<SessionId>(seed + 1);
            var identity = TestExecutionIdentity.Create(
                new TenantId("tenant-observability"), new PrincipalId($"owner{principalSuffix}"), ExecutionSubjectKind.Human);
            var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(seed + 2), null);
            return new SessionOperationContext(
                agentId, sessionId, null, correlation, identity, Authorization(agentId, sessionId, correlation, identity));
        }

        public static SessionLocation Location(SessionOperationContext context) => new(
            context.ToAddress(), context.Identity.TenantId, new SessionStoreKey("agentkit.json"),
            new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("1"));

        public async ValueTask<AuthorizedSessionDirectoryRequest<TRequest>> AuthorizeAsync<TRequest>(
            TRequest request, SecurityOperationKind kind, SecurityEffect effect)
            where TRequest : class
        {
            var context = request switch
            {
                SessionOperationContext value => value,
                SessionDirectoryWriteRequest value => value.Context,
                _ => throw new NotSupportedException(typeof(TRequest).Name),
            };
            var resource = SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress());
            var fingerprint = request switch
            {
                SessionOperationContext value => SessionDirectorySecurityBinding.LocateFingerprint(value),
                SessionDirectoryWriteRequest value => SessionDirectorySecurityBinding.RecordFingerprint(value),
                _ => throw new NotSupportedException(typeof(TRequest).Name),
            };
            var grant = new SecurityGrant(
                new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), context.Authorization.Scope,
                context.Identity, context.Authorization, _audience, kind, effect, [resource], fingerprint,
                new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), TimeProvider.System.GetUtcNow(),
                TimeProvider.System.GetUtcNow().AddDays(1), 1);
            await _grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
            return new AuthorizedSessionDirectoryRequest<TRequest>(
                request, grant, new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
        }

        private Guid NextGuid()
        {
            var value = Interlocked.Increment(ref _nextIdentity);
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, value);
            bytes[15] = 8;
            return new Guid(bytes);
        }

        private static T Identifier<T>(int value)
        {
            var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8);
            return typeof(T) switch
            {
                var t when t == typeof(AgentId) => (T) (object) new AgentId(guid),
                var t when t == typeof(SessionId) => (T) (object) new SessionId(guid),
                var t when t == typeof(OperationId) => (T) (object) new OperationId(guid),
                var t when t == typeof(SecurityPolicySnapshotId) => (T) (object) new SecurityPolicySnapshotId(guid),
                _ => throw new NotSupportedException(typeof(T).Name),
            };
        }

        private static SecurityAuthorizationContext Authorization(
            AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
            new(new SecurityProfileKey("observability"), new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(50),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:observability-directory-policy")),
                new ComponentKey<ISecurityAuthority>("observability"), new AgentDefinitionRevision(1),
                new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    }
}
