// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the SQLite journal's construction guards, bootstrap requirement, and cross-instance durability.</summary>
public sealed class SqliteDurableOperationJournalTests
{
    private static readonly DurableJournalKey Key = new("sqlite-journal");
    private static readonly FencingToken TokenOne = new(1);

    /// <summary>Verifies a keyless journal is refused, because every authorized request must name an exact journal.</summary>
    [Fact]
    public void Constructor_WhenKeyCarriesNoKeyText_ThrowsForTheKeyArgument()
    {
        var harness = new TestDurableSecurityHarness();
        var store = new SqliteDurableTestStore();

        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableOperationJournal(
            default,
            new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault()),
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("key");
    }

    /// <summary>Verifies the shared database is required, because the journal owns no target of its own.</summary>
    [Fact]
    public void Constructor_WhenDatabaseIsNull_ThrowsForTheDatabaseArgument()
    {
        var harness = new TestDurableSecurityHarness();

        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableOperationJournal(
            Key,
            null!,
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("database");
    }

    /// <summary>Verifies an injected clock is required, so commit timestamps never come from an ambient clock.</summary>
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsForTheTimeProviderArgument()
    {
        var harness = new TestDurableSecurityHarness();
        var store = new SqliteDurableTestStore();

        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableOperationJournal(
            Key,
            new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault()),
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            null!));

        exception.ParamName.ShouldBe("timeProvider");
    }

    /// <summary>Verifies the journal answers for its exact registration key and stable security audience.</summary>
    [Fact]
    public void Constructor_WhenCreated_ExposesItsKeyAndSecurityAudience()
    {
        var store = new SqliteDurableTestStore();

        var journal = CreateJournal(store, new TestDurableSecurityHarness());

        journal.Key.ShouldBe(Key);
        journal.SecurityAudience.ShouldBe(new ComponentId("agentkit.durability.sqlite"));
    }

    /// <summary>Verifies a write before trusted bootstrap fails closed instead of creating a store implicitly.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenBootstrapDidNotRun_ThrowsAnOpenFailure()
    {
        var store = new SqliteDurableTestStore();
        var harness = new TestDurableSecurityHarness();
        var journal = CreateJournal(store, harness);
        var start = DurabilityConformanceData.Start(Key, TokenOne);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => journal.RecordStartAsync(
            Authorize(journal, harness, start), TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
        harness.ConsumedCount.ShouldBe(0);
    }

    /// <summary>Verifies an evidence read before trusted bootstrap fails closed before any grant is consumed.</summary>
    [Fact]
    public async Task LoadEvidenceAsync_WhenBootstrapDidNotRun_ThrowsAnOpenFailure()
    {
        var store = new SqliteDurableTestStore();
        var harness = new TestDurableSecurityHarness();
        var journal = CreateJournal(store, harness);
        var address = DurabilityConformanceData.Address();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => journal.LoadEvidenceAsync(
            AuthorizeRead(journal, harness, address), TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
        harness.ConsumedCount.ShouldBe(0);
    }

    /// <summary>Verifies repeated initialization from the journal is a no-op, so both stores may bootstrap the database.</summary>
    [Fact]
    public async Task InitializeAsync_WhenRepeated_SucceedsWithoutRecreatingTheStore()
    {
        var store = new SqliteDurableTestStore();
        var journal = CreateJournal(store, new TestDurableSecurityHarness());

        await journal.InitializeAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(() => journal.InitializeAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies acknowledged evidence is observed by a second journal opened over the same database file.</summary>
    /// <remarks>
    /// This is the property the in-memory adapter cannot provide and the reason this package exists: a process that
    /// crashed after a settlement was acknowledged must find that settlement, not an empty store.
    /// </remarks>
    [Fact]
    public async Task LoadEvidenceAsync_WhenTheStoreIsReopened_ObservesEvidenceAcknowledgedByThePreviousInstance()
    {
        var store = new SqliteDurableTestStore();
        var harness = new TestDurableSecurityHarness();
        var database = store.CreateDatabase();
        var writer = CreateJournal(database, harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, TokenOne);
        var recorded = await writer.RecordStartAsync(
            Authorize(writer, harness, start), TestContext.Current.CancellationToken);
        _ = recorded.ShouldBeOfType<DurableRecorded>();

        var reopened = CreateJournal(store.CreateDatabase(), harness);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var evidence = await reopened.LoadEvidenceAsync(
            AuthorizeRead(reopened, harness, start.Descriptor.Binding.Address),
            TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
        loaded.Evidence.Descriptor.ShouldBe(start.Descriptor);
        loaded.Evidence.LastWriterToken.ShouldBe(TokenOne);
    }

    /// <summary>Verifies a reopened store keeps refusing a stale generation, because fencing state is persisted.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenAStaleTokenIsPresentedAfterReopen_IsFencedByThePersistedGeneration()
    {
        var store = new SqliteDurableTestStore();
        var harness = new TestDurableSecurityHarness();
        var writer = CreateJournal(store.CreateDatabase(), harness);
        await writer.InitializeAsync(TestContext.Current.CancellationToken);
        var start = DurabilityConformanceData.Start(Key, new FencingToken(7));
        _ = (await writer.RecordStartAsync(
            Authorize(writer, harness, start), TestContext.Current.CancellationToken)).ShouldBeOfType<DurableRecorded>();

        var reopened = CreateJournal(store.CreateDatabase(), harness);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var checkpoint = DurabilityConformanceData.CheckpointRecord(Key, TokenOne);
        var result = await reopened.RecordCheckpointAsync(
            AuthorizeCheckpoint(reopened, harness, checkpoint), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(new FencingToken(7));
    }

    private static SqliteDurableOperationJournal CreateJournal(
        SqliteDurableTestStore store,
        TestDurableSecurityHarness harness) =>
        CreateJournal(new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault()), harness);

    private static SqliteDurableOperationJournal CreateJournal(
        SqliteDurableDatabase database,
        TestDurableSecurityHarness harness) =>
        new(
            Key,
            database,
            new TestAuditRecordIdGenerator(),
            harness,
            harness,
            new FakeTimeProvider(DurabilityConformanceData.Now));

    private static AuthorizedDurableRequest<DurableOperationStart> Authorize(
        SqliteDurableOperationJournal journal,
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
        SqliteDurableOperationJournal journal,
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
        SqliteDurableOperationJournal journal,
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
