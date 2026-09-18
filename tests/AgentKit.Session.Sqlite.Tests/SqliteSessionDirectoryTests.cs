// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Session.Sqlite;

public sealed class SqliteSessionDirectoryTests
{
    private static readonly ComponentId _audience = new("agentkit.session.directory.in-memory");

    [Fact]
    public async Task LocateAsync_WhenDirectoryIsReopened_ReturnsPersistedRoute()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-reopen-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var target = new SqliteSessionStoreTarget(Path.Combine(path, "sessions.db"),
            new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations);
        var audits = new RecordingAuditDispatcher(new SecurityAuditAccepted());
        var grants = new RecordingGrantStore();
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var first = new SqliteSessionDirectory(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            target, SqliteSessionStoreSettings.CreateDefault());
        _ = await first.RecordAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
            new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
            Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var reopened = new SqliteSessionDirectory(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            target, SqliteSessionStoreSettings.CreateDefault());

        var result = await reopened.LocateAsync(new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
            context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocated(location));
    }

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
    public async Task LocateAsync_WhenAuditDispatcherThrowsUnexpectedException_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new ThrowingAuditDispatcher(), new RecordingGrantStore());
        var context = Context();

        var result = await directory.LocateAsync(
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryLookupUnavailable("The directory authorization prerequisite is unavailable."));
    }

    [Fact]
    public async Task LocateAsync_WhenDatabaseFileIsMissingAfterAuthorization_PropagatesAndReportsFaulted()
    {
        // Authorization succeeds first (it is fully isolated from the database), so the missing file can
        // only be discovered by the real SQLite read that follows, exercising the unexpected-fault path
        // that AuthorizeAsync's own catch-all cannot reach.
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-missing-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        var logger = new RecordingLogger<SqliteSessionDirectory>();
        var directory = new SqliteSessionDirectory(_audience, new RecordingAuditDispatcher(new SecurityAuditAccepted()),
            new RecordingGrantStore(), new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault(), logger);
        var context = Context();
        File.Delete(databasePath);

        _ = await Should.ThrowAsync<Exception>(async () =>
            await directory.LocateAsync(
                new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                    context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
                TestContext.Current.CancellationToken));

        var faulted = logger.Snapshot().ShouldHaveSingleItem();
        faulted.EventId.Id.ShouldBe(25003);
        faulted.State["Operation"].ShouldBe("locate");
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var create = CreateRequest("locate-for-create-unavailable");

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                create, Grant(create, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryCreationLookupUnavailable(
            "Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task LocateForCreateAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var create = CreateRequest("locate-for-create-denied");

        var result = await directory.LocateForCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionCreateRequest>(
                create, Grant(create, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryCreationLookupDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task RecordAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-unavailable"));

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task RecordAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-denied"));

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task RecordAsync_WhenRetriedWithSameIdempotencyKeyAndEvidence_ReturnsExistingLocation()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var write = new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-replay"));

        var first = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var second = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                write, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        first.ShouldBe(new SessionLocationRecorded(location, existing: false));
        second.ShouldBe(new SessionLocationRecorded(location, existing: true));
    }

    [Fact]
    public async Task RecordAsync_WhenRetriedWithSameKeyAndDifferentStoreKey_ReturnsConflict()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var first = new SessionDirectoryWriteRequest(
            context, Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId), new IdempotencyKey("record-diff"));
        var second = new SessionDirectoryWriteRequest(
            context, Location(context.SessionId.ToString(), "store-b", context.Identity.TenantId), new IdempotencyKey("record-diff"));

        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                first, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                second, Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionLocationConflict>();
        conflict.Existing.StoreKey.ShouldBe(new SessionStoreKey("store-a"));
        conflict.RequestedStoreKey.ShouldBe(new SessionStoreKey("store-b"));
    }

    [Fact]
    public async Task RecordAsync_WhenExistingLocationBelongsToAnotherOwner_ReturnsDenied()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var owner = Context();
        var location = Location(owner.SessionId.ToString(), "store-a", owner.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(owner, location, new IdempotencyKey("record-owner-1")),
                Grant(owner, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        // Same tenant, different principal, and a fresh idempotency key so the write-route lookup misses.
        var otherIdentity = TestExecutionIdentity.Create(owner.Identity.TenantId, new PrincipalId("someone-else"), ExecutionSubjectKind.Human);
        var otherPrincipal = new SessionOperationContext(
            owner.AgentId, owner.SessionId, null, owner.Correlation, otherIdentity,
            Authorization(owner.AgentId, owner.SessionId, owner.Correlation, otherIdentity));

        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(otherPrincipal, location, new IdempotencyKey("record-owner-2")),
                Grant(otherPrincipal, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("The directory route cannot be recorded."));
    }

    [Fact]
    public async Task RecordAsync_WhenExistingLocationMatchesAndNewKeyRetried_ReturnsExistingAsExisting()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        _ = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-second-key-1")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        // A fresh idempotency key against the exact same address, tenant, owner, and store key indexes a new
        // write route but resolves to the already-committed location.
        var result = await directory.RecordAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
                new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record-second-key-2")),
                Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocationRecorded(location, existing: true));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var create = CreateRequest("record-create-unavailable");
        var location = Location("22222222-2222-2222-2222-222222222222", "store-a");

        var result = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(create, location),
                Grant(create, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsDenied()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var create = CreateRequest("record-create-denied");
        var location = Location("22222222-2222-2222-2222-222222222222", "store-a");

        var result = await directory.RecordCreateAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                new SessionDirectoryCreateRecordRequest(create, location),
                Grant(create, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryWriteDenied("Directory authorization could not be verified."));
    }

    [Fact]
    public async Task ListAsync_WhenRequiredAuditIsUnavailable_ReturnsUnavailable()
    {
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditUnavailable("unavailable")), new RecordingGrantStore());
        var request = ListRequest();

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(request, Grant(request), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionDirectoryListUnavailable("Required audit delivery is unavailable for the directory operation."));
    }

    [Fact]
    public async Task ListAsync_WhenGrantStoreReturnsWrongReceipt_ReturnsUnavailable()
    {
        var grants = new RecordingGrantStore { ReturnWrongReceipt = true };
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), grants);
        var request = ListRequest();

        var result = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(request, Grant(request), Intent()),
            TestContext.Current.CancellationToken);

        // ListAsync intentionally maps a denial the same way as an unavailability (see SqliteSessionDirectory.ListCoreAsync).
        result.ShouldBe(new SessionDirectoryListUnavailable("Directory authorization could not be verified."));
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
    public async Task ListAsync_WhenMoreRoutesExistThanOnePage_PagesEntirelyThroughSqlOrderingAndTheCursor()
    {
        // ListCandidateLocationsAsync used to select every route for the (tenant, agent) unfiltered and
        // unbounded, filtering by owner, applying the cursor, sorting, and taking MaximumResults + 1 entirely in
        // process. This must instead be pushed into SQL (WHERE owner_principal_id, WHERE session_id > $after,
        // ORDER BY session_id, LIMIT), and the SQL ordinal TEXT order must agree exactly with the cursor
        // comparison so consecutive pages neither skip nor duplicate a row.
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());

        var first = CreateRequest("first");
        var second = CreateRequest("second");
        var third = CreateRequest("third");
        var locationA = Location("a0000000-0000-0000-0000-000000000001", "store-a");
        var locationB = Location("10000000-0000-0000-0000-000000000002", "store-a");
        var locationC = Location("20000000-0000-0000-0000-000000000003", "store-a");
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(first, locationA),
            Grant(first, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(second, locationB),
            Grant(second, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);
        _ = await directory.RecordCreateAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
            new SessionDirectoryCreateRecordRequest(third, locationC),
            Grant(third, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()), TestContext.Current.CancellationToken);

        var firstPageRequest = ListRequest(maximumResults: 2);
        var firstPageResult = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                firstPageRequest, Grant(firstPageRequest), Intent()), TestContext.Current.CancellationToken);
        var firstPage = firstPageResult.ShouldBeOfType<SessionDirectoryPage>();

        firstPage.Locations.Select(static location => location.Address.SessionId)
            .ShouldBe([locationB.Address.SessionId, locationC.Address.SessionId]);
        var cursor = firstPage.NextCursor.ShouldNotBeNull();

        var secondPageRequest = ListRequest(cursor, maximumResults: 2);
        var secondPageResult = await directory.ListAsync(
            new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(
                secondPageRequest, Grant(secondPageRequest), Intent()), TestContext.Current.CancellationToken);
        var secondPage = secondPageResult.ShouldBeOfType<SessionDirectoryPage>();

        secondPage.Locations.Select(static location => location.Address.SessionId)
            .ShouldBe([locationA.Address.SessionId]);
        secondPage.NextCursor.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenSchemaModeIsValidateExactAndTableMissing_Throws()
    {
        // ValidateExact must never install schema; a database without the directory table is rejected with the store's typed failure.
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-validate-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate, Pooling = false }.ConnectionString))
        {
            connection.Open();
        }

        var target = new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() => new SqliteSessionDirectory(
            _audience, new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(),
            new SequenceAuditRecordIds(), TimeProvider.System, target, SqliteSessionStoreSettings.CreateDefault()));

        exception.Message.ShouldBe("The SQLite session directory schema or persistent store identity is unavailable.");
        using var probe = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
        probe.Open();
        using var command = probe.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'agentkit_session_directory_schema';";
        Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenPersistedStoreInstanceIdDiffers_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-instance-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        var audits = new RecordingAuditDispatcher(new SecurityAuditAccepted());
        var grants = new RecordingGrantStore();
        _ = new SqliteSessionDirectory(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault());
        var foreign = new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() => new SqliteSessionDirectory(
            _audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System, foreign,
            SqliteSessionStoreSettings.CreateDefault()));

        exception.Message.ShouldBe("The SQLite session directory schema or persistent store identity is unavailable.");
    }

    [Fact]
    public void Constructor_WhenLegacyWholeBlobTableExists_LeavesItInertAndCreatesRelationalSchemaFresh()
    {
        // The pre-1.0 single-row-blob format is a breaking change: ApplyKnownMigrations never reads or reinterprets
        // it, it just creates the new relational tables fresh alongside the untouched legacy table. ValidateExact
        // against the legacy-only file fails typed until that fresh schema exists.
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-legacy-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate, Pooling = false }.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE agentkit_session_directory(singleton INTEGER PRIMARY KEY CHECK(singleton=1),state_json BLOB NOT NULL); INSERT INTO agentkit_session_directory VALUES(1,$state);";
            _ = command.Parameters.AddWithValue("$state", "{}"u8.ToArray());
            _ = command.ExecuteNonQuery();
        }

        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        var audits = new RecordingAuditDispatcher(new SecurityAuditAccepted());
        var grants = new RecordingGrantStore();

        var validateOnly = Should.Throw<InvalidOperationException>(() => new SqliteSessionDirectory(
            _audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact),
            SqliteSessionStoreSettings.CreateDefault()));
        _ = new SqliteSessionDirectory(
            _audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault());
        var migrated = new SqliteSessionDirectory(
            _audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact),
            SqliteSessionStoreSettings.CreateDefault());

        validateOnly.Message.ShouldBe("The SQLite session directory schema or persistent store identity is unavailable.");
        migrated.Durable.ShouldBeTrue();

        using var probe = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
        probe.Open();
        using var legacyProbe = probe.CreateCommand();
        legacyProbe.CommandText = "SELECT state_json FROM agentkit_session_directory WHERE singleton = 1;";
        ((byte[]) legacyProbe.ExecuteScalar()!).ShouldBe("{}"u8.ToArray());
    }

    [Fact]
    public async Task LocateAsync_WhenReopenedWithValidateExactAndMatchingInstance_ReturnsPersistedRoute()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-exact-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        var audits = new RecordingAuditDispatcher(new SecurityAuditAccepted());
        var grants = new RecordingGrantStore();
        var context = Context();
        var location = Location(context.SessionId.ToString(), "store-a", context.Identity.TenantId);
        var first = new SqliteSessionDirectory(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault());
        _ = await first.RecordAsync(new AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest>(
            new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")),
            Grant(context, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
            TestContext.Current.CancellationToken);
        var reopened = new SqliteSessionDirectory(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact),
            SqliteSessionStoreSettings.CreateDefault());

        var result = await reopened.LocateAsync(new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
            context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SessionLocated(location));
    }

    [Fact]
    public async Task RecordCreateAsync_WhenConcurrentLocateHydrates_DoesNotLoseCommittedRoute()
    {
        // Every committed route must survive concurrent reads that reload the durable projection while writers commit.
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore());
        const int routeCount = 32;
        var requests = Enumerable.Range(0, routeCount)
            .Select(index => (Request: CreateRequest($"concurrent-{index}"), Location: Location(new Guid(index + 1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 9]).ToString("D"), "store-a")))
            .ToArray();
        var reader = Context(sessionId: requests[0].Location.Address.SessionId);

        var operations = new List<Task>(routeCount * 4);
        foreach (var (request, location) in requests)
        {
            operations.Add(directory.RecordCreateAsync(
                new AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest>(
                    new SessionDirectoryCreateRecordRequest(request, location),
                    Grant(request, SecurityOperationKind.StateMutation, SecurityEffect.Mutate), Intent()),
                TestContext.Current.CancellationToken).AsTask());
            for (var probe = 0; probe < 4; probe++)
            {
                operations.Add(directory.LocateAsync(
                    new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                        reader, Grant(reader, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
                    TestContext.Current.CancellationToken).AsTask());
            }
        }

        await Task.WhenAll(operations);

        foreach (var (_, location) in requests)
        {
            var context = Context(sessionId: location.Address.SessionId);
            var located = await directory.LocateAsync(
                new AuthorizedSessionDirectoryRequest<SessionOperationContext>(
                    context, Grant(context, SecurityOperationKind.StateRead, SecurityEffect.Observe), Intent()),
                TestContext.Current.CancellationToken);
            located.ShouldBe(new SessionLocated(location), location.Address.SessionId.ToString());
        }
    }

    [Fact]
    public async Task ListAsync_WhenObserved_EmitsDirectoryActivityAndLog()
    {
        var logger = new RecordingLogger<SqliteSessionDirectory>();
        var directory = CreateDirectory(new RecordingAuditDispatcher(new SecurityAuditAccepted()), new RecordingGrantStore(), logger);
        var request = ListRequest();
        using var activities = new ActivityCollector(
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
        completed.EventId.Id.ShouldBe(25002);
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

    private static SqliteSessionDirectory CreateDirectory(
        ISecurityAuditDispatcher audits,
        RecordingGrantStore grants,
        Microsoft.Extensions.Logging.ILogger<SqliteSessionDirectory>? logger = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        return new SqliteSessionDirectory(_audience, audits, grants, new SequenceAuditRecordIds(), TimeProvider.System,
            new SqliteSessionStoreTarget(Path.Combine(path, "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault(), logger);
    }

    private static SessionOperationContext Context(string tenant = "tenant", SessionId? sessionId = null)
    {
        var identity = TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId("principal"), ExecutionSubjectKind.Human);
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
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SessionCreateRequest(agentId, identity, Authorization(agentId, null, correlation, identity), conversationId,
            new IdempotencyKey(idempotencyKey), extensions ?? ExtensionData.Empty);
    }

    private static SessionDirectoryListRequest ListRequest(SessionId? afterSessionId = null, int maximumResults = 1)
    {
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(GuidOperation(), null);
        return new SessionDirectoryListRequest(
            agentId, identity, Authorization(agentId, null, correlation, identity), afterSessionId, maximumResults);
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

    /// <summary>An audit dispatcher that always throws, to exercise the directory's catch-all authorization failure path.</summary>
    private sealed class ThrowingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated audit dispatch failure.");
    }

    private sealed class RecordingAuditDispatcher(SecurityAuditDispatchResult result): ISecurityAuditDispatcher
    {
        private readonly Lock _gate = new();
        private readonly List<SecurityAuditRecord> _records = [];

        public IReadOnlyList<SecurityAuditRecord> Records
        {
            get
            {
                lock (_gate)
                {
                    return [.. _records];
                }
            }
        }

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _records.Add(record);
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingGrantStore: ISecurityGrantStore
    {
        private readonly Lock _gate = new();
        private readonly List<SecurityEnforcementRequest> _enforcements = [];

        public IReadOnlyList<SecurityEnforcementRequest> Enforcements
        {
            get
            {
                lock (_gate)
                {
                    return [.. _enforcements];
                }
            }
        }

        public bool ReturnWrongReceipt { get; set; }
        public Action? AfterConsume { get; set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default)
        {
            Record(enforcement);
            return ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed."));
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default)
        {
            Record(enforcement);
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

        private void Record(SecurityEnforcementRequest enforcement)
        {
            lock (_gate)
            {
                _enforcements.Add(enforcement);
            }
        }
    }

    private sealed class SequenceAuditRecordIds: IIdentifierGenerator<SecurityAuditRecordId>
    {
        private int _next;

        public SecurityAuditRecordId Create() => new(new Guid(Interlocked.Increment(ref _next), 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
    }
}
