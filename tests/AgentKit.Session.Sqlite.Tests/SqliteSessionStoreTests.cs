// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Text.Json;

using AgentKit.Conformance;
using AgentKit.Session;
using AgentKit.Session.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Runs the reusable protected session-store contract against durable SQLite.</summary>
public sealed class SqliteSessionStoreTests: SessionStoreConformanceTests<SqliteSessionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteSessionStoreConformanceFixture CreateFixture() => new();

    // ---- Probing tests beyond the shared suite. ----

    [Fact]
    public async Task AppendAsync_WhenStoreIsReopened_ReturnsCommittedEntriesAndVersion()
    {
        // sessions-persistence-and-branching.md: SQLite "additionally proves committed state survives close and reopen".
        using var directory = new TempDirectory();
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        SessionDescriptor descriptor;
        MessageSessionEntry entry;
        SessionOperationContext context;

        await using (var first = Harness.Open(directory.Path, instance))
        {
            descriptor = await first.CreateSessionAsync();
            context = Harness.SessionContext(descriptor.Address, 20);
            entry = Harness.MessageEntry(descriptor, 30, 1, "durable");
            var appended = await first.Store.AppendAsync(
                await first.AuthorizeAsync(
                    new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("a1"), [entry]),
                    SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken);
            _ = appended.ShouldBeOfType<SessionAppended>();
        }

        await using var second = Harness.Open(directory.Path, instance);
        var loaded = await second.Store.LoadAsync(
            await second.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = await second.Store.ReadAsync(
            await second.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        page.ShouldBeOfType<SessionPage>().Entries.ShouldBe([entry]);
    }

    [Fact]
    public async Task AppendAsync_WhenEquivalentRequestWithToolCallPartReplays_ReturnsOriginalReceipt()
    {
        // Idempotent replay must compare canonical content; a JsonElement-bearing part must not defeat replay equality.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var entry = Harness.AssistantToolCallEntry(descriptor, 30, 1);
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("tool-call"), [entry]);

        var first = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var replay = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = first.ShouldBeOfType<SessionAppended>((first as SessionAppendFailed)?.SafeMessage);
        replay.ShouldBeOfType<SessionAppended>((replay as SessionAppendFailed)?.SafeMessage).NewVersion.ShouldBe(((SessionAppended) first).NewVersion);
    }

    [Fact]
    public async Task AppendAsync_WhenEquivalentToolCallRequestReplaysAfterReopen_ReturnsOriginalReceipt()
    {
        using var directory = new TempDirectory();
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        SessionDescriptor descriptor;
        SessionAppendRequest request;
        SessionAppended first;

        await using (var opened = Harness.Open(directory.Path, instance))
        {
            descriptor = await opened.CreateSessionAsync();
            var context = Harness.SessionContext(descriptor.Address, 20);
            request = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("tool-call"),
                [Harness.AssistantToolCallEntry(descriptor, 30, 1)]);
            first = (await opened.Store.AppendAsync(
                await opened.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        }

        await using var reopened = Harness.Open(directory.Path, instance);
        var replay = await reopened.Store.AppendAsync(
            await reopened.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        replay.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(first.NewVersion);
    }

    [Fact]
    public async Task AppendAsync_WhenBatchCommitsMultipleEntries_NextSingleAppendRequiresNextSequenceNotVersionPlusOne()
    {
        // Documents the store rule callers must follow: sequence continuity is whole-session, not Version + 1.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var batch = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("batch"),
            [Harness.MessageEntry(descriptor, 30, 1, "a"), Harness.MessageEntry(descriptor, 32, 2, "b")]);
        var batched = (await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(batch, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();

        // A caller that follows the "Version + 1" convention used by Plan/Compaction/loop-rebase computes sequence 2.
        var versionDerived = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, batched.NewVersion, new IdempotencyKey("version-derived"),
            [Harness.MessageEntry(descriptor, 34, batched.NewVersion.Value + 1, "c")]);
        var result = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(versionDerived, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        // Both first-party stores reject it; this pins the contract so caller bugs are visible.
        batched.NewVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
        _ = result.ShouldBeOfType<SessionAppendFailed>();
    }

    [Fact]
    public async Task AppendAsync_WhenEntryHasNoCodec_ReturnsTypedFailureWithoutMutation()
    {
        // An entry without a durable codec must be rejected as a typed store outcome before any state changes.
        using var directory = new TempDirectory();
        await using var harness = Harness.Open(directory.Path, new SqliteSessionStoreInstanceId(Guid.NewGuid()));
        var descriptor = await harness.CreateSessionAsync();
        var context = Harness.SessionContext(descriptor.Address, 20);
        var uncoded = new UncodedSessionEntry(
            new SessionEntryId(new Guid(99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), descriptor.Address,
            new InRunOperationCorrelation(new OperationId(new Guid(98, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), new RunId(new Guid(97, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), null),
            descriptor.ActiveBranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"));
        var request = new SessionAppendRequest(context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("uncoded"), [uncoded]);

        var result = await harness.Store.AppendAsync(
            await harness.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var loaded = await harness.Store.LoadAsync(
            await harness.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = await harness.Store.ReadAsync(
            await harness.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldContain("durable codec");
        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(descriptor.Version);
        page.ShouldBeOfType<SessionPage>().Entries.ShouldBeEmpty();
    }

    [Fact]
    public void AddSqliteSessionStore_WhenCalledTwice_DoesNotRegisterDuplicateStore()
    {
        // Repeating the SQLite registration is idempotent for the single "agentkit.sqlite" store key.
        var target = new SqliteSessionStoreTarget(
            Path.Combine(Path.GetTempPath(), $"agentkit-{Guid.NewGuid():N}", "s.db"),
            new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations);
        var services = new ServiceCollection();

        _ = services.AddSqliteSessionStore(target);
        _ = services.AddSqliteSessionStore(target);

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionStore)).ShouldBe(1);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenInMemoryStoreAlreadyRegistered_RegistersBothStores()
    {
        // Store registrations are additive: registration order never selects a store, the directory route does.
        using var directory = new TempDirectory();
        var target = new SqliteSessionStoreTarget(
            Path.Combine(directory.Path, "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(timeProvider);
        _ = services.AddSingleton<ISecurityGrantStore>(new InMemorySecurityGrantStore(timeProvider));
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());

        _ = services.AddInMemorySessionStore();
        _ = services.AddSqliteSessionStore(target);
        _ = services.AddInMemorySessionStore();
        _ = services.AddSqliteSessionStore(target);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var stores = provider.GetServices<ISessionStore>().ToArray();
        stores.Select(static store => store.GetType()).ShouldBe([typeof(InMemorySessionStore), typeof(SqliteSessionStore)]);
        new DefaultSessionStoreCatalog(stores).GetDescriptors().Select(static descriptor => descriptor.Key.Value)
            .ShouldBe(["agentkit.in-memory", "agentkit.sqlite"]);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenRegisteredBeforeInMemoryStore_RegistersBothStores()
    {
        using var directory = new TempDirectory();
        var target = new SqliteSessionStoreTarget(
            Path.Combine(directory.Path, "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var services = new ServiceCollection();

        _ = services.AddSqliteSessionStore(target);
        _ = services.AddInMemorySessionStore();

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionStore)).ShouldBe(2);
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <summary>A session entry kind that no registered codec can encode.</summary>
    private sealed record UncodedSessionEntry(
        SessionEntryId Id, SessionAddress Address, OperationCorrelation Correlation, BranchId BranchId,
        SessionSequence Sequence, SessionEntryId? CausalParentId, DateTimeOffset RecordedAt, SchemaVersion SchemaVersion)
        : SessionEntry(Id, Address, Correlation, BranchId, Sequence, CausalParentId, RecordedAt, SchemaVersion);

    private sealed class TempDirectory: IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentkit-session-{Guid.NewGuid():N}");
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

    private sealed class Harness: ISecurityAuditDispatcher, IAsyncDisposable
    {
        private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        private readonly ServiceProvider _services;
        private readonly InMemorySecurityGrantStore _grants;
        private long _nextIdentity;

        private Harness(string directory, SqliteSessionStoreInstanceId instance)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<TimeProvider>(_timeProvider);
            _grants = new InMemorySecurityGrantStore(_timeProvider);
            _ = services.AddSingleton<ISecurityGrantStore>(_grants);
            _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
            _ = services.AddSqliteSessionStore(new SqliteSessionStoreTarget(
                Path.Combine(directory, "sessions.db"), instance,
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));
            _services = services.BuildServiceProvider();
            Store = (SqliteSessionStore) _services.GetRequiredService<ISessionStore>();
        }

        public SqliteSessionStore Store { get; }

        public static Harness Open(string directory, SqliteSessionStoreInstanceId instance) => new(directory, instance);

        public async ValueTask<SessionDescriptor> CreateSessionAsync()
        {
            var request = CreateStoreRequest();
            var result = await Store.CreateAsync(
                await AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
                TestContext.Current.CancellationToken);
            return result.ShouldBeOfType<SessionCreated>().Descriptor;
        }

        public async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
            TRequest request, SecurityOperationKind kind, SecurityEffect effect)
            where TRequest : class
        {
            var context = RequestContext(request);
            var resource = SessionStoreSecurityBinding.Resource(Store.Descriptor.Key, context.ToAddress());
            var grant = new SecurityGrant(
                new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), context.Authorization.Scope,
                context.Identity, context.Authorization, Store.SecurityAudience, kind, effect, [resource],
                RequestFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
                _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddDays(1), 1);
            await _grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
            return new AuthorizedSessionStoreRequest<TRequest>(
                request, Store.Descriptor.Key, grant,
                new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
        }

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());

        public ValueTask DisposeAsync()
        {
            _services.Dispose();
            return ValueTask.CompletedTask;
        }

        public static SessionOperationContext SessionContext(SessionAddress address, int offset)
        {
            var identity = Identity();
            var correlation = Correlation(offset);
            return new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
        }

        public static MessageSessionEntry MessageEntry(SessionDescriptor descriptor, int offset, long sequence, string text)
        {
            var correlation = Correlation(offset);
            var message = new UserMessage(
                Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
                descriptor.Address.SessionId, descriptor.ConversationId, descriptor.ActiveBranchId,
                correlation.RunId, null, Timestamp(offset), MessageState.Complete,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
            return new MessageSessionEntry(
                Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation,
                descriptor.ActiveBranchId, new SessionSequence(sequence), null, Timestamp(offset),
                new SchemaVersion("1"), message);
        }

        public static MessageSessionEntry AssistantToolCallEntry(SessionDescriptor descriptor, int offset, long sequence)
        {
            var correlation = Correlation(offset);
            using var arguments = JsonDocument.Parse("""{"path":"README.md","limit":10}""");
            var toolCall = new ToolCallPart(
                Identifier<ToolCallId>(offset + 5),
                new ToolReference(new ToolId("read"), null, "read"),
                arguments.RootElement.Clone(),
                new ProviderToolCallId("call_1"),
                ExtensionData.Empty);
            var message = new AssistantMessage(
                Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
                descriptor.Address.SessionId, descriptor.ConversationId, descriptor.ActiveBranchId,
                correlation.RunId, null, Timestamp(offset), MessageState.Complete,
                [toolCall],
                new AssistantResponseMetadata(
                    Identifier<ModelRequestId>(offset + 6),
                    new ProviderResponseIdentity(
                        new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("m"), new ModelId("m"), null, null, null),
                    NormalizedStopReason.ToolUse, null, ModelUsage.NotReported, ExtensionData.Empty),
                ExtensionData.Empty);
            return new MessageSessionEntry(
                Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation,
                descriptor.ActiveBranchId, new SessionSequence(sequence), null, Timestamp(offset),
                new SchemaVersion("1"), message);
        }

        private static SessionStoreCreateRequest CreateStoreRequest()
        {
            var agentId = Identifier<AgentId>(1);
            var identity = Identity();
            var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
            var authorization = Authorization(agentId, null, correlation, identity);
            var logical = new SessionCreateRequest(
                agentId, identity, authorization, Identifier<ConversationId>(3), new IdempotencyKey("create"), ExtensionData.Empty);
            var address = new SessionAddress(agentId, Identifier<SessionId>(4));
            var context = new SessionOperationContext(
                address.AgentId, address.SessionId, null, correlation, identity,
                Authorization(address.AgentId, address.SessionId, correlation, identity));
            return new SessionStoreCreateRequest(logical, address, context);
        }

        private static SecurityAuthorizationContext Authorization(
            AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
            new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
                new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
                new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

        private static ExecutionIdentity Identity() =>
            TestExecutionIdentity.Create(new TenantId("tenant-owner"), new PrincipalId("owner"), ExecutionSubjectKind.Human);

        private static InRunOperationCorrelation Correlation(int offset) =>
            new(Identifier<OperationId>(offset), Identifier<RunId>(offset + 1), null);

        private static DateTimeOffset Timestamp(int offset) => DateTimeOffset.UnixEpoch.AddSeconds(offset + 1);

        private static T Identifier<T>(int value)
        {
            var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
            return typeof(T) switch
            {
                var t when t == typeof(AgentId) => (T) (object) new AgentId(guid),
                var t when t == typeof(SessionId) => (T) (object) new SessionId(guid),
                var t when t == typeof(ConversationId) => (T) (object) new ConversationId(guid),
                var t when t == typeof(OperationId) => (T) (object) new OperationId(guid),
                var t when t == typeof(RunId) => (T) (object) new RunId(guid),
                var t when t == typeof(MessageId) => (T) (object) new MessageId(guid),
                var t when t == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
                var t when t == typeof(ToolCallId) => (T) (object) new ToolCallId(guid),
                var t when t == typeof(ModelRequestId) => (T) (object) new ModelRequestId(guid),
                var t when t == typeof(SecurityPolicySnapshotId) => (T) (object) new SecurityPolicySnapshotId(guid),
                _ => throw new NotSupportedException(typeof(T).Name),
            };
        }

        private Guid NextGuid()
        {
            var value = Interlocked.Increment(ref _nextIdentity);
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, value);
            bytes[15] = 7;
            return new Guid(bytes);
        }

        private static SessionOperationContext RequestContext<TRequest>(TRequest request)
            where TRequest : class => request switch
            {
                SessionStoreCreateRequest value => value.Context,
                SessionOperationContext value => value,
                SessionAppendRequest value => value.Context,
                SessionReadRequest value => value.Context,
                _ => throw new InvalidOperationException($"Unsupported request {typeof(TRequest).FullName}."),
            };

        private static InputFingerprint RequestFingerprint<TRequest>(TRequest request)
            where TRequest : class => request switch
            {
                SessionStoreCreateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
                SessionOperationContext value => SessionStoreSecurityBinding.Fingerprint(value),
                SessionAppendRequest value => SessionStoreSecurityBinding.Fingerprint(value),
                SessionReadRequest value => SessionStoreSecurityBinding.Fingerprint(value),
                _ => throw new InvalidOperationException($"Unsupported request {typeof(TRequest).FullName}."),
            };
    }
}
