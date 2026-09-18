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

    /// <summary>Verifies the directory always advertises durability, since a committed route is flushed before it is observable.</summary>
    [Fact]
    public void Durable_WhenAccessed_IsAlwaysTrue()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);

        directory.Durable.ShouldBeTrue();
    }

    /// <summary>Verifies a second <see cref="JsonSessionDirectory.InitializeAsync"/> call is a no-op.</summary>
    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);

        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(async () => await directory.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a second <see cref="JsonSessionDirectory.Dispose"/> call is a no-op.</summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_IsIdempotent()
    {
        using var root = new TestDirectoryRoot();
        var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);

        directory.Dispose();

        Should.NotThrow(directory.Dispose);
    }

    /// <summary>Verifies a disposed directory rejects further use instead of serving its dropped projection.</summary>
    [Fact]
    public async Task LocateAsync_WhenDirectoryIsDisposed_ThrowsObjectDisposedException()
    {
        using var root = new TestDirectoryRoot();
        var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Context(Identifier<SessionId>(65));
        directory.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await directory.LocateAsync(
                await AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
                TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies every other protected method also fails closed before initialization.</summary>
    [Fact]
    public async Task RecordAsync_WhenDirectoryWasNotInitialized_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        var context = Context(Identifier<SessionId>(66));
        var write = new SessionDirectoryWriteRequest(context, Location(context, "agentkit.json"), new IdempotencyKey("uninit"));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await directory.RecordAsync(
                await AuthorizeAsync(write, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
                TestContext.Current.CancellationToken));
    }

    // ---- Bootstrap and manifest validation. ----

    /// <summary>Verifies opening an existing root with no manifest fails closed instead of creating one.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOpenExistingAndManifestIsMissing_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        _ = Directory.CreateDirectory(root.DirectoryPath);
        using var directory = root.Open(this, _grants, this, _timeProvider, openMode: JsonStoreOpenMode.OpenExisting);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await directory.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("no manifest");
    }

    /// <summary>Verifies a manifest written under a different persistent directory identity is rejected.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestStoreIdentityDiffers_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var reopened = root.Open(
            this, _grants, this, _timeProvider, instanceId: new JsonSessionDirectoryInstanceId(Guid.NewGuid()));
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("identity does not match");
    }

    /// <summary>Verifies a manifest schema version the directory no longer supports is rejected.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestSchemaVersionIsUnsupported_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var manifestPath = Path.Combine(root.DirectoryPath, "store.json");
        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(
            File.ReadAllBytes(manifestPath), JsonEncodingSettings.CreateDefault().DocumentOptions);
        var tampered = new JsonStoreManifest(manifest.StoreId, manifest.StoreKind, 99, manifest.FormatFingerprint);
        JsonAtomicDocument.Replace(
            manifestPath,
            JsonStoreSerialization.Encode(tampered, JsonEncodingSettings.CreateDefault().DocumentOptions, 1_048_576),
            TestContext.Current.CancellationToken);

        using var reopened = root.Open(this, _grants, this, _timeProvider);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("schema version is unsupported");
    }

    /// <summary>Verifies changing a semantic option alters the fingerprint and is rejected as a different encoding contract.</summary>
    [Fact]
    public async Task InitializeAsync_WhenMaxDepthChanges_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.MaxDepth = 32;
        var differentEncoding = new JsonSessionDirectorySettings(1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(options));
        using var reopened = root.Open(this, _grants, this, _timeProvider, settings: differentEncoding);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("different encoding contract");
    }

    /// <summary>Verifies changing only indentation does not alter the fingerprint, since presentation is excluded from it.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOnlyWriteIndentedChanges_IsAccepted()
    {
        using var root = new TestDirectoryRoot();
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.WriteIndented = true;
        var differentIndentation = new JsonSessionDirectorySettings(1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(options));
        using var reopened = root.Open(this, _grants, this, _timeProvider, settings: differentIndentation);

        await Should.NotThrowAsync(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies an encoding contract that cannot round-trip the directory's fidelity probe is rejected at initialization.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEncodingContractCannotRoundTrip_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        var unusableEncoding = new JsonSessionDirectorySettings(1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(options));
        using var directory = root.Open(this, _grants, this, _timeProvider, settings: unusableEncoding);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await directory.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("cannot round-trip");
    }

    // ---- Torn appends and log corruption. ----

    /// <summary>Verifies a torn trailing append is discarded under recovery and the directory still opens cleanly.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogEndsInTornAppend_WithRecoverTornAppends_DiscardsPartialRecord()
    {
        using var root = new TestDirectoryRoot();
        var context = Context(Identifier<SessionId>(70));
        var location = Location(context, "agentkit.json");
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            _ = (await first.RecordAsync(
                await AuthorizeAsync(new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("torn-write")),
                    SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        }

        var logPath = Path.Combine(root.DirectoryPath, "directory.jsonl");
        using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write))
        {
            stream.Write("{\"kind\":\"Rou"u8);
        }

        using var reopened = root.Open(this, _grants, this, _timeProvider, recoveryMode: JsonStoreRecoveryMode.RecoverTornAppends);
        await Should.NotThrowAsync(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        var located = await reopened.LocateAsync(
            await AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        located.ShouldBe(new SessionLocated(location));
    }

    /// <summary>Verifies a torn trailing append under strict validation fails closed instead of silently discarding data.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogEndsInTornAppend_WithValidateExact_ThrowsInvalidOperationException()
    {
        using var root = new TestDirectoryRoot();
        var context = Context(Identifier<SessionId>(71));
        var location = Location(context, "agentkit.json");
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            _ = (await first.RecordAsync(
                await AuthorizeAsync(new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("torn-write-2")),
                    SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        }

        var logPath = Path.Combine(root.DirectoryPath, "directory.jsonl");
        using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write))
        {
            stream.Write("{\"kind\":\"Rou"u8);
        }

        using var reopened = root.Open(
            this, _grants, this, _timeProvider, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.ValidateExact);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("incomplete record");
    }

    /// <summary>Verifies a complete but malformed log line fails closed rather than being silently skipped.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogContainsAMalformedCompleteLine_Throws()
    {
        using var root = new TestDirectoryRoot();
        using (var first = root.Open(this, _grants, this, _timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var logPath = Path.Combine(root.DirectoryPath, "directory.jsonl");
        File.AppendAllText(logPath, "not-json-at-all\n");

        using var reopened = root.Open(this, _grants, this, _timeProvider);
        _ = await Should.ThrowAsync<Exception>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    // ---- Exclusive lock and root confinement. ----

    /// <summary>Verifies a second directory cannot initialize while the first still holds the exclusive lock, and can once it is disposed.</summary>
    [Fact]
    public async Task InitializeAsync_WhenAnotherDirectoryHoldsTheLock_FailsUntilTheFirstIsDisposed()
    {
        using var root = new TestDirectoryRoot();
        var first = root.Open(this, _grants, this, _timeProvider);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = root.Open(this, _grants, this, _timeProvider);

        _ = await Should.ThrowAsync<Exception>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));

        first.Dispose();
        await Should.NotThrowAsync(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a symbolic-link root is rejected instead of silently following the link.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRootIsSymbolicLink_ThrowsInvalidOperationException()
    {
        var parent = TestTemporaryDirectory.Create();
        try
        {
            var real = Path.Combine(parent, "real");
            _ = Directory.CreateDirectory(real);
            var link = Path.Combine(parent, "link");
            _ = Directory.CreateSymbolicLink(link, real);
            using var directory = new JsonSessionDirectory(
                new ComponentId("agentkit.session.directory.json"), this, _grants, this, _timeProvider,
                new JsonSessionDirectoryTarget(
                    link, new JsonSessionDirectoryInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
                    JsonStoreRecoveryMode.RecoverTornAppends),
                JsonSessionDirectorySettings.CreateDefault());

            var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await directory.InitializeAsync(TestContext.Current.CancellationToken));
            exception.Message.ShouldContain("replaceable link");
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    // ---- RecordAsync/RecordCreateAsync branches beyond the shared cases above. ----

    /// <summary>Verifies a write for an address owned by a different tenant is denied rather than recorded.</summary>
    [Fact]
    public async Task RecordAsync_WhenExistingAddressBelongsToADifferentTenant_ReturnsDenied()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(80));
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.json"), new IdempotencyKey("owner-write")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        var foreignIdentity = Identity("tenant-foreign", "foreign-user");
        var foreignCorrelation = new BeforeRunOperationCorrelation(Identifier<OperationId>(81), null);
        var foreignContext = new SessionOperationContext(
            owner.AgentId, owner.SessionId, null, foreignCorrelation, foreignIdentity,
            Authorization(owner.AgentId, owner.SessionId, foreignCorrelation, foreignIdentity));

        var result = await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(
                    foreignContext, Location(foreignContext, "agentkit.json"), new IdempotencyKey("foreign-write")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDirectoryWriteDenied>();
    }

    /// <summary>Verifies a write for an existing address from a different owning principal within the same tenant is denied.</summary>
    [Fact]
    public async Task RecordAsync_WhenExistingAddressBelongsToADifferentOwner_ReturnsDenied()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(82));
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.json"), new IdempotencyKey("owner-write-2")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        var sameTenantDifferentOwner = Context(owner.SessionId, "tenant-owner", "different-owner");

        var result = await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(
                    sameTenantDifferentOwner, Location(owner, "agentkit.json"), new IdempotencyKey("different-owner-write")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDirectoryWriteDenied>();
    }

    /// <summary>Verifies a write for an existing address that names a different store key conflicts instead of overwriting the route.</summary>
    [Fact]
    public async Task RecordAsync_WhenExistingAddressNamesADifferentStoreKey_ReturnsConflict()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(83));
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.json"), new IdempotencyKey("owner-write-3")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();

        var result = await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.sqlite"), new IdempotencyKey("different-store-write")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLocationConflict>();
    }

    /// <summary>Verifies re-recording the same location under a new idempotency key reconciles rather than duplicating the write route.</summary>
    [Fact]
    public async Task RecordAsync_WhenReconciledUnderANewIdempotencyKey_ReturnsExistingLocationAndPersists()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(84));
        var location = Location(owner, "agentkit.json");
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, location, new IdempotencyKey("owner-write-4")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();

        var result = await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, location, new IdempotencyKey("owner-write-4-retry")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        var recorded = result.ShouldBeOfType<SessionLocationRecorded>();
        recorded.Existing.ShouldBeTrue();
        recorded.Location.ShouldBe(location);
    }

    /// <summary>Verifies replaying a creation route with different evidence under the same key conflicts instead of reconciling.</summary>
    [Fact]
    public async Task RecordCreateAsync_WhenRetriedWithDifferentEvidence_ReturnsConflict()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Context(Identifier<SessionId>(90));
        var create = new SessionCreateRequest(
            context.AgentId, context.Identity, Authorization(context.AgentId, null, context.Correlation, context.Identity),
            Identifier<ConversationId>(91), new IdempotencyKey("create-90"), ExtensionData.Empty);
        var record = new SessionDirectoryCreateRecordRequest(create, Location(context, "agentkit.json"));
        _ = (await directory.RecordCreateAsync(
            await AuthorizeAsync(record, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        var differentCreate = new SessionCreateRequest(
            context.AgentId, context.Identity, Authorization(context.AgentId, null, context.Correlation, context.Identity),
            Identifier<ConversationId>(92), new IdempotencyKey("create-90"), ExtensionData.Empty);
        var differentRecord = new SessionDirectoryCreateRecordRequest(differentCreate, Location(context, "agentkit.json"));

        var result = await directory.RecordCreateAsync(
            await AuthorizeAsync(differentRecord, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLocationConflict>();
    }

    /// <summary>Verifies a creation route for an address a plain write already recorded under a different tenant is denied.</summary>
    [Fact]
    public async Task RecordCreateAsync_WhenAddressAlreadyRoutedToADifferentTenant_ReturnsDenied()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(93));
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.json"), new IdempotencyKey("pre-write")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        var foreignIdentity = Identity("tenant-foreign", "foreign-user");
        var foreignCorrelation = new BeforeRunOperationCorrelation(Identifier<OperationId>(94), null);
        var foreignCreate = new SessionCreateRequest(
            owner.AgentId, foreignIdentity, Authorization(owner.AgentId, null, foreignCorrelation, foreignIdentity),
            Identifier<ConversationId>(95), new IdempotencyKey("foreign-create"), ExtensionData.Empty);
        var foreignLocation = new SessionLocation(
            owner.ToAddress(), foreignIdentity.TenantId, new SessionStoreKey("agentkit.json"),
            new SessionDirectoryRevision(1), _timeProvider.GetUtcNow(), new SchemaVersion("1"));
        var foreignRecord = new SessionDirectoryCreateRecordRequest(foreignCreate, foreignLocation);

        var result = await directory.RecordCreateAsync(
            await AuthorizeAsync(foreignRecord, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDirectoryWriteDenied>();
    }

    /// <summary>Verifies a creation route for an address a plain write already recorded under the same tenant conflicts.</summary>
    [Fact]
    public async Task RecordCreateAsync_WhenAddressAlreadyRoutedUnderTheSameTenant_ReturnsConflict()
    {
        using var root = new TestDirectoryRoot();
        using var directory = root.Open(this, _grants, this, _timeProvider);
        await directory.InitializeAsync(TestContext.Current.CancellationToken);
        var owner = Context(Identifier<SessionId>(96));
        _ = (await directory.RecordAsync(
            await AuthorizeAsync(
                new SessionDirectoryWriteRequest(owner, Location(owner, "agentkit.json"), new IdempotencyKey("pre-write-2")),
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionLocationRecorded>();
        var create = new SessionCreateRequest(
            owner.AgentId, owner.Identity, Authorization(owner.AgentId, null, owner.Correlation, owner.Identity),
            Identifier<ConversationId>(97), new IdempotencyKey("same-tenant-create"), ExtensionData.Empty);
        var record = new SessionDirectoryCreateRecordRequest(create, Location(owner, "agentkit.json"));

        var result = await directory.RecordCreateAsync(
            await AuthorizeAsync(record, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionLocationConflict>();
    }

    // ---- Store and directory require separate roots. ----

    /// <summary>Verifies a directory cannot be pointed at a root already owned by a session store.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRootIsSharedWithASessionStore_ThrowsInvalidOperationException()
    {
        using var storeRoot = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var store = storeRoot.Open(authority, grants, timeProvider))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var directory = new JsonSessionDirectory(
            new ComponentId("agentkit.session.directory.json"), this, _grants, this, timeProvider,
            new JsonSessionDirectoryTarget(
                storeRoot.DirectoryPath, new JsonSessionDirectoryInstanceId(Guid.NewGuid()), JsonStoreOpenMode.OpenExisting,
                JsonStoreRecoveryMode.RecoverTornAppends),
            JsonSessionDirectorySettings.CreateDefault());

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await directory.InitializeAsync(TestContext.Current.CancellationToken));
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
