// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using AgentKit.Session.InMemory;

public sealed class InMemorySessionDirectoryTests
{
    private static readonly ComponentId _audience = new("agentkit.session.directory.in-memory");

    [Fact]
    public async Task LocateAsync_WhenRequiredAuditIsUnavailable_DoesNotConsumeGrantOrReadRoute()
    {
        var audits = new RecordingAuditDispatcher(new SecurityAuditUnavailable("audit unavailable"));
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(audits, grants);
        var context = Context();

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupUnavailable("Required audit delivery is unavailable for the directory operation."));
        grants.Enforcements.ShouldBeEmpty();
        _ = audits.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RecordCreateAsync_WhenReplayHasEquivalentOriginalRequest_ReturnsWinningRouteWithoutRebinding()
    {
        var audits = new RecordingAuditDispatcher(new SecurityAuditAccepted());
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(audits, grants);
        var original = CreateRequest("retry", extensions: Extensions());
        var reconstructed = CreateRequest("retry", extensions: Extensions());
        var winner = Location("22222222-2222-2222-2222-222222222222", "store-a");
        var candidate = Location("33333333-3333-3333-3333-333333333333", "store-b");

        var first = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(original, winner),
                Grant(original, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var replay = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(reconstructed, candidate),
                Grant(reconstructed, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        first.ShouldBe(new SessionLocationRecorded(winner, existing: false));
        replay.ShouldBe(new SessionLocationRecorded(winner, existing: true));
        grants.Enforcements.Count.ShouldBe(2);
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenRetryEvidenceDiffers_ReturnsTypedConflictBeforeAllocation()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var original = CreateRequest("retry");
        var location = Location("22222222-2222-2222-2222-222222222222", "store-a");
        _ = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(original, location),
                Grant(original, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var changed = CreateRequest("retry", new ConversationId(Guid.Parse("44444444-4444-4444-4444-444444444444")));

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                changed,
                Grant(changed, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionCreationLocationConflict("The creation retry key was already used with different request evidence."));
    }

    [Fact]
    public async Task LocateAsync_WhenRouteBelongsToAnotherTenant_ReturnsTenantMaskedMissingResult()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var owner = Context("tenant-a");
        var location = Location(owner.SessionId.ToString(), "store-a", owner.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(owner, location, new IdempotencyKey("record")),
                Grant(owner, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var other = Context("tenant-b", owner.SessionId);

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                other,
                Grant(other, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocationNotFound(other.ToAddress()));
    }

    [Fact]
    public async Task LocateAsync_WhenGrantStoreReturnsWrongReceipt_DeniesBeforeReadingExistingRoute()
    {
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        grants.ReturnWrongReceipt = true;

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task LocateAsync_WhenCancelledAfterConsumption_ThrowsBeforeReadingExistingRoute()
    {
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        grants.AfterConsume = cancellation.Cancel;

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await directory.LocateAsync(
                new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                    context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task ListAsync_WhenRoutesSpanAgentsAndTenants_ReturnsOnlyVisibleOrderedPage()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var first = CreateRequest("first");
        var second = CreateRequest("second");
        var firstLocation = Location("22222222-2222-2222-2222-222222222222", "store-a");
        var secondLocation = Location("33333333-3333-3333-3333-333333333333", "store-a");
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(first, secondLocation),
            Grant(first, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(second, firstLocation),
            Grant(second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);
        var request = ListRequest();

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                request, Grant(request), Intent()), TestContext.Current.CancellationToken);

        var page = result.ShouldBeOfType<SessionDirectoryPage>();
        page.Locations.Select(static location => location.Address.SessionId)
            .ShouldBe([firstLocation.Address.SessionId]);
        page.NextCursor.ShouldBe(firstLocation.Address.SessionId);
    }

    [Fact]
    public async Task ListAsync_WhenObserved_EmitsDirectoryActivityAndLog()
    {
        var logger = new TestSupport.RecordingLogger<InMemorySessionDirectory>();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(), logger);
        var request = ListRequest();
        using var activities = new TestSupport.ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionDirectoryOperation
                && activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals("list") == true
                && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(request.AgentId.ToString()) == true);

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                request, Grant(request), Intent()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDirectoryPage>();
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("success");
        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(16002);
        completed.State["Operation"].ShouldBe("list");
        completed.State["Outcome"].ShouldBe("success");
    }

    [Fact]
    public async Task ListAsync_WhenCancelledAfterConsumption_ThrowsBeforeReadingRoutes()
    {
        var grants = new RecordingGrantStore();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var request = ListRequest();
        using var cancellation = new CancellationTokenSource();
        grants.AfterConsume = cancellation.Cancel;

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await directory.ListAsync(
                new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                    request, Grant(request), Intent()), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    private static InMemorySessionDirectory CreateDirectory(
        RecordingAuditDispatcher audits,
        RecordingGrantStore grants,
        Microsoft.Extensions.Logging.ILogger<InMemorySessionDirectory>? logger = null) => new(
        _audience,
        audits,
        grants,
        new SequenceAuditRecordIds(),
        TimeProvider.System,
        logger);

    private static SessionOperationContext Context(string tenant = "tenant", SessionId? sessionId = null)
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var id = sessionId ?? new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SessionOperationContext(agentId, id, null, correlation, identity, Authorization(agentId, id, correlation, identity));
    }

    private static SessionCreateRequest CreateRequest(
        string idempotencyKey,
        ConversationId? conversationId = null,
        ExtensionData? extensions = null)
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SessionCreateRequest(agentId, identity, Authorization(agentId, null, correlation, identity), conversationId,
            new IdempotencyKey(idempotencyKey), extensions ?? ExtensionData.Empty);
    }

    private static SessionDirectoryListRequest ListRequest()
    {
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(GuidOperation(), null);
        return new SessionDirectoryListRequest(
            agentId, identity, Authorization(agentId, null, correlation, identity), null, 1);
    }

    private static OperationId GuidOperation() =>
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private static ExtensionData Extensions() => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add("extension", new ExtensionValue([1, 2, 3])));

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
        new SecurityProfileKey("security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"),
        new AgentDefinitionRevision(1),
        new ConfigurationVersion(1),
        new SecurityAuthorizationScope(agentId, sessionId, correlation),
        identity);

    private static SessionLocation Location(string sessionId, string storeKey, TenantId? tenantId = null) => new(
        new SessionAddress(
            new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse(sessionId))),
        tenantId ?? new TenantId("tenant"),
        new SessionStoreKey(storeKey),
        new SessionDirectoryRevision(1),
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("v1"));

    private static SecurityGrant Grant(SessionOperationContext context, SecurityOperationKind kind, SecurityEffect effect) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), context.Authorization.Scope, context.Identity, _audience,
        kind, effect, [SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress())],
        SessionDirectorySecurityBinding.LocateFingerprint(context), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, 1);

    private static SecurityGrant Grant(SessionCreateRequest request, SecurityOperationKind kind, SecurityEffect effect) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), request.Authorization.Scope, request.Identity, _audience,
        kind, effect, [SessionDirectorySecurityBinding.CreationResource(request.Identity.TenantId, request.AgentId, request.IdempotencyKey)],
        SessionDirectorySecurityBinding.LocateForCreateFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, 1);

    private static SecurityGrant Grant(SessionDirectoryListRequest request) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), request.Authorization.Scope, request.Identity, _audience,
        SecurityOperationKind.StateRead, SecurityEffect.Observe,
        [SessionDirectorySecurityBinding.ListResource(request.Identity.TenantId, request.AgentId)],
        SessionDirectorySecurityBinding.ListFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, 1);

    private static SecurityEnforcementIntent Intent() => new(
        new SecurityEnforcementIntentId(Guid.NewGuid()),
        requiredFence: null);

    private sealed class RecordingAuditDispatcher(SecurityAuditDispatchResult result): ISecurityAuditDispatcher
    {
        public List<SecurityAuditRecord> Records { get; } = [];

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingGrantStore: ISecurityGrantStore
    {
        public List<SecurityEnforcementRequest> Enforcements { get; } = [];
        public bool ReturnWrongReceipt { get; set; }
        public Action? AfterConsume { get; set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default)
        {
            Enforcements.Add(enforcement);
            return ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed."));
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default)
        {
            Enforcements.Add(enforcement);
            var receipt = new SecurityEnforcementIntentReceipt(intent.Id, grant.Id, grant.RequestId, enforcement,
                intent.RequiredFence, ReturnWrongReceipt
                    ? new ContentHash("sha256:wrong")
                    : SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch);
            AfterConsume?.Invoke();
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed, 0, "Consumed.", receipt));
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class SequenceAuditRecordIds: IIdentifierGenerator<SecurityAuditRecordId>
    {
        private int _next;

        public SecurityAuditRecordId Create() => new(new Guid(++_next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
    }
}
