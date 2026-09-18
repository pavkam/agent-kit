// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the reusable protected session-store contract against the durable JSON adapter, plus its durability probes.</summary>
/// <remarks>
/// The shared suite never reopens a store, so the cases below cover the guarantee that is specific to this leaf: replaying
/// the newline-delimited record log must reproduce optimistic-concurrency versions, branch history, idempotency receipts,
/// per-lane ownership, and installed accepted run state exactly as they were before the process was lost.
/// </remarks>
public sealed class JsonSessionStoreTests: SessionStoreConformanceTests<JsonSessionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonSessionStoreConformanceFixture CreateFixture() => new();

    // ---- Durability probes beyond the shared suite. ----

    /// <summary>Verifies committed entries, the session version, and the append receipt all survive a reopen.</summary>
    [Fact]
    public async Task AppendAsync_WhenStoreIsReopened_ReplaysCommittedHistoryAndIdempotency()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(20));
        var entry = MessageEntry(descriptor, 30, 1, "durable");
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("durable"), [entry]);
        var first = (SessionAppended) await store.AppendAsync(
            await fixture.AuthorizeAsync(append, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var loaded = await reopened.LoadAsync(
            await fixture.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var page = await reopened.ReadAsync(
            await fixture.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var replay = await reopened.AppendAsync(
            await fixture.AuthorizeAsync(append, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(first.NewVersion);
        page.ShouldBeOfType<SessionPage>().Entries.ShouldBe([entry]);
        replay.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(first.NewVersion);
    }

    /// <summary>Verifies a stale expected version observed before a reopen still conflicts after it.</summary>
    [Fact]
    public async Task AppendAsync_WhenExpectedVersionIsStaleAcrossReopen_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(40));
        var committed = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("committed"),
            [MessageEntry(descriptor, 41, 1, "committed")]);
        _ = (await store.AppendAsync(
            await fixture.AuthorizeAsync(committed, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var stale = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("stale"),
            [MessageEntry(descriptor, 44, 2, "stale")]);
        var result = await reopened.AppendAsync(
            await fixture.AuthorizeAsync(stale, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionAppendConflict>();
        conflict.ExpectedVersion.ShouldBe(descriptor.Version);
        conflict.ActualVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
    }

    /// <summary>Verifies lane ownership and installed accepted run state survive a reopen and still fence a later start.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenStoreIsReopened_RetainsLaneOwnershipAndAcceptedState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneId = Identifier<ExecutionLaneId>(60);
        var laneContext = Context(descriptor.Address, laneId, new BeforeRunOperationCorrelation(
            Identifier<OperationId>(61), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(62), Profile(), Configuration(), Timestamp(60),
            new IdempotencyKey("provision"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(63), Identifier<InputId>(64), Identifier<SessionEntryId>(65),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit");
        var accepted = (AcceptedInput) await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var runId = Identifier<RunId>(70);
        var turnId = Identifier<TurnId>(71);
        var start = new SessionRunStartRequest(
            laneContext, admission.AdmissionId, [admission.AdmissionId], accepted.Receipt.AdmittedSequence,
            new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionVersion(provisioned.SessionVersion.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), null, runId, turnId,
            Identifier<SessionEntryId>(72), [Identifier<SessionEntryId>(73)], [Identifier<MessageId>(74)],
            Identifier<SessionEntryId>(75), new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(descriptor.Address.AgentId, descriptor.Address.SessionId,
                new InRunOperationCorrelation(laneContext.Correlation.OperationId, runId, turnId),
                laneContext.Identity),
            Timestamp(70), new IdempotencyKey("accept"));
        var started = (SessionRunAccepted) await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var inRunContext = Context(descriptor.Address, laneId, started.State.Correlation);
        var loaded = await reopened.LoadRunStateAsync(
            await fixture.AuthorizeAsync(new SessionRunStateRequest(inRunContext),
                SecurityOperationKind.StateRead, SecurityEffect.Observe, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var reprovision = new SessionExecutionLaneProvisionRequest(
            laneContext, started.State.CommittedCursor, started.SessionVersion, Identifier<SessionEntryId>(80),
            Profile(), Configuration(), Timestamp(80), new IdempotencyKey("reprovision"));
        var conflict = await reopened.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(reprovision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionRunStateLoaded>().State.ShouldBeEquivalentTo(started.State);
        _ = conflict.ShouldBeOfType<SessionExecutionLaneProvisionConflict>();
    }

    /// <summary>Verifies a deleted session stays deleted and its creation retry key stays retired after a reopen.</summary>
    [Fact]
    public async Task DeleteAsync_WhenStoreIsReopened_KeepsSessionRemovedAndCreationKeyRetired()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var create = CreateStoreRequest();
        var descriptor = (await store.CreateAsync(
            await fixture.AuthorizeAsync(create, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>().Descriptor;
        var context = Context(descriptor.Address, null, Correlation(90));
        var delete = new SessionDeleteRequest(context, new IdempotencyKey("delete"));
        _ = (await store.DeleteAsync(
            await fixture.AuthorizeAsync(delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleted>();

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var loaded = await reopened.LoadAsync(
            await fixture.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var recreated = await reopened.CreateAsync(
            await fixture.AuthorizeAsync(create, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var repeatedDelete = await reopened.DeleteAsync(
            await fixture.AuthorizeAsync(delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        _ = loaded.ShouldBeOfType<SessionNotFound>();
        _ = recreated.ShouldBeOfType<SessionCreateFailed>();
        _ = repeatedDelete.ShouldBeOfType<SessionDeleted>();
    }

    // ---- Lifecycle: idempotent initialize/dispose, uninitialized and disposed use. ----

    /// <summary>Verifies a second <see cref="JsonSessionStore.InitializeAsync"/> call is a no-op.</summary>
    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using var store = root.Open(authority, grants, timeProvider);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a second <see cref="JsonSessionStore.Dispose"/> call is a no-op.</summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_IsIdempotent()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        var store = root.Open(authority, grants, timeProvider);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.Dispose();

        Should.NotThrow(store.Dispose);
    }

    /// <summary>Verifies every protected method fails closed when the store was never initialized.</summary>
    [Fact]
    public async Task LoadAsync_WhenStoreWasNeverInitialized_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using var store = root.Open(authority, grants, timeProvider);
        var context = Context(new SessionAddress(Identifier<AgentId>(700), Identifier<SessionId>(701)), null, Correlation(702));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.LoadAsync(
                await authority.AuthorizeAsync(store, context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a disposed store rejects further use instead of serving its dropped projection.</summary>
    [Fact]
    public async Task LoadAsync_WhenStoreIsDisposed_ThrowsObjectDisposedException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        var store = root.Open(authority, grants, timeProvider);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var context = Context(new SessionAddress(Identifier<AgentId>(703), Identifier<SessionId>(704)), null, Correlation(705));
        store.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await store.LoadAsync(
                await authority.AuthorizeAsync(store, context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies initializing an already disposed store fails closed rather than reopening the root.</summary>
    [Fact]
    public async Task InitializeAsync_WhenStoreIsDisposed_ThrowsObjectDisposedException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        var store = root.Open(authority, grants, timeProvider);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    // ---- Bootstrap and manifest validation. ----

    /// <summary>Verifies opening an existing root with no manifest fails closed instead of creating one.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOpenExistingAndManifestIsMissing_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        _ = Directory.CreateDirectory(root.DirectoryPath);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using var store = root.Open(authority, grants, timeProvider, openMode: JsonStoreOpenMode.OpenExisting);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("no manifest");
    }

    /// <summary>Verifies a manifest written under a different persistent store identity is rejected.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestStoreIdentityDiffers_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var reopened = root.Open(authority, grants, timeProvider, instanceId: new JsonSessionStoreInstanceId(Guid.NewGuid()));
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("identity does not match");
    }

    /// <summary>Verifies a manifest schema version the store no longer supports is rejected.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestSchemaVersionIsUnsupported_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var first = root.Open(authority, grants, timeProvider))
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

        using var reopened = root.Open(authority, grants, timeProvider);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("schema version is unsupported");
    }

    /// <summary>Verifies changing a semantic option alters the fingerprint and is rejected as a different encoding contract.</summary>
    [Fact]
    public async Task InitializeAsync_WhenMaxDepthChanges_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.MaxDepth = 32;
        var differentEncoding = new JsonSessionStoreSettings(
            1_048_576, 1_048_576, 4_096, 4_096, new JsonEncodingSettings(options));
        using var reopened = root.Open(authority, grants, timeProvider, settings: differentEncoding);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("different encoding contract");
    }

    /// <summary>Verifies changing only indentation does not alter the fingerprint, since presentation is excluded from it.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOnlyWriteIndentedChanges_IsAccepted()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.WriteIndented = true;
        var differentIndentation = new JsonSessionStoreSettings(
            1_048_576, 1_048_576, 4_096, 4_096, new JsonEncodingSettings(options));
        using var reopened = root.Open(authority, grants, timeProvider, settings: differentIndentation);

        await Should.NotThrowAsync(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies an encoding contract that cannot round-trip the store's fidelity probe is rejected at initialization.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEncodingContractCannotRoundTrip_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        var options = JsonStoreSerialization.CreateCanonicalOptions();
        options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
        var unusableEncoding = new JsonSessionStoreSettings(
            1_048_576, 1_048_576, 4_096, 4_096, new JsonEncodingSettings(options));
        using var store = root.Open(authority, grants, timeProvider, settings: unusableEncoding);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("cannot round-trip");
    }

    // ---- Torn appends and log corruption. ----

    /// <summary>Verifies a torn trailing append is discarded under recovery and the store still opens cleanly.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogEndsInTornAppend_WithRecoverTornAppends_DiscardsPartialRecord()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        SessionDescriptor descriptor;
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            descriptor = (await first.CreateAsync(
                await authority.AuthorizeAsync(first, CreateStoreRequest(), SecurityOperationKind.StateMutation,
                    SecurityEffect.Create, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>().Descriptor;
        }

        var logPath = Path.Combine(root.DirectoryPath, "sessions.jsonl");
        using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write))
        {
            stream.Write("{\"kind\":\"Ent"u8);
        }

        using var reopened = root.Open(authority, grants, timeProvider, recoveryMode: JsonStoreRecoveryMode.RecoverTornAppends);
        await Should.NotThrowAsync(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        var loaded = await reopened.LoadAsync(
            await authority.AuthorizeAsync(reopened, Context(descriptor.Address, null, Correlation(710)),
                SecurityOperationKind.StateRead, SecurityEffect.Observe, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        _ = loaded.ShouldBeOfType<SessionLoaded>();
    }

    /// <summary>Verifies a torn trailing append under strict validation fails closed instead of silently discarding data.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogEndsInTornAppend_WithValidateExact_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            _ = await first.CreateAsync(
                await authority.AuthorizeAsync(first, CreateStoreRequest("torn-2"), SecurityOperationKind.StateMutation,
                    SecurityEffect.Create, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);
        }

        var logPath = Path.Combine(root.DirectoryPath, "sessions.jsonl");
        using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write))
        {
            stream.Write("{\"kind\":\"Ent"u8);
        }

        using var reopened = root.Open(
            authority, grants, timeProvider, openMode: JsonStoreOpenMode.OpenExisting,
            recoveryMode: JsonStoreRecoveryMode.ValidateExact);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("incomplete record");
    }

    /// <summary>Verifies a complete but malformed log line fails closed rather than being silently skipped.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogContainsAMalformedCompleteLine_Throws()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var logPath = Path.Combine(root.DirectoryPath, "sessions.jsonl");
        File.AppendAllText(logPath, "not-json-at-all\n");

        using var reopened = root.Open(authority, grants, timeProvider);
        _ = await Should.ThrowAsync<Exception>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a well-formed record that references a session no longer present fails closed instead of applying partially.</summary>
    [Fact]
    public async Task InitializeAsync_WhenLogRecordReferencesAnUnknownSession_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        SessionDescriptor descriptor;
        SessionAppendRequest orphanAppend;
        using (var first = root.Open(authority, grants, timeProvider))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            descriptor = (await first.CreateAsync(
                await authority.AuthorizeAsync(first, CreateStoreRequest("orphan"), SecurityOperationKind.StateMutation,
                    SecurityEffect.Create, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>().Descriptor;
            var context = Context(descriptor.Address, null, Correlation(720));
            _ = (await first.DeleteAsync(
                await authority.AuthorizeAsync(first, new SessionDeleteRequest(context, new IdempotencyKey("orphan-delete")),
                    SecurityOperationKind.StateMutation, SecurityEffect.Delete, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleted>();
            orphanAppend = new SessionAppendRequest(
                context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("orphan-append"),
                [MessageEntry(descriptor, 721, 1, "orphan")]);
        }

        // Hand-append an EntriesAppended record for the now-deleted session directly to the log, bypassing the store's
        // own commit path, to simulate a persisted record that no longer applies against recovered state.
        var codecs = new SessionEntryCodecCatalog(
            [new MessageSessionEntryCodec()], TimeProvider.System);
        var recordEncoding = JsonSessionSerialization.CreateStoreRecordEncoding(JsonEncodingSettings.CreateDefault(), codecs);
        var orphanRecord = JsonSessionStoreLogRecord.ForAppend(orphanAppend, DateTimeOffset.UnixEpoch);
        var encoded = JsonStoreSerialization.Encode(orphanRecord, recordEncoding.RecordOptions, 1_048_576);
        var logPath = Path.Combine(root.DirectoryPath, "sessions.jsonl");
        using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write))
        {
            stream.Write(encoded);
            stream.WriteByte((byte) '\n');
        }

        using var reopened = root.Open(authority, grants, timeProvider);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reopened.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("no longer commits");
    }

    // ---- Exclusive lock and root confinement. ----

    /// <summary>Verifies a second store cannot initialize while the first still holds the exclusive lock, and can once it is disposed.</summary>
    [Fact]
    public async Task InitializeAsync_WhenAnotherStoreHoldsTheLock_FailsUntilTheFirstIsDisposed()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        var first = root.Open(authority, grants, timeProvider);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = root.Open(authority, grants, timeProvider);

        _ = await Should.ThrowAsync<Exception>(async () =>
            await second.InitializeAsync(TestContext.Current.CancellationToken));

        first.Dispose();
        await Should.NotThrowAsync(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies a symbolic-link root is rejected instead of silently following the link.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRootIsSymbolicLink_ThrowsInvalidOperationException()
    {
        using var directory = new DisposableDirectory();
        var real = Path.Combine(directory.Path, "real");
        _ = Directory.CreateDirectory(real);
        var link = Path.Combine(directory.Path, "link");
        _ = Directory.CreateSymbolicLink(link, real);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(timeProvider);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(authority);
        _ = services.AddSingleton<ISecurityGrantStore>(grants);
        _ = services.AddJsonSessionStore(new JsonSessionStoreTarget(
            link, new JsonSessionStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends));
        using var provider = services.BuildServiceProvider();
        using var store = (JsonSessionStore) provider.GetRequiredService<ISessionStore>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("replaceable link");
    }

    /// <summary>Verifies the store and the directory each require their own dedicated root rather than sharing one.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRootIsSharedWithADirectory_ThrowsInvalidOperationException()
    {
        using var root = new TestStoreRoot();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var authority = new TestStoreAuthority(grants, timeProvider);
        using (var store = root.Open(authority, grants, timeProvider))
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var auditRecordIds = new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value));
        using var directory = new JsonSessionDirectory(
            new ComponentId("agentkit.session.directory.json"), authority, grants, auditRecordIds, timeProvider,
            new JsonSessionDirectoryTarget(
                root.DirectoryPath, new JsonSessionDirectoryInstanceId(Guid.NewGuid()), JsonStoreOpenMode.OpenExisting,
                JsonStoreRecoveryMode.RecoverTornAppends),
            JsonSessionDirectorySettings.CreateDefault());

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await directory.InitializeAsync(TestContext.Current.CancellationToken));
    }

    // ---- CreateAsync/LoadAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies a second creation request for an already-present address under a different idempotency key fails rather than replaying.</summary>
    [Fact]
    public async Task CreateAsync_WhenAddressAlreadyPresentUnderADifferentIdempotencyKey_ReturnsSessionCreateFailed()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = CreateStoreRequest("first-key");
        _ = (await store.CreateAsync(
            await fixture.AuthorizeAsync(first, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>();
        var second = CreateStoreRequest("second-key");

        var result = await store.CreateAsync(
            await fixture.AuthorizeAsync(second, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionCreateFailed>().SafeMessage.ShouldContain("already present");
    }

    // ---- ProvisionLaneAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies provisioning against an unavailable session is rejected instead of throwing.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenSessionIsUnavailable_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Identifier<AgentId>(900), Identifier<SessionId>(901));
        var laneContext = Context(address, Identifier<ExecutionLaneId>(902),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(903), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(new BranchId(new Guid(904, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), null),
            new SessionVersion(0), Identifier<SessionEntryId>(905), Profile(), Configuration(), Timestamp(900),
            new IdempotencyKey("absent"));

        var result = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionExecutionLaneProvisionRejected>();
    }

    /// <summary>Verifies replaying the exact same provisioning request returns the original receipt marked existing.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenRequestIsReplayed_ReturnsExistingReceipt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneContext = Context(descriptor.Address, Identifier<ExecutionLaneId>(910),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(911), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(912), Profile(), Configuration(), Timestamp(910), new IdempotencyKey("replay-provision"));
        var first = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var replay = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var provisioned = replay.ShouldBeOfType<SessionExecutionLaneProvisioned>();
        provisioned.Existing.ShouldBeTrue();
        provisioned.ExecutionLaneId.ShouldBe(first.ExecutionLaneId);
    }

    /// <summary>Verifies reusing a provisioning idempotency key with different evidence conflicts instead of replaying.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenIdempotencyKeyReusedWithDifferentEvidence_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneContext = Context(descriptor.Address, Identifier<ExecutionLaneId>(913),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(914), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(915), Profile(), Configuration(), Timestamp(913), new IdempotencyKey("reused-provision"));
        _ = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var different = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(916), Profile(), Configuration(), Timestamp(913), new IdempotencyKey("reused-provision"));

        var result = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(different, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldContain("reused with different evidence");
    }

    /// <summary>Verifies a stale expected session version is rejected during provisioning.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenExpectedVersionIsStale_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneContext = Context(descriptor.Address, Identifier<ExecutionLaneId>(917),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(918), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null),
            new SessionVersion(descriptor.Version.Value + 1), Identifier<SessionEntryId>(919), Profile(), Configuration(),
            Timestamp(917), new IdempotencyKey("stale-version-provision"));

        var result = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldContain("stale");
    }

    /// <summary>Verifies a stale or unavailable branch cursor is rejected during provisioning.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenBranchCursorIsStale_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneContext = Context(descriptor.Address, Identifier<ExecutionLaneId>(920),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(921), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, Identifier<SessionEntryId>(922)),
            descriptor.Version, Identifier<SessionEntryId>(923), Profile(), Configuration(), Timestamp(920),
            new IdempotencyKey("stale-cursor-provision"));

        var result = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldContain("cursor");
    }

    /// <summary>Verifies a branch already owned by another lane cannot be claimed a second time.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenBranchAlreadyOwnedByAnotherLane_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var firstLane = Context(descriptor.Address, Identifier<ExecutionLaneId>(924),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(925), null));
        var firstProvision = new SessionExecutionLaneProvisionRequest(
            firstLane, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(926), Profile(), Configuration(), Timestamp(924), new IdempotencyKey("owner-lane"));
        _ = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(firstProvision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var secondLane = Context(descriptor.Address, Identifier<ExecutionLaneId>(927),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(928), null));
        var secondProvision = new SessionExecutionLaneProvisionRequest(
            secondLane, new SessionBranchCursor(descriptor.ActiveBranchId, Identifier<SessionEntryId>(926)),
            new SessionVersion(descriptor.Version.Value + 1), Identifier<SessionEntryId>(929), Profile(), Configuration(),
            Timestamp(927), new IdempotencyKey("second-lane"));

        var result = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(secondProvision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldContain("already owned");
    }

    /// <summary>Verifies a reserved provisioning entry identity already in use is rejected.</summary>
    [Fact]
    public async Task ProvisionLaneAsync_WhenEntryIdIsAlreadyReserved_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(930));
        var reservedEntryId = Identifier<SessionEntryId>(931);
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("reserve-entry"),
            [MessageEntryWithId(descriptor, reservedEntryId, 932, 1, "reserve")]);
        _ = (await store.AppendAsync(
            await fixture.AuthorizeAsync(append, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var laneContext = Context(descriptor.Address, Identifier<ExecutionLaneId>(933),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(934), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, reservedEntryId),
            new SessionVersion(descriptor.Version.Value + 1), reservedEntryId, Profile(), Configuration(), Timestamp(933),
            new IdempotencyKey("reused-entry-provision"));

        var result = await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionExecutionLaneProvisionConflict>().SafeMessage.ShouldContain("already reserved");
    }

    // ---- AppendAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies an appended entry identity that is already reserved is rejected.</summary>
    [Fact]
    public async Task AppendAsync_WhenEntryIdIsAlreadyReserved_ReturnsFailed()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(940));
        var reusedEntryId = Identifier<SessionEntryId>(941);
        var first = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("first-entry"),
            [MessageEntryWithId(descriptor, reusedEntryId, 942, 1, "first")]);
        _ = (await store.AppendAsync(
            await fixture.AuthorizeAsync(first, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var second = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(descriptor.Version.Value + 1),
            new IdempotencyKey("second-entry"), [MessageEntryWithId(descriptor, reusedEntryId, 943, 2, "second")]);

        var result = await store.AppendAsync(
            await fixture.AuthorizeAsync(second, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldContain("entry identity is already reserved");
    }

    /// <summary>Verifies an appended message identity that is already reserved is rejected.</summary>
    [Fact]
    public async Task AppendAsync_WhenMessageIdIsAlreadyReserved_ReturnsFailed()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(944));
        var reusedMessageId = Identifier<MessageId>(945);
        var first = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("first-message"),
            [MessageEntryWithMessageId(descriptor, reusedMessageId, 946, 1, "first")]);
        _ = (await store.AppendAsync(
            await fixture.AuthorizeAsync(first, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();
        var second = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, new SessionVersion(descriptor.Version.Value + 1),
            new IdempotencyKey("second-message"), [MessageEntryWithMessageId(descriptor, reusedMessageId, 947, 2, "second")]);

        var result = await store.AppendAsync(
            await fixture.AuthorizeAsync(second, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionAppendFailed>().SafeMessage.ShouldContain("message identity is already reserved");
    }

    // ---- DeleteAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies a delete for a session owned by a different tenant masks existence instead of removing it.</summary>
    [Fact]
    public async Task DeleteAsync_WhenSessionBelongsToADifferentTenant_ReturnsDeletedWithoutRemovingIt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var foreignIdentity = TestExecutionIdentity.Create(
            new TenantId("tenant-foreign"), new PrincipalId("foreign-user"), ExecutionSubjectKind.Human);
        var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(950), null);
        var foreignContext = new SessionOperationContext(
            descriptor.Address.AgentId, descriptor.Address.SessionId, null, correlation, foreignIdentity,
            Authorization(descriptor.Address.AgentId, descriptor.Address.SessionId, correlation, foreignIdentity));
        var delete = new SessionDeleteRequest(foreignContext, new IdempotencyKey("foreign-delete"));

        var result = await store.DeleteAsync(
            await fixture.AuthorizeAsync(delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionDeleted>();
        var loaded = await store.LoadAsync(
            await fixture.AuthorizeAsync(Context(descriptor.Address, null, Correlation(951)),
                SecurityOperationKind.StateRead, SecurityEffect.Observe, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        _ = loaded.ShouldBeOfType<SessionLoaded>();
    }

    // ---- AdmitInputAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies admission against an unavailable session is rejected instead of throwing.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenSessionIsUnavailable_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Identifier<AgentId>(1000), Identifier<SessionId>(1001));
        var laneContext = Context(address, Identifier<ExecutionLaneId>(1002),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(1003), null));
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(1004), Identifier<InputId>(1005), Identifier<SessionEntryId>(1006),
            new SessionVersion(0), new SessionLaneRevision(1),
            new SessionBranchCursor(new BranchId(new Guid(1007, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), null), "absent");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
    }

    /// <summary>Verifies reusing an admission idempotency key with different evidence conflicts instead of replaying.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenIdempotencyKeyReusedWithDifferentEvidence_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1010);
        var sharedKey = new IdempotencyKey("reused-admission-shared");
        var admission = AdmissionRequestWithKey(
            laneContext, Identifier<AdmissionId>(1013), Identifier<InputId>(1014), Identifier<SessionEntryId>(1015),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, sharedKey, "reused-a");
        _ = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var different = AdmissionRequestWithKey(
            laneContext, Identifier<AdmissionId>(1016), Identifier<InputId>(1017), Identifier<SessionEntryId>(1018),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, sharedKey, "reused-b");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(different, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldContain("reused with different evidence");
    }

    /// <summary>Verifies admitting the same caller input identity a second time with different evidence conflicts.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenInputIdAlreadyAdmittedWithDifferentEvidence_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1020);
        var inputId = Identifier<InputId>(1023);
        var first = AdmissionRequestWithInputId(
            laneContext, Identifier<AdmissionId>(1024), inputId, Identifier<SessionEntryId>(1025),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "first-reuse", "content-a");
        _ = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(first, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var second = AdmissionRequestWithInputId(
            laneContext, Identifier<AdmissionId>(1026), inputId, Identifier<SessionEntryId>(1027),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "second-reuse", "content-b");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(second, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldContain("different immutable evidence");
    }

    /// <summary>Verifies re-admitting the exact same caller input under a new idempotency key reconciles and persists a replay receipt.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenInputIdAlreadyAdmittedWithEquivalentEvidence_ReturnsAcceptedReplay()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1030);
        var inputId = Identifier<InputId>(1033);
        var first = AdmissionRequestWithInputId(
            laneContext, Identifier<AdmissionId>(1034), inputId, Identifier<SessionEntryId>(1035),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "equivalent-first", "same-content");
        var firstAccepted = (AcceptedInput) await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(first, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var second = AdmissionRequestWithInputId(
            laneContext, Identifier<AdmissionId>(1034), inputId, Identifier<SessionEntryId>(1035),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "equivalent-second", "same-content");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(second, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var accepted = result.ShouldBeOfType<AcceptedInput>();
        accepted.Receipt.Existing.ShouldBeTrue();
        accepted.Receipt.AdmissionId.ShouldBe(firstAccepted.Receipt.AdmissionId);
    }

    /// <summary>Verifies a stale expected session version is rejected during admission.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenExpectedVersionIsStale_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1040);
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(1043), Identifier<InputId>(1044), Identifier<SessionEntryId>(1045),
            new SessionVersion(provisioned.SessionVersion.Value + 1), provisioned.LaneRevision, provisioned.BranchCursor,
            "stale-version");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.StaleVersion);
    }

    /// <summary>Verifies admission against a session-wide execution lane that was never provisioned is rejected.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenLaneWasNeverProvisioned_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneContext = Context(descriptor.Address, Identifier<ExecutionLaneId>(1046),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(1047), null));
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(1048), Identifier<InputId>(1049), Identifier<SessionEntryId>(1050),
            descriptor.Version, new SessionLaneRevision(1),
            new SessionBranchCursor(descriptor.ActiveBranchId, null), "unprovisioned-lane");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
    }

    /// <summary>Verifies admission against a lane revision or branch cursor that no longer matches the provisioned lane is rejected.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenLaneRevisionOrCursorIsStale_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1050);
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(1053), Identifier<InputId>(1054), Identifier<SessionEntryId>(1055),
            provisioned.SessionVersion, new SessionLaneRevision(provisioned.LaneRevision.Value + 1), provisioned.BranchCursor,
            "stale-lane");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RejectedInput>().Rejection.Kind.ShouldBe(InputRejectionKind.StaleVersion);
    }

    /// <summary>Verifies an admission entry identity that is already reserved is rejected.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenEntryIdIsAlreadyReserved_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1060);
        var reservedEntryId = provisioned.BranchCursor.LastEntryId!.Value;
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(1063), Identifier<InputId>(1064), reservedEntryId,
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "reused-admission-entry");

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<InputConflict>().SafeReason.ShouldContain("entry identity is already reserved");
    }

    /// <summary>Verifies exceeding the maximum pending-input bound is reported as typed backpressure.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenPendingCapacityIsExceeded_ReturnsQueueCapacityExceeded()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1070);
        var first = AdmissionRequestWithKey(
            laneContext, Identifier<AdmissionId>(1073), Identifier<InputId>(1074), Identifier<SessionEntryId>(1075),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor,
            new IdempotencyKey("capacity-first-admit"), "capacity-first", maximumPendingInputs: 1);
        _ = (await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(first, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<AcceptedInput>();
        var second = AdmissionRequestWithKey(
            laneContext, Identifier<AdmissionId>(1076), Identifier<InputId>(1077), Identifier<SessionEntryId>(1078),
            new SessionVersion(provisioned.SessionVersion.Value + 1), new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId),
            new IdempotencyKey("capacity-second-admit"), "capacity-second", maximumPendingInputs: 1);

        var result = await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(second, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<QueueCapacityExceeded>().Limit.MaximumPendingInputs.ShouldBe(1);
    }

    // ---- AcceptRunAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies acceptance against an unavailable session is rejected instead of throwing.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenSessionIsUnavailable_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Identifier<AgentId>(1100), Identifier<SessionId>(1101));
        var laneContext = Context(address, Identifier<ExecutionLaneId>(1102),
            new BeforeRunOperationCorrelation(Identifier<OperationId>(1103), null));
        var start = MinimalRunStartRequest(laneContext, 1104);

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionRunStartRejected>();
    }

    /// <summary>Verifies a distributed fencing token is rejected, since this store provides only host-local coordination.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenFencingTokenIsSupplied_ReturnsRejectedBecauseFencingIsUnsupported()
    {
        // The JSON store never advertises distributed fencing, so the protected surface itself rejects any request
        // carrying a fence before AcceptRunCore's own "no fencing" branch could run.
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        store.Descriptor.SupportsDistributedFencing.ShouldBeFalse();
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1110);
        var (admission, accepted) = await AdmitOneInputAsync(fixture, store, laneContext, provisioned, 1113);
        var start = RunStartRequest(
            descriptor, laneContext, provisioned, admission, accepted, 1120, expectedFencingToken: new FencingToken(1));

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartRejected>().SafeReason.ShouldContain("does not support distributed fencing");
    }

    /// <summary>Verifies a stale lane revision is rejected during acceptance.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenLaneRevisionIsStale_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1130);
        var (admission, accepted) = await AdmitOneInputAsync(fixture, store, laneContext, provisioned, 1133);
        var start = RunStartRequest(
            descriptor, laneContext, provisioned, admission, accepted, 1140,
            expectedLaneRevision: new SessionLaneRevision(provisioned.LaneRevision.Value + 5));

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartConflict>().Kind.ShouldBe(SessionRunStartConflictKind.LaneRevision);
    }

    /// <summary>Verifies a stale branch cursor is rejected during acceptance.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenBranchCursorIsStale_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1150);
        var (admission, accepted) = await AdmitOneInputAsync(fixture, store, laneContext, provisioned, 1153);
        var start = RunStartRequest(
            descriptor, laneContext, provisioned, admission, accepted, 1160,
            branchCursor: new SessionBranchCursor(descriptor.ActiveBranchId, Identifier<SessionEntryId>(1161)));

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartConflict>().Kind.ShouldBe(SessionRunStartConflictKind.BranchCursor);
    }

    /// <summary>Verifies a stale expected session version is rejected during acceptance.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenSessionVersionIsStale_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1170);
        var (admission, accepted) = await AdmitOneInputAsync(fixture, store, laneContext, provisioned, 1173);
        var start = RunStartRequest(
            descriptor, laneContext, provisioned, admission, accepted, 1180,
            expectedVersion: new SessionVersion(provisioned.SessionVersion.Value + 5));

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartConflict>().Kind.ShouldBe(SessionRunStartConflictKind.SessionVersion);
    }

    /// <summary>Verifies an admission plan selected out of admitted-sequence order is no longer eligible for promotion.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenSelectedAdmissionsAreOutOfOrder_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1190);
        var (firstAdmission, firstAccepted) = await AdmitOneInputAsync(fixture, store, laneContext, provisioned, 1193);
        var secondAdmission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(1196), Identifier<InputId>(1197), Identifier<SessionEntryId>(1198),
            new SessionVersion(provisioned.SessionVersion.Value + 1), new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, firstAdmission.EntryId), "second-order");
        var secondAccepted = (AcceptedInput) await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(secondAdmission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var runId = Identifier<RunId>(1200);
        var turnId = Identifier<TurnId>(1201);
        var outOfOrder = new SessionRunStartRequest(
            laneContext, secondAccepted.Receipt.AdmissionId,
            [secondAccepted.Receipt.AdmissionId, firstAccepted.Receipt.AdmissionId],
            secondAccepted.Receipt.AdmittedSequence, new SessionLaneRevision(provisioned.LaneRevision.Value + 2),
            new SessionVersion(provisioned.SessionVersion.Value + 2),
            new SessionBranchCursor(descriptor.ActiveBranchId, secondAdmission.EntryId), null, runId, turnId,
            Identifier<SessionEntryId>(1202), [Identifier<SessionEntryId>(1203), Identifier<SessionEntryId>(1204)],
            [Identifier<MessageId>(1205), Identifier<MessageId>(1206)], Identifier<SessionEntryId>(1207),
            new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(descriptor.Address.AgentId, descriptor.Address.SessionId,
                new InRunOperationCorrelation(laneContext.Correlation.OperationId, runId, turnId), laneContext.Identity),
            Timestamp(1200), new IdempotencyKey("out-of-order-accept"));

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(outOfOrder, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartConflict>().Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
    }

    /// <summary>Verifies a reserved session-entry identity already in use is rejected during acceptance.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenReservedEntryIdIsAlreadyInUse_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, laneContext, provisioned) = await ProvisionLaneAsync(fixture, store, 1210);
        var (admission, accepted) = await AdmitOneInputAsync(fixture, store, laneContext, provisioned, 1213);
        var start = RunStartRequest(
            descriptor, laneContext, provisioned, admission, accepted, 1220, promotionEntryId: admission.EntryId);

        var result = await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunStartConflict>().Kind.ShouldBe(SessionRunStartConflictKind.PromotionPlan);
    }

    // ---- ReleaseRunAsync branches beyond the shared conformance suite. ----

    /// <summary>Verifies release against an unavailable session is rejected instead of throwing.</summary>
    [Fact]
    public async Task ReleaseRunAsync_WhenSessionIsUnavailable_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var address = new SessionAddress(Identifier<AgentId>(1300), Identifier<SessionId>(1301));
        var context = Context(address, Identifier<ExecutionLaneId>(1302), Correlation(1303));
        var release = new SessionRunReleaseRequest(
            context, new OperationStateRevision(1), new SessionVersion(0), new IdempotencyKey("absent-release"));

        var result = await store.ReleaseRunAsync(
            await fixture.AuthorizeAsync(release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
    }

    /// <summary>Verifies release against a lane that does not exist on an existing session is rejected.</summary>
    [Fact]
    public async Task ReleaseRunAsync_WhenLaneDoesNotExist_ReturnsRejected()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneId = Identifier<ExecutionLaneId>(1310);
        var context = Context(descriptor.Address, laneId, Correlation(1311));
        var release = new SessionRunReleaseRequest(
            context, new OperationStateRevision(1), descriptor.Version, new IdempotencyKey("missing-lane-release"));

        var result = await store.ReleaseRunAsync(
            await fixture.AuthorizeAsync(release, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunReleaseRejected>().Kind.ShouldBe(SessionRunReleaseRejectionKind.LaneNotFound);
    }

    // ---- Scenario helpers shared by the branch-coverage cases above. ----

    private static async ValueTask<(SessionDescriptor Descriptor, SessionOperationContext LaneContext, SessionExecutionLaneProvisioned Provisioned)>
        ProvisionLaneAsync(JsonSessionStoreConformanceFixture fixture, ISessionStore store, int seed)
    {
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneId = Identifier<ExecutionLaneId>(seed);
        var laneContext = Context(descriptor.Address, laneId,
            new BeforeRunOperationCorrelation(Identifier<OperationId>(seed + 1), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(seed + 2), Profile(), Configuration(), Timestamp(seed),
            new IdempotencyKey($"provision-{seed}"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        return (descriptor, laneContext, provisioned);
    }

    private static async ValueTask<(SessionInputAdmissionRequest Admission, AcceptedInput Accepted)> AdmitOneInputAsync(
        JsonSessionStoreConformanceFixture fixture, ISessionStore store, SessionOperationContext laneContext,
        SessionExecutionLaneProvisioned provisioned, int seed)
    {
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(seed), Identifier<InputId>(seed + 1), Identifier<SessionEntryId>(seed + 2),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, $"admit-{seed}");
        var accepted = (AcceptedInput) await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        return (admission, accepted);
    }

    private static SessionInputAdmissionRequest AdmissionRequestWithInputId(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId,
        SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, string key, string text)
    {
        var original = new AgentInput(
            inputId, InputDelivery.FollowUp, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(
            new ConfigurationVersion(1), new InputFingerprint($"sha256:{text}:original"),
            new InputFingerprint($"sha256:{text}:effective"));
        return new SessionInputAdmissionRequest(
            context, admissionId, entryId, original, original, preprocessing, Timestamp(99), version, laneRevision,
            cursor, new IdempotencyKey($"{key}-admit"), 8);
    }

    /// <summary>Builds an admission request under an explicit, caller-chosen idempotency key, to drive key-reuse cases.</summary>
    private static SessionInputAdmissionRequest AdmissionRequestWithKey(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId,
        SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, IdempotencyKey idempotencyKey,
        string text, int maximumPendingInputs = 8)
    {
        var original = new AgentInput(
            inputId, InputDelivery.FollowUp, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var effective = new AgentInput(
            inputId, InputDelivery.FollowUp, [new TextPart($"{text}-effective", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(
            new ConfigurationVersion(1), new InputFingerprint($"sha256:{text}:original"),
            new InputFingerprint($"sha256:{text}:effective"));
        return new SessionInputAdmissionRequest(
            context, admissionId, entryId, original, effective, preprocessing, Timestamp(99), version, laneRevision,
            cursor, idempotencyKey, maximumPendingInputs);
    }

    /// <summary>Builds a minimal, structurally valid run-start request against a fully provisioned lane and one admission.</summary>
    private static SessionRunStartRequest RunStartRequest(
        SessionDescriptor descriptor, SessionOperationContext laneContext, SessionExecutionLaneProvisioned provisioned,
        SessionInputAdmissionRequest admission, AcceptedInput accepted, int seed,
        FencingToken? expectedFencingToken = null, SessionLaneRevision? expectedLaneRevision = null,
        SessionBranchCursor? branchCursor = null, SessionVersion? expectedVersion = null,
        SessionEntryId? promotionEntryId = null)
    {
        var runId = Identifier<RunId>(seed);
        var turnId = Identifier<TurnId>(seed + 1);
        return new SessionRunStartRequest(
            laneContext, accepted.Receipt.AdmissionId, [accepted.Receipt.AdmissionId], accepted.Receipt.AdmittedSequence,
            expectedLaneRevision ?? new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            expectedVersion ?? new SessionVersion(provisioned.SessionVersion.Value + 1),
            branchCursor ?? new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), expectedFencingToken,
            runId, turnId, promotionEntryId ?? Identifier<SessionEntryId>(seed + 2),
            [Identifier<SessionEntryId>(seed + 3)], [Identifier<MessageId>(seed + 4)],
            Identifier<SessionEntryId>(seed + 5), new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(descriptor.Address.AgentId, descriptor.Address.SessionId,
                new InRunOperationCorrelation(laneContext.Correlation.OperationId, runId, turnId), laneContext.Identity),
            Timestamp(seed), new IdempotencyKey($"accept-{seed}"));
    }

    /// <summary>Builds a structurally valid, but never-committable, run-start request for use against an unavailable session.</summary>
    private static SessionRunStartRequest MinimalRunStartRequest(SessionOperationContext laneContext, int seed)
    {
        var runId = Identifier<RunId>(seed);
        var turnId = Identifier<TurnId>(seed + 1);
        var admissionId = Identifier<AdmissionId>(seed + 2);
        return new SessionRunStartRequest(
            laneContext, admissionId, [admissionId], new SessionSequence(1), new SessionLaneRevision(1), new SessionVersion(0),
            new SessionBranchCursor(new BranchId(new Guid(seed + 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)), null), null, runId, turnId,
            Identifier<SessionEntryId>(seed + 4), [Identifier<SessionEntryId>(seed + 5)], [Identifier<MessageId>(seed + 6)],
            Identifier<SessionEntryId>(seed + 7), new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(laneContext.AgentId, laneContext.SessionId,
                new InRunOperationCorrelation(laneContext.Correlation.OperationId, runId, turnId), laneContext.Identity),
            Timestamp(seed), new IdempotencyKey($"minimal-accept-{seed}"));
    }

    private sealed class DisposableDirectory: IDisposable
    {
        public DisposableDirectory() => Path = TestTemporaryDirectory.Create();

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    internal static async ValueTask<SessionDescriptor> CreateSessionAsync(
        JsonSessionStoreConformanceFixture fixture, ISessionStore store)
    {
        var request = CreateStoreRequest();
        var result = await store.CreateAsync(
            await fixture.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        return result.ShouldBeOfType<SessionCreated>().Descriptor;
    }

    internal static SessionStoreCreateRequest CreateStoreRequest(string idempotencyKey = "create")
    {
        var agentId = Identifier<AgentId>(1);
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
        var logical = new SessionCreateRequest(
            agentId, identity, Authorization(agentId, null, correlation, identity),
            Identifier<ConversationId>(3), new IdempotencyKey(idempotencyKey), ExtensionData.Empty);
        var address = new SessionAddress(agentId, Identifier<SessionId>(4));
        var context = new SessionOperationContext(
            address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
        return new SessionStoreCreateRequest(logical, address, context);
    }

    internal static SessionInputAdmissionRequest AdmissionRequest(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId,
        SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, string key)
    {
        var original = new AgentInput(
            inputId, InputDelivery.FollowUp,
            [new TextPart($"{key}-original", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var effective = new AgentInput(
            inputId, InputDelivery.FollowUp,
            [new TextPart($"{key}-effective", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(
            new ConfigurationVersion(1), new InputFingerprint($"sha256:{key}:original"),
            new InputFingerprint($"sha256:{key}:effective"));
        return new SessionInputAdmissionRequest(
            context, admissionId, entryId, original, effective, preprocessing, Timestamp(99), version,
            laneRevision, cursor, new IdempotencyKey($"{key}-admit"), 8);
    }

    internal static MessageSessionEntry MessageEntry(
        SessionDescriptor descriptor, int offset, long sequence, string text)
    {
        var correlation = Correlation(offset);
        var message = new UserMessage(
            Identifier<MessageId>(offset + 1), descriptor.Address.AgentId, descriptor.Address.SessionId,
            descriptor.ConversationId, descriptor.ActiveBranchId, correlation.RunId, null, Timestamp(offset),
            MessageState.Complete, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        return new MessageSessionEntry(
            Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation, descriptor.ActiveBranchId,
            new SessionSequence(sequence), null, Timestamp(offset), new SchemaVersion("1"), message);
    }

    /// <summary>Builds one message entry with an explicit, caller-chosen entry identity, to drive entry-collision cases.</summary>
    private static MessageSessionEntry MessageEntryWithId(
        SessionDescriptor descriptor, SessionEntryId entryId, int offset, long sequence, string text)
    {
        var entry = MessageEntry(descriptor, offset, sequence, text);
        return entry with { Id = entryId };
    }

    /// <summary>Builds one message entry with an explicit, caller-chosen message identity, to drive message-collision cases.</summary>
    private static MessageSessionEntry MessageEntryWithMessageId(
        SessionDescriptor descriptor, MessageId messageId, int offset, long sequence, string text)
    {
        var entry = MessageEntry(descriptor, offset, sequence, text);
        return entry with { Message = entry.Message with { Id = messageId } };
    }

    internal static SessionOperationContext Context(
        SessionAddress address, ExecutionLaneId? laneId, OperationCorrelation correlation) =>
        new(address.AgentId, address.SessionId, laneId, correlation, Identity(),
            Authorization(address.AgentId, address.SessionId, correlation, Identity()));

    internal static SecurityAuthorizationContext Authorization(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
        new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
            new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    internal static ExecutionIdentity Identity() => TestExecutionIdentity.Create(
        new TenantId("tenant-owner"), new PrincipalId("owner"), ExecutionSubjectKind.Human);

    internal static InRunOperationCorrelation Correlation(int offset) =>
        new(Identifier<OperationId>(offset), Identifier<RunId>(offset + 1), null);

    internal static SessionProfileReference Profile() =>
        new(new SessionProfileKey("conformance"), new SessionProfileVersion(1));

    internal static RunConfigurationReference Configuration() =>
        new(new ConfigurationVersion(1), new RunPolicyVersion(1),
            new ContentHash("sha256:conformance-configuration"));

    internal static DateTimeOffset Timestamp(int offset) =>
        DateTimeOffset.UnixEpoch.AddSeconds(Math.Abs((long) offset) + 1);

    internal static T Identifier<T>(int value)
    {
        var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        return typeof(T) switch
        {
            var type when type == typeof(AgentId) => (T) (object) new AgentId(guid),
            var type when type == typeof(SessionId) => (T) (object) new SessionId(guid),
            var type when type == typeof(ConversationId) => (T) (object) new ConversationId(guid),
            var type when type == typeof(OperationId) => (T) (object) new OperationId(guid),
            var type when type == typeof(RunId) => (T) (object) new RunId(guid),
            var type when type == typeof(TurnId) => (T) (object) new TurnId(guid),
            var type when type == typeof(ExecutionLaneId) => (T) (object) new ExecutionLaneId(guid),
            var type when type == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
            var type when type == typeof(MessageId) => (T) (object) new MessageId(guid),
            var type when type == typeof(AdmissionId) => (T) (object) new AdmissionId(guid),
            var type when type == typeof(InputId) => (T) (object) new InputId(guid),
            var type when type == typeof(SecurityPolicySnapshotId) =>
                (T) (object) new SecurityPolicySnapshotId(guid),
            _ => throw new InvalidOperationException($"Unsupported identifier type {typeof(T).FullName}."),
        };
    }
}
