// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

using AgentKit.Observability;
using AgentKit.TestSupport;

/// <summary>Runs the shared intent-store contract suite against the JSON adapter and verifies its durability, locking, recovery, compaction, store-kind, and observation guarantees.</summary>
public sealed class JsonArtifactReferenceCommitIntentStoreTests: ArtifactReferenceCommitIntentStoreConformanceTests<JsonArtifactReferenceCommitIntentStoreConformanceFixture>
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId _tenant = new("tenant");

    /// <inheritdoc/>
    protected override JsonArtifactReferenceCommitIntentStoreConformanceFixture CreateFixture() => new();

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
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsAllowed_RecoversEveryAcknowledgedIntent()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var recorded = await store.RecordAsync(Intent(1), TestContext.Current.CancellationToken);
        fixture.Reopen().Dispose();
        await File.AppendAllTextAsync(fixture.LogPath, "{\"id\":\"torn", TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();

        (await reopened.GetAsync(_tenant, Preparation(1), TestContext.Current.CancellationToken)).Intent.ShouldBe(recorded.Intent);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsNotAllowed_Fails()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(1), TestContext.Current.CancellationToken);
        fixture.Reopen().Dispose();
        await File.AppendAllTextAsync(fixture.LogPath, "{\"id\":\"torn", TestContext.Current.CancellationToken);
        using var strict = new JsonArtifactReferenceCommitIntentStore(
            fixture.Root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact), JsonArtifactSettings.CreateDefault(), TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await strict.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogExceedsTheCompactionThreshold_RewritesOneRecordPerIntentAndKeepsTheLatestState()
    {
        await using var fixture = CreateFixture();
        var settings = new JsonArtifactSettings(1_048_576, 1_048_576, 1_048_576, 3, JsonEncodingSettings.CreateDefault());
        using (var store = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target(), settings, TimeProvider.System))
        {
            _ = await store.RecordAsync(Intent(1), TestContext.Current.CancellationToken);
            _ = await store.RecordAsync(Intent(2), TestContext.Current.CancellationToken);
            _ = await store.TransitionAsync(_tenant, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, TestContext.Current.CancellationToken);
            _ = await store.TransitionAsync(_tenant, Preparation(1), ArtifactReferenceCommitState.Fenced, ArtifactReferenceCommitState.Collected, _now, TestContext.Current.CancellationToken);
        }

        using var reopened = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target(), settings, TimeProvider.System);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        File.ReadAllLines(fixture.LogPath).Length.ShouldBe(2);
        (await reopened.GetAsync(_tenant, Preparation(1), TestContext.Current.CancellationToken)).Intent!.State.ShouldBe(ArtifactReferenceCommitState.Collected);
        (await reopened.GetAsync(_tenant, Preparation(2), TestContext.Current.CancellationToken)).Intent!.State.ShouldBe(ArtifactReferenceCommitState.Pending);
    }

    [Fact]
    public async Task RecordAsync_WhenPersistenceFails_ThrowsAndLeavesTheProjectionUnchanged()
    {
        await using var fixture = CreateFixture();
        var settings = new JsonArtifactSettings(700, 1_048_576, 1_048_576, 4_096, JsonEncodingSettings.CreateDefault());
        using var store = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target(), settings, TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidDataException>(async () => await store.RecordAsync(Intent(1, owner: new string('o', 1_000)), TestContext.Current.CancellationToken));

        (await store.GetAsync(_tenant, Preparation(1), TestContext.Current.CancellationToken)).Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRootBelongsToAnArtifactStore_RefusesItAndCreatesNoPayloadDirectory()
    {
        await using var fixture = CreateFixture();
        var grants = new Permissions.InMemory.InMemorySecurityGrantStore(TimeProvider.System);
        using (var artifacts = new JsonArtifactStore(fixture.Root.Target("artifacts"), JsonArtifactSettings.CreateDefault(), grants, new SequentialIntentIds(), TimeProvider.System))
        {
            await artifacts.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using (var intents = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target("intents"), JsonArtifactSettings.CreateDefault(), TimeProvider.System))
        {
            await intents.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var wrongIntents = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target("artifacts"), JsonArtifactSettings.CreateDefault(), TimeProvider.System);
        using var wrongArtifacts = new JsonArtifactStore(fixture.Root.Target("intents"), JsonArtifactSettings.CreateDefault(), grants, new SequentialIntentIds(), TimeProvider.System);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await wrongIntents.InitializeAsync(TestContext.Current.CancellationToken))).Message.ShouldContain("identity does not match");
        (await Should.ThrowAsync<InvalidOperationException>(async () => await wrongArtifacts.InitializeAsync(TestContext.Current.CancellationToken))).Message.ShouldContain("identity does not match");
        Directory.Exists(fixture.Root.PayloadDirectory("intents")).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenAnotherStoreHoldsTheRoot_ThrowsUntilItIsDisposed()
    {
        await using var fixture = CreateFixture();
        using var first = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target(), JsonArtifactSettings.CreateDefault(), TimeProvider.System);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = new JsonArtifactReferenceCommitIntentStore(fixture.Root.Target(), JsonArtifactSettings.CreateDefault(), TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
        first.Dispose();
        first.Dispose();

        await second.InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsNamingItBeforeAnyEffect()
    {
        using var root = new JsonArtifactTestRoot();
        var target = root.Target();
        var settings = JsonArtifactSettings.CreateDefault();

        Should.Throw<ArgumentNullException>(() => new JsonArtifactReferenceCommitIntentStore(null!, settings, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactReferenceCommitIntentStore(target, null!, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactReferenceCommitIntentStore(target, settings, null!)).ParamName.ShouldBe("time");
        File.Exists(root.ManifestPath()).ShouldBeFalse();
    }

    [Fact]
    public void AddJsonArtifactReferenceCommitIntentStore_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        using var root = new JsonArtifactTestRoot();
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddJsonArtifactReferenceCommitIntentStore(root.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonArtifactReferenceCommitIntentStore(null!)).ParamName.ShouldBe("target");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddJsonArtifactReferenceCommitIntentStore(root.Target(), static options => options.MaximumRecordBytes = 0));
    }

    [Fact]
    public async Task AddJsonArtifactReferenceCommitIntentStore_WhenRegisteredTwice_ResolvesTheFirstUnkeyedStoreThatOpensLazily()
    {
        using var root = new JsonArtifactTestRoot();
        var services = new ServiceCollection();
        _ = services.AddJsonArtifactReferenceCommitIntentStore(root.Target());
        _ = services.AddJsonArtifactReferenceCommitIntentStore(root.Target("ignored"));
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IArtifactReferenceCommitIntentStore>();

        store.ShouldBeOfType<JsonArtifactReferenceCommitIntentStore>().ShouldBeSameAs(provider.GetRequiredService<IArtifactReferenceCommitIntentStore>());
        File.Exists(root.ManifestPath()).ShouldBeFalse();
        await ((JsonArtifactReferenceCommitIntentStore) store).InitializeAsync(TestContext.Current.CancellationToken);
        File.Exists(root.ManifestPath()).ShouldBeTrue();
        File.Exists(root.ManifestPath("ignored")).ShouldBeFalse();
    }

    [Fact]
    public async Task RecordAsync_WhenObserved_EmitsAnIntentSpanLabelledJsonWithTheTenant()
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
        span.GetTagItem(AgentKitTagNames.ArtifactStoreAdapter).ShouldBe("json");
        span.GetTagItem(AgentKitTagNames.ArtifactStoreOperation).ShouldBe("intent_record");
    }

    private static ArtifactPreparationId Preparation(int n) => new(new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));

    private static ArtifactReferenceCommitIntent Intent(int n, TenantId? tenant = null, string owner = "owner")
    {
        var id = new ArtifactReferenceCommitIntentId(new Guid(n, 9, 0, [0, 0, 0, 0, 0, 0, 0, 3]));
        return new ArtifactReferenceCommitIntent(
            id, tenant ?? _tenant, Preparation(n), new ArtifactId(new Guid(n, 8, 0, [0, 0, 0, 0, 0, 0, 0, 2])), new ArtifactVersion("1"),
            new ArtifactOwnerId(owner), new ArtifactPin(id, _now, _now.AddHours(1)), ArtifactReferenceCommitState.Pending, _now.AddMinutes(n), _now.AddMinutes(n));
    }
}
