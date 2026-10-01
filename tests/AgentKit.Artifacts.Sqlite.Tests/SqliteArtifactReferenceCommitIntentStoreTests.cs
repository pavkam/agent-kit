// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

using AgentKit.Observability;
using AgentKit.TestSupport;

/// <summary>Runs the shared intent-store contract suite against the SQLite adapter and verifies its durability, exclusivity, schema-kind, and observation guarantees.</summary>
public sealed class SqliteArtifactReferenceCommitIntentStoreTests: ArtifactReferenceCommitIntentStoreConformanceTests<SqliteArtifactReferenceCommitIntentStoreConformanceFixture>
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId _tenant = new("tenant");

    /// <inheritdoc/>
    protected override SqliteArtifactReferenceCommitIntentStoreConformanceFixture CreateFixture() => new();

    [Fact]
    public async Task Reopen_WhenIntentsWereRecordedAndTransitioned_PreservesEveryAcknowledgedChange()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var recorded = await store.RecordAsync(Intent(1), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(2), TestContext.Current.CancellationToken);
        _ = await store.TransitionAsync(_tenant, Preparation(2), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now.AddMinutes(5), TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();

        (await reopened.GetAsync(_tenant, Preparation(1), TestContext.Current.CancellationToken)).Intent.ShouldBe(recorded.Intent);
        (await reopened.GetAsync(_tenant, Preparation(2), TestContext.Current.CancellationToken)).Intent!.State.ShouldBe(ArtifactReferenceCommitState.Fenced);
        (await reopened.ListPendingAsync(_tenant, _now.AddDays(1), 10, TestContext.Current.CancellationToken)).Select(static intent => intent.PreparationId).ShouldBe([Preparation(1)]);
    }

    [Fact]
    public async Task TransitionAsync_WhenTheIntentIsFencedThenCollectedAcrossReopens_KeepsTheTerminalState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(1), TestContext.Current.CancellationToken);
        _ = await store.TransitionAsync(_tenant, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, TestContext.Current.CancellationToken);
        var reopened = fixture.Reopen();

        var collected = await reopened.TransitionAsync(_tenant, Preparation(1), ArtifactReferenceCommitState.Fenced, ArtifactReferenceCommitState.Collected, _now.AddMinutes(1), TestContext.Current.CancellationToken);
        var late = await fixture.Reopen().TransitionAsync(_tenant, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed, _now.AddMinutes(2), TestContext.Current.CancellationToken);

        collected.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        late.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.StateChanged);
        late.Intent!.State.ShouldBe(ArtifactReferenceCommitState.Collected);
    }

    [Fact]
    public async Task RecordAsync_WhenPersistenceFails_ThrowsAndLeavesTheProjectionUnchanged()
    {
        await using var fixture = CreateFixture();
        using var store = new SqliteArtifactReferenceCommitIntentStore(
            fixture.Database.Target(), new SqliteArtifactSettings(TimeSpan.FromSeconds(1), 16), TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidDataException>(async () => await store.RecordAsync(Intent(1), TestContext.Current.CancellationToken));

        (await store.GetAsync(_tenant, Preparation(1), TestContext.Current.CancellationToken)).Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnArtifactStore_RefusesItAndTheReverseHolds()
    {
        await using var fixture = CreateFixture();
        var grants = new Permissions.InMemory.InMemorySecurityGrantStore(TimeProvider.System);
        using (var artifacts = new SqliteArtifactStore(fixture.Database.Target("artifacts"), SqliteArtifactSettings.CreateDefault(), grants, new SequentialIntentIds(), TimeProvider.System))
        {
            await artifacts.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using (var intents = new SqliteArtifactReferenceCommitIntentStore(fixture.Database.Target("intents"), SqliteArtifactSettings.CreateDefault(), TimeProvider.System))
        {
            await intents.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var wrongIntents = new SqliteArtifactReferenceCommitIntentStore(fixture.Database.Target("artifacts"), SqliteArtifactSettings.CreateDefault(), TimeProvider.System);
        using var wrongArtifacts = new SqliteArtifactStore(fixture.Database.Target("intents"), SqliteArtifactSettings.CreateDefault(), grants, new SequentialIntentIds(), TimeProvider.System);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await wrongIntents.InitializeAsync(TestContext.Current.CancellationToken))).Message.ShouldContain("does not match the supported layout");
        (await Should.ThrowAsync<InvalidOperationException>(async () => await wrongArtifacts.InitializeAsync(TestContext.Current.CancellationToken))).Message.ShouldContain("does not match the supported layout");
    }

    [Fact]
    public async Task InitializeAsync_WhenAnotherStoreHoldsTheDatabase_ThrowsUntilItIsDisposed()
    {
        await using var fixture = CreateFixture();
        var settings = new SqliteArtifactSettings(TimeSpan.FromSeconds(1), 1_048_576);
        using var first = new SqliteArtifactReferenceCommitIntentStore(fixture.Database.Target(), settings, TimeProvider.System);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = new SqliteArtifactReferenceCommitIntentStore(fixture.Database.Target(), settings, TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
        first.Dispose();
        first.Dispose();

        await second.InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndMayNotBeCreated_Throws()
    {
        await using var fixture = CreateFixture();
        var target = fixture.Database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact);
        using var store = new SqliteArtifactReferenceCommitIntentStore(target, SqliteArtifactSettings.CreateDefault(), TimeProvider.System);

        (await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken))).Message.ShouldContain("does not exist");
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsNamingItBeforeAnyEffect()
    {
        using var database = new SqliteArtifactTestDatabase();
        var target = database.Target();
        var settings = SqliteArtifactSettings.CreateDefault();

        Should.Throw<ArgumentNullException>(() => new SqliteArtifactReferenceCommitIntentStore(null!, settings, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteArtifactReferenceCommitIntentStore(target, null!, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteArtifactReferenceCommitIntentStore(target, settings, null!)).ParamName.ShouldBe("time");
        File.Exists(database.PathOf()).ShouldBeFalse();
    }

    [Fact]
    public async Task RecordAsync_WhenObserved_EmitsAnIntentSpanLabelledSqliteWithTheTenant()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var unique = new TenantId($"tenant-{Guid.NewGuid():N}");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == unique.Value);

        _ = await store.RecordAsync(Intent(1, unique), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.ArtifactStoreAdapter).ShouldBe("sqlite");
        span.GetTagItem(AgentKitTagNames.ArtifactStoreOperation).ShouldBe("intent_record");
    }

    private static ArtifactPreparationId Preparation(int n) => new(new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));

    private static ArtifactReferenceCommitIntent Intent(int n, TenantId? tenant = null)
    {
        var id = new ArtifactReferenceCommitIntentId(new Guid(n, 9, 0, [0, 0, 0, 0, 0, 0, 0, 3]));
        return new ArtifactReferenceCommitIntent(
            id, tenant ?? _tenant, Preparation(n), new ArtifactId(new Guid(n, 8, 0, [0, 0, 0, 0, 0, 0, 0, 2])), new ArtifactVersion("1"),
            new ArtifactOwnerId("owner"), new ArtifactPin(id, _now, _now.AddHours(1)), ArtifactReferenceCommitState.Pending, _now.AddMinutes(n), _now.AddMinutes(n));
    }
}
