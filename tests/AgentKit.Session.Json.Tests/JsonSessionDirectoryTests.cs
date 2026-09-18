// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the durable JSON session directory's routing, tenant masking, discovery, and replay behavior.</summary>
/// <remarks>
/// The fixture supplies the directory's real enforcement dependencies: an in-memory grant store that produces authoritative
/// intent receipts and an accepting audit dispatcher. It also generates deterministic audit-record identities so a case
/// never depends on a random source.
/// </remarks>
public sealed class JsonSessionDirectoryTests: ISecurityAuditDispatcher, IIdentifierGenerator<SecurityAuditRecordId>
{
    private static readonly ComponentId _audience = new("agentkit.session.directory.json");
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemorySecurityGrantStore _grants;
    private long _nextIdentity;

    /// <summary>Initializes one case's isolated grant store bound to the deterministic test clock.</summary>
    public JsonSessionDirectoryTests() => _grants = new InMemorySecurityGrantStore(_timeProvider);

    /// <inheritdoc/>
    /// <remarks>Required audit always succeeds here; these cases exercise routing semantics rather than audit outages.</remarks>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <inheritdoc/>
    public SecurityAuditRecordId Create() => new(NextGuid());

    /// <summary>Verifies a committed route is still authoritative after the directory is closed and replayed.</summary>
    [Fact]
    public async Task RecordAsync_WhenDirectoryIsReopened_LocateReturnsThePersistedRoute()
    {
        using var root = new TestDirectoryRoot();
        var context = Context(Identifier<SessionId>(10));
        var location = Location(context, "agentkit.json");
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record"));
        SessionDirectoryWriteResult recorded;
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            recorded = await first.RecordAsync(
                await AuthorizeAsync(write, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
                TestContext.Current.CancellationToken);
        }

        using var reopened = root.Open(this, _grants, this, _timeProvider);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var located = await reopened.LocateAsync(
            await AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var replay = await reopened.RecordAsync(
            await AuthorizeAsync(write, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        recorded.ShouldBeOfType<SessionLocationRecorded>().Existing.ShouldBeFalse();
        located.ShouldBe(new SessionLocated(location));
        replay.ShouldBeOfType<SessionLocationRecorded>().Existing.ShouldBeTrue();
    }

    /// <summary>Verifies a creation retry issued after a restart reconciles instead of allocating a second route.</summary>
    [Fact]
    public async Task RecordCreateAsync_WhenRetriedAfterReopen_ReconcilesToTheOriginalRoute()
    {
        using var root = new TestDirectoryRoot();
        var context = Context(Identifier<SessionId>(20));
        var create = new SessionCreateRequest(
            context.AgentId, context.Identity,
            Authorization(context.AgentId, null, context.Correlation, context.Identity),
            Identifier<ConversationId>(21), new IdempotencyKey("create"), ExtensionData.Empty);
        var record = new SessionDirectoryCreateRecordRequest(create, Location(context, "agentkit.json"));
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            _ = (await first.RecordCreateAsync(
                await AuthorizeAsync(record, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        }

        using var reopened = root.Open(this, _grants, this, _timeProvider);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var retried = await reopened.RecordCreateAsync(
            await AuthorizeAsync(record, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var lookup = await reopened.LocateForCreateAsync(
            await AuthorizeAsync(create, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        retried.ShouldBeOfType<SessionLocationRecorded>().Existing.ShouldBeTrue();
        lookup.ShouldBe(new SessionCreationLocationLocated(record.Location));
    }

    /// <summary>Verifies a fully authorized foreign tenant observes absence rather than another tenant's route.</summary>
    [Fact]
    public async Task LocateAsync_WhenTenantDiffers_MasksExistence()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(30));
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.json"), new IdempotencyKey("own")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        var foreign = Context(owner.SessionId, "tenant-foreign", "foreign-user");

        var located = await directory.LocateAsync(
            await AuthorizeAsync(foreign, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        located.ShouldBe(new SessionLocationNotFound(foreign.ToAddress()));
    }

    /// <summary>Verifies discovery returns one bounded page ordered by session identity with an exact continuation cursor.</summary>
    [Fact]
    public async Task ListAsync_WhenMoreRoutesExistThanRequested_ReturnsBoundedOrderedPage()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var recorded = new List<SessionLocation>();
        for (var index = 0; index < 3; index++)
        {
            var context = Context(Identifier<SessionId>(40 + index));
            var location = Location(context, "agentkit.json");
            recorded.Add(location);
            _ = (await directory.RecordAsync(
                await AuthorizeAsync(
                    new SessionDirectoryWriteRequest(context, location, new IdempotencyKey($"route-{index}")),
                    SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        }

        var identity = Identity();
        var scan = new SessionDirectoryListRequest(
            Identifier<AgentId>(1), identity,
            Authorization(Identifier<AgentId>(1), null, Correlation(50), identity), null, 2);
        var page = await directory.ListAsync(
            await AuthorizeAsync(scan, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        var listed = page.ShouldBeOfType<SessionDirectoryPage>();
        listed.Locations.Length.ShouldBe(2);
        listed.Locations.Select(static location => location.Address.SessionId)
            .ShouldBe(recorded.Select(static location => location.Address.SessionId)
                .OrderBy(static id => id.Value).Take(2));
        listed.NextCursor.ShouldBe(listed.Locations[^1].Address.SessionId);
    }

    /// <summary>Verifies an uninitialized directory refuses protected access instead of serving an empty projection.</summary>
    [Fact]
    public async Task LocateAsync_WhenDirectoryWasNotInitialized_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        var context = Context(Identifier<SessionId>(60));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await directory.LocateAsync(
                await AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
                TestContext.Current.CancellationToken));
    }

    private async ValueTask<AuthorizedSessionDirectoryRequest<TRequest>> AuthorizeAsync<TRequest>(
        TRequest request, SecurityOperationKind kind, SecurityEffect effect)
        where TRequest : class
    {
        var (authorization, resource, fingerprint) = Binding(request);
        var grant = new SecurityGrant(
            new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), authorization.Scope, authorization.Identity,
            authorization, _audience, kind, effect, [resource], fingerprint, new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddDays(1), 1);
        await _grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
        return new AuthorizedSessionDirectoryRequest<TRequest>(
            request, grant, new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
    }

    private static (SecurityAuthorizationContext Authorization, ProtectedResource Resource, InputFingerprint Fingerprint)
        Binding<TRequest>(TRequest request)
        where TRequest : class => request switch
        {
            SessionOperationContext value => (
                value.Authorization,
                SessionDirectorySecurityBinding.Resource(value.Identity.TenantId, value.ToAddress()),
                SessionDirectorySecurityBinding.LocateFingerprint(value)),
            SessionCreateRequest value => (
                value.Authorization,
                SessionDirectorySecurityBinding.CreationResource(
                    value.Identity.TenantId, value.AgentId, value.IdempotencyKey),
                SessionDirectorySecurityBinding.LocateForCreateFingerprint(value)),
            SessionDirectoryWriteRequest value => (
                value.Context.Authorization,
                SessionDirectorySecurityBinding.Resource(
                    value.Context.Identity.TenantId, value.Location.Address),
                SessionDirectorySecurityBinding.RecordFingerprint(value)),
            SessionDirectoryCreateRecordRequest value => (
                value.Request.Authorization,
                SessionDirectorySecurityBinding.CreationResource(
                    value.Request.Identity.TenantId, value.Request.AgentId, value.Request.IdempotencyKey),
                SessionDirectorySecurityBinding.RecordCreateFingerprint(value)),
            SessionDirectoryListRequest value => (
                value.Authorization,
                SessionDirectorySecurityBinding.ListResource(value.Identity.TenantId, value.AgentId),
                SessionDirectorySecurityBinding.ListFingerprint(value)),
            _ => throw new InvalidOperationException($"Unsupported directory request {typeof(TRequest).FullName}."),
        };

    private SessionLocation Location(SessionOperationContext context, string storeKey) => new(
        context.ToAddress(), context.Identity.TenantId, new SessionStoreKey(storeKey),
        new SessionDirectoryRevision(1), _timeProvider.GetUtcNow(), new SchemaVersion("1"));

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 2;
        return new Guid(bytes);
    }

    private static SessionOperationContext Context(
        SessionId sessionId, string tenant = "tenant-owner", string principal = "owner")
    {
        var agentId = Identifier<AgentId>(1);
        var identity = Identity(tenant, principal);
        var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
        return new SessionOperationContext(
            agentId, sessionId, null, correlation, identity,
            Authorization(agentId, sessionId, correlation, identity));
    }

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
        new(new SecurityProfileKey("directory"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                new SecurityPolicyVersion(1), new ContentHash("sha256:directory-policy")),
            new ComponentKey<ISecurityAuthority>("directory"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private static ExecutionIdentity Identity(string tenant = "tenant-owner", string principal = "owner") =>
        TestExecutionIdentity.Create(
            new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    private static BeforeRunOperationCorrelation Correlation(int offset) =>
        new(Identifier<OperationId>(offset), null);

    private static T Identifier<T>(int value)
    {
        var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2);
        return typeof(T) switch
        {
            var type when type == typeof(AgentId) => (T) (object) new AgentId(guid),
            var type when type == typeof(SessionId) => (T) (object) new SessionId(guid),
            var type when type == typeof(ConversationId) => (T) (object) new ConversationId(guid),
            var type when type == typeof(OperationId) => (T) (object) new OperationId(guid),
            var type when type == typeof(SecurityPolicySnapshotId) =>
                (T) (object) new SecurityPolicySnapshotId(guid),
            _ => throw new InvalidOperationException($"Unsupported identifier type {typeof(T).FullName}."),
        };
    }
}
