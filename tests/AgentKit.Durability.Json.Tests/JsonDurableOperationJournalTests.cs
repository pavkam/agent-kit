// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Verifies the JSON journal's guards, root binding, single-writer lock, and crash recovery.</summary>
public sealed class JsonDurableOperationJournalTests
{
    private static readonly DurableJournalKey Key = new("json-journal");
    private static readonly FencingToken TokenOne = new(1);

    /// <summary>Verifies a keyless journal is refused, because every authorized request must name an exact journal.</summary>
    [Fact]
    public void Constructor_WhenKeyCarriesNoKeyText_ThrowsForTheKeyArgument()
    {
        var harness = new TestDurableSecurityHarness();
        var root = new JsonDurableTestRoot();

        var exception = Should.Throw<ArgumentException>(() => new JsonDurableOperationJournal(
            default,
            root.Target(),
            JsonDurableStoreSettings.CreateDefault(),
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("key");
    }

    /// <summary>Verifies the store root is required, because the journal fabricates no persistence target.</summary>
    [Fact]
    public void Constructor_WhenTargetIsNull_ThrowsForTheTargetArgument()
    {
        var harness = new TestDurableSecurityHarness();

        var exception = Should.Throw<ArgumentNullException>(() => new JsonDurableOperationJournal(
            Key,
            null!,
            JsonDurableStoreSettings.CreateDefault(),
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("target");
    }

    /// <summary>Verifies bounds are required, because every append and replay enforces them.</summary>
    [Fact]
    public void Constructor_WhenSettingsAreNull_ThrowsForTheSettingsArgument()
    {
        var harness = new TestDurableSecurityHarness();
        var root = new JsonDurableTestRoot();

        var exception = Should.Throw<ArgumentNullException>(() => new JsonDurableOperationJournal(
            Key,
            root.Target(),
            null!,
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("settings");
    }

    /// <summary>Verifies an injected clock is required, so commit timestamps never come from an ambient clock.</summary>
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsForTheTimeProviderArgument()
    {
        var harness = new TestDurableSecurityHarness();
        var root = new JsonDurableTestRoot();

        var exception = Should.Throw<ArgumentNullException>(() => new JsonDurableOperationJournal(
            Key,
            root.Target(),
            JsonDurableStoreSettings.CreateDefault(),
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            null!));

        exception.ParamName.ShouldBe("timeProvider");
    }

    /// <summary>Verifies construction performs no bootstrap effect, so the manifest appears only on initialization.</summary>
    [Fact]
    public void Constructor_WhenCalled_WritesNothingIntoTheRoot()
    {
        var root = new JsonDurableTestRoot();

        using var journal = Create(root, new TestDurableSecurityHarness());

        journal.Key.ShouldBe(Key);
        journal.SecurityAudience.ShouldBe(new ComponentId("agentkit.durability.json"));
        File.Exists(root.ManifestPath).ShouldBeFalse();
    }

    /// <summary>Verifies initialization binds the root's identity, kind, schema version, and encoding fingerprint.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRootIsEmpty_WritesTheBindingManifest()
    {
        var root = new JsonDurableTestRoot();
        using var journal = Create(root, new TestDurableSecurityHarness());

        await journal.InitializeAsync(TestContext.Current.CancellationToken);

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(
            File.ReadAllBytes(root.ManifestPath), JsonStoreSerialization.CreateCanonicalOptions());
        manifest.StoreId.ShouldBe(root.StoreInstanceId.Value);
        manifest.StoreKind.ShouldBe("agentkit.durability.operations");
        manifest.SchemaVersion.ShouldBe(1);
        manifest.FormatFingerprint.ShouldBe(JsonEncodingSettings.CreateDefault().Fingerprint);
    }

    /// <summary>Verifies repeated initialization is a no-op rather than a second lock acquisition.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRepeated_SucceedsWithoutReacquiringTheRoot()
    {
        var root = new JsonDurableTestRoot();
        using var journal = Create(root, new TestDurableSecurityHarness());
        await journal.InitializeAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(() => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a root with no manifest is refused when creation was not permitted.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestIsMissingAndCreationIsNotPermitted_ThrowsAnOpenFailure()
    {
        var root = new JsonDurableTestRoot();
        using var journal = Create(
            root,
            new TestDurableSecurityHarness(),
            root.Target(JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.RecoverTornAppends));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
    }

    /// <summary>Verifies a root written by another deployment is refused rather than adopted.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestIdentityDiffers_ThrowsACorruptEvidenceFailure()
    {
        var root = new JsonDurableTestRoot();
        root.WriteManifest(new JsonStoreManifest(
            Guid.NewGuid(),
            "agentkit.durability.operations",
            1,
            JsonEncodingSettings.CreateDefault().Fingerprint));
        using var journal = Create(root, new TestDurableSecurityHarness());

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a root owned by a different store family is refused.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestKindDiffers_ThrowsACorruptEvidenceFailure()
    {
        var root = new JsonDurableTestRoot();
        root.WriteManifest(new JsonStoreManifest(
            root.StoreInstanceId.Value,
            "agentkit.permissions.decisions",
            1,
            JsonEncodingSettings.CreateDefault().Fingerprint));
        using var journal = Create(root, new TestDurableSecurityHarness());

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies an unsupported schema version is refused instead of being read optimistically.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestSchemaVersionDiffers_ThrowsASchemaFailure()
    {
        var root = new JsonDurableTestRoot();
        root.WriteManifest(new JsonStoreManifest(
            root.StoreInstanceId.Value,
            "agentkit.durability.operations",
            2,
            JsonEncodingSettings.CreateDefault().Fingerprint));
        using var journal = Create(root, new TestDurableSecurityHarness());

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("schema_unsupported");
    }

    /// <summary>Verifies a root written under a different encoding contract is refused.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestFingerprintDiffers_ThrowsASchemaFailure()
    {
        var root = new JsonDurableTestRoot();
        root.WriteManifest(new JsonStoreManifest(
            root.StoreInstanceId.Value, "agentkit.durability.operations", 1, "sha256:another-contract"));
        using var journal = Create(root, new TestDurableSecurityHarness());

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("schema_unsupported");
    }

    /// <summary>Verifies a second live writer over the same root is refused rather than silently interleaved.</summary>
    /// <remarks>
    /// This is the honest limit of the adapter and the reason it ships no lease manager: the root permits one writer,
    /// so it cannot coordinate ownership between processes.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenAnotherWriterHoldsTheRoot_Throws()
    {
        var root = new JsonDurableTestRoot();
        using var first = Create(root, new TestDurableSecurityHarness());
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = Create(root, new TestDurableSecurityHarness());

        _ = await Should.ThrowAsync<Exception>(
            () => second.InitializeAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies the advisory lock is released on disposal so a restarted writer can open the root.</summary>
    [Fact]
    public async Task InitializeAsync_WhenThePreviousWriterWasDisposed_AcquiresTheRoot()
    {
        var root = new JsonDurableTestRoot();
        var first = Create(root, new TestDurableSecurityHarness());
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        first.Dispose();

        using var second = Create(root, new TestDurableSecurityHarness());

        await Should.NotThrowAsync(() => second.InitializeAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a write before trusted bootstrap fails closed before any grant is consumed.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenBootstrapDidNotRun_ThrowsAnOpenFailure()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        using var journal = Create(root, harness);
        var start = DurabilityConformanceData.Start(Key, TokenOne);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => journal.RecordStartAsync(
            Authorize(journal, harness, start), TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
        harness.ConsumedCount.ShouldBe(0);
    }

    /// <summary>Verifies a write after disposal throws instead of resurrecting a released root.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenTheJournalWasDisposed_ThrowsObjectDisposed()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var journal = Create(root, harness);
        await journal.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Dispose();
        var start = DurabilityConformanceData.Start(Key, TokenOne);

        _ = await Should.ThrowAsync<ObjectDisposedException>(() => journal.RecordStartAsync(
            Authorize(journal, harness, start), TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies each acknowledged transition is appended and flushed as one complete line.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenAcknowledged_FlushesOneCompleteLogRecord()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        using var journal = Create(root, harness);
        await journal.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, TokenOne);

        var result = await journal.RecordStartAsync(
            Authorize(journal, harness, start), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        root.LogLineCount().ShouldBe(1);
    }

    /// <summary>Verifies a denied write appends nothing, so a refusal cannot leave partial evidence.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenAuthorizationIsRefused_AppendsNothing()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness { ForcedConsumptionStatus = GrantConsumptionStatus.Revoked };
        using var journal = Create(root, harness);
        await journal.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, TokenOne);

        var result = await journal.RecordStartAsync(
            Authorize(journal, harness, start), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        root.LogLineCount().ShouldBe(0);
    }

    /// <summary>Verifies a torn trailing record is discarded and the log is compacted under explicit recovery.</summary>
    /// <remarks>
    /// A crash can only lose a record whose terminating newline never reached disk. Recovery discards exactly that
    /// record and keeps every acknowledged one, which is the guarantee an append-only log exists to provide.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornRecord_RecoversTheAcknowledgedRecords()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var writer = Create(root, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, TokenOne);
        _ = (await writer.RecordStartAsync(
            Authorize(writer, harness, start), TestContext.Current.CancellationToken)).ShouldBeOfType<DurableRecorded>();
        var checkpoint = DurabilityConformanceData.CheckpointRecord(Key, TokenOne);
        _ = (await writer.RecordCheckpointAsync(
                AuthorizeCheckpoint(writer, harness, checkpoint), TestContext.Current.CancellationToken))
            .ShouldBeOfType<DurableRecorded>();
        writer.Dispose();
        root.TearTrailingRecord();

        using var reopened = Create(root, harness);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var evidence = await reopened.LoadEvidenceAsync(
            AuthorizeRead(reopened, harness, start.Descriptor.Binding.Address),
            TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
        loaded.Evidence.Descriptor.ShouldBe(start.Descriptor);
        loaded.Evidence.LatestCheckpoint.ShouldBeNull();
        root.LogLineCount().ShouldBe(1);
    }

    /// <summary>Verifies a torn trailing record is refused when the host chose validation over recovery.</summary>
    [Fact]
    public async Task InitializeAsync_WhenTheLogIsTornAndRecoveryIsNotPermitted_ThrowsACorruptEvidenceFailure()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var writer = Create(root, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, TokenOne);
        _ = (await writer.RecordStartAsync(
            Authorize(writer, harness, start), TestContext.Current.CancellationToken)).ShouldBeOfType<DurableRecorded>();
        writer.Dispose();
        root.TearTrailingRecord();

        using var reopened = Create(
            root, harness, root.Target(JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => reopened.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies a complete but malformed line is corruption rather than a recoverable torn append.</summary>
    [Fact]
    public async Task InitializeAsync_WhenTheLogContainsAMalformedRecord_ThrowsRatherThanSkippingIt()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var writer = Create(root, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        writer.Dispose();
        root.AppendLogLine(/*lang=json,strict*/ "{\"kind\":\"started\"}");

        using var reopened = Create(root, harness);

        _ = await Should.ThrowAsync<Exception>(
            () => reopened.InitializeAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a persisted transition for an operation that was never started is refused.</summary>
    /// <remarks>
    /// Replaying a checkpoint against no acceptance record would invent an operation whose declaration nobody
    /// committed, so the journal treats the log as corrupt instead of projecting partial state.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenATransitionHasNoAcceptanceRecord_ThrowsACorruptEvidenceFailure()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var writer = Create(root, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        writer.Dispose();
        var orphan = DurableJournalRecord.ForCheckpointed(DurabilityConformanceData.CheckpointRecord(Key, TokenOne));
        root.AppendLogLine(Encoding.UTF8.GetString(JsonStoreSerialization.Encode(
            orphan, JsonEncodingSettings.CreateDefault().RecordOptions, 1_048_576)));

        using var reopened = Create(root, harness);
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => reopened.InitializeAsync(TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("corrupt_evidence");
    }

    /// <summary>Verifies exceeding the compaction threshold rewrites the log to one snapshot per live operation.</summary>
    [Fact]
    public async Task InitializeAsync_WhenReplayExceedsTheCompactionThreshold_RewritesTheLogFromLiveState()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var writer = Create(root, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, TokenOne);
        _ = (await writer.RecordStartAsync(
            Authorize(writer, harness, start), TestContext.Current.CancellationToken)).ShouldBeOfType<DurableRecorded>();
        for (var marker = 0; marker < 2; marker++)
        {
            var checkpoint = DurabilityConformanceData.CheckpointRecord(
                Key, TokenOne, marker: (byte) (10 + marker));
            _ = (await writer.RecordCheckpointAsync(
                    AuthorizeCheckpoint(writer, harness, checkpoint), TestContext.Current.CancellationToken))
                .ShouldBeOfType<DurableRecorded>();
        }

        writer.Dispose();
        root.LogLineCount().ShouldBe(3);

        using var reopened = Create(
            root,
            harness,
            settings: new JsonDurableStoreSettings(
                1_048_576, 1_048_576, 2, JsonEncodingSettings.CreateDefault()));
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        root.LogLineCount().ShouldBe(1);
        var evidence = await reopened.LoadEvidenceAsync(
            AuthorizeRead(reopened, harness, start.Descriptor.Binding.Address),
            TestContext.Current.CancellationToken);
        _ = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LatestCheckpoint.ShouldNotBeNull();
    }

    /// <summary>Verifies a compacted root still refuses a stale generation, because fencing state is persisted.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenAStaleTokenIsPresentedAfterReopen_IsFencedByThePersistedGeneration()
    {
        var root = new JsonDurableTestRoot();
        var harness = new TestDurableSecurityHarness();
        var writer = Create(root, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        _ = (await writer.RecordStartAsync(
                Authorize(writer, harness, DurabilityConformanceData.Start(Key, new FencingToken(7))),
                TestContext.Current.CancellationToken))
            .ShouldBeOfType<DurableRecorded>();
        writer.Dispose();

        using var reopened = Create(root, harness);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var checkpoint = DurabilityConformanceData.CheckpointRecord(Key, TokenOne);
        var result = await reopened.RecordCheckpointAsync(
            AuthorizeCheckpoint(reopened, harness, checkpoint), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(new FencingToken(7));
    }

    /// <summary>Verifies disposal is idempotent, so a host may dispose a journal it already released.</summary>
    [Fact]
    public async Task Dispose_WhenRepeated_DoesNotThrow()
    {
        var root = new JsonDurableTestRoot();
        var journal = Create(root, new TestDurableSecurityHarness());
        await journal.InitializeAsync(TestContext.Current.CancellationToken);

        journal.Dispose();

        Should.NotThrow(journal.Dispose);
    }

    private static JsonDurableOperationJournal Create(
        JsonDurableTestRoot root,
        TestDurableSecurityHarness harness,
        JsonDurableStoreTarget? target = null,
        JsonDurableStoreSettings? settings = null) =>
        new(
            Key,
            target ?? root.Target(),
            settings ?? JsonDurableStoreSettings.CreateDefault(),
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now));

    private static AuthorizedDurableRequest<DurableOperationStart> Authorize(
        JsonDurableOperationJournal journal,
        TestDurableSecurityHarness harness,
        DurableOperationStart start) =>
        harness.Authorize(
            start,
            Key,
            journal.SecurityAudience,
            start.Descriptor.Binding.Address,
            start.Descriptor.Binding.ExecutionContext.Authorization,
            DurableJournalSecurityBinding.Fingerprint(start),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Create,
            start.FencingToken);

    private static AuthorizedDurableRequest<DurableCheckpoint> AuthorizeCheckpoint(
        JsonDurableOperationJournal journal,
        TestDurableSecurityHarness harness,
        DurableCheckpoint checkpoint) =>
        harness.Authorize(
            checkpoint,
            Key,
            journal.SecurityAudience,
            checkpoint.Address,
            checkpoint.ExecutionContext.Authorization,
            DurableJournalSecurityBinding.Fingerprint(checkpoint),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Append,
            checkpoint.FencingToken);

    private static AuthorizedDurableRequest<DurableOperationAddress> AuthorizeRead(
        JsonDurableOperationJournal journal,
        TestDurableSecurityHarness harness,
        DurableOperationAddress address) =>
        harness.Authorize(
            address,
            Key,
            journal.SecurityAudience,
            address,
            DurabilityConformanceData.Authorization(address.OperationId),
            DurableJournalSecurityBinding.Fingerprint(address),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            requiredFence: null);

    /// <summary>Produces distinct audit-record identities for these scenarios.</summary>
    private sealed class TestAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() => new(Guid.NewGuid());
    }
}
