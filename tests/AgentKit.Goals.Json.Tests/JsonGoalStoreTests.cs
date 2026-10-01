// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json.Tests;

/// <summary>Verifies JSON goal-store initialization, identity binding, recovery, compaction, and bootstrap constraints.</summary>
public sealed class JsonGoalStoreTests
{
    private static readonly TestGoalGrants _grants = new();

    private static JsonGoalStore Open(JsonGoalStoreTestRoot root, JsonGoalStoreTarget? target = null, JsonGoalStoreSettings? settings = null) =>
        new(target ?? root.Target(), settings ?? JsonGoalStoreSettings.CreateDefault(), _grants, new Ids(), TimeProvider.System);

    private static async Task<(AgentId Agent, SessionId Session, RunId Run, SecurityAuthorizationContext Authorization)> CreateGoalAsync(
        JsonGoalStore store, string key = "create")
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        var request = new GoalRequestFactory(_grants, store.Descriptor.SecurityAudience)
            .Create(GoalTestData.Goal(agent, session, run), authorization, key);
        _ = (await store.CreateAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<GoalCreated>();
        return (agent, session, run, authorization);
    }

    [Fact]
    public void Descriptor_WhenRead_ClaimsDurabilityAndIntentDiscovery()
    {
        using var root = new JsonGoalStoreTestRoot();
        using var store = Open(root);

        store.Descriptor.IsDurable.ShouldBeTrue();
        store.Descriptor.SupportsIntentDiscovery.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        using var root = new JsonGoalStoreTestRoot();
        var settings = JsonGoalStoreSettings.CreateDefault();

        Should.Throw<ArgumentNullException>(() => new JsonGoalStore(null!, settings, _grants, new Ids(), TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonGoalStore(root.Target(), null!, _grants, new Ids(), TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonGoalStore(root.Target(), settings, null!, new Ids(), TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new JsonGoalStore(root.Target(), settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new JsonGoalStore(root.Target(), settings, _grants, new Ids(), null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Constructor_WhenBuilt_DoesNotOpenLockOrWriteAnything()
    {
        using var root = new JsonGoalStoreTestRoot();

        using var store = Open(root);

        Directory.GetFileSystemEntries(root.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestIsMissingAndCreationIsAllowed_CreatesItAndIsIdempotent()
    {
        using var root = new JsonGoalStoreTestRoot();
        using var store = Open(root);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        File.Exists(root.ManifestPath).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestIsMissingAndOnlyExistingRootsAreAllowed_Fails()
    {
        using var root = new JsonGoalStoreTestRoot();
        using var store = Open(root, root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<Exception>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestBelongsToAnotherInstance_FailsClosed()
    {
        using var root = new JsonGoalStoreTestRoot();
        using (var first = Open(root))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var second = Open(root, root.Target(new JsonGoalStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("identity");
    }

    [Fact]
    public async Task InitializeAsync_WhenASecondWriterOpensTheSameRoot_IsRejectedWhileTheFirstHoldsTheLock()
    {
        using var root = new JsonGoalStoreTestRoot();
        using var first = Open(root);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = Open(root);

        _ = await Should.ThrowAsync<Exception>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_AfterTheFirstStoreIsDisposed_LetsAnotherWriterOpenTheRoot()
    {
        using var root = new JsonGoalStoreTestRoot();
        var first = Open(root);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        first.Dispose();
        first.Dispose();
        using var second = Open(root);

        await second.InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Operations_WhenTheStoreIsDisposed_ThrowObjectDisposedException()
    {
        using var root = new JsonGoalStoreTestRoot();
        var store = Open(root);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsAllowed_RecoversEveryAcknowledgedRecord()
    {
        using var root = new JsonGoalStoreTestRoot();
        (AgentId Agent, SessionId Session, RunId Run, SecurityAuthorizationContext Authorization) owner;
        using (var store = Open(root))
        {
            owner = await CreateGoalAsync(store);
        }

        await File.AppendAllTextAsync(root.LogPath, "{\"tenant\":\"tenant\",\"recor", TestContext.Current.CancellationToken);
        using var reopened = Open(root);

        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        var intents = await reopened.ReadIntentsAsync(new GoalIntentScanRequest(new ComponentId("unconfigured"), 0, 5), TestContext.Current.CancellationToken);
        _ = intents.ShouldBeOfType<GoalPageRejected>();
        (await File.ReadAllBytesAsync(root.LogPath, TestContext.Current.CancellationToken)).Last().ShouldBe((byte) '\n');
        owner.Agent.ShouldNotBe(default);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsNotAllowed_Fails()
    {
        using var root = new JsonGoalStoreTestRoot();
        using (var store = Open(root))
        {
            _ = await CreateGoalAsync(store);
        }

        await File.AppendAllTextAsync(root.LogPath, "{\"tenant\":\"t", TestContext.Current.CancellationToken);
        using var reopened = Open(root, root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogExceedsTheCompactionThreshold_RewritesOneSnapshotPerGoal()
    {
        using var root = new JsonGoalStoreTestRoot();
        var settings = new JsonGoalStoreSettings(1_048_576, 1_048_576, 2, [], JsonEncodingSettings.CreateDefault());
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        using (var store = Open(root, settings: settings))
        {
            var factory = new GoalRequestFactory(_grants, store.Descriptor.SecurityAudience);
            var created = (await store.CreateAsync(factory.Create(GoalTestData.Goal(agent, session, run), authorization, "create"), TestContext.Current.CancellationToken))
                .ShouldBeOfType<GoalCreated>().Record;
            var ready = (await store.TransitionAsync(factory.Transition(GoalTestData.Transition(created, GoalStatus.Ready, "ready"), null, authorization), TestContext.Current.CancellationToken))
                .ShouldBeOfType<GoalTransitioned>().Record;
            _ = await store.TransitionAsync(factory.Transition(GoalTestData.Transition(ready, GoalStatus.Cancelled, "cancel"), null, authorization), TestContext.Current.CancellationToken);
        }

        (await File.ReadAllLinesAsync(root.LogPath, TestContext.Current.CancellationToken)).Length.ShouldBe(3);
        using var reopened = Open(root, settings: settings);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        (await File.ReadAllLinesAsync(root.LogPath, TestContext.Current.CancellationToken)).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ReadIntentsAsync_WhenAWorkerRestarts_RestoresThePersistedDelegationWithItsCapturedAuthorization()
    {
        using var root = new JsonGoalStoreTestRoot();
        var scanner = new ComponentId("worker");
        var settings = new JsonGoalStoreSettings(1_048_576, 1_048_576, 4_096, [scanner], JsonEncodingSettings.CreateDefault());
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        DelegationRequest delegation;
        using (var store = Open(root, settings: settings))
        {
            var factory = new GoalRequestFactory(_grants, store.Descriptor.SecurityAudience);
            var parent = (await store.CreateAsync(factory.Create(GoalTestData.Goal(agent, session, run), authorization, "root"), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalCreated>().Record;
            var ready = (await store.TransitionAsync(factory.Transition(GoalTestData.Transition(parent, GoalStatus.Ready, "r"), null, authorization), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalTransitioned>().Record;
            var attempt = GoalTestData.Attempt(parent.Goal.Id, 1, agent, session, run);
            var active = (await store.TransitionAsync(factory.Transition(GoalTestData.Transition(ready, GoalStatus.Active, "a"), new GoalAttemptStart(attempt), authorization), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalTransitioned>().Record;
            delegation = GoalTestData.Delegation(active.Goal, attempt.Id, run, GoalTestData.NewAgent(), "d", authorization);
            _ = await store.CreateAsync(
                factory.Create(GoalTestData.Goal(agent, session, run, parent.Goal.Id, GoalStatus.Ready), authorization, "child", delegation), TestContext.Current.CancellationToken);
        }

        using var restarted = Open(root, settings: settings);
        var page = (await restarted.ReadIntentsAsync(new GoalIntentScanRequest(scanner, 0, 5), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalPage>();

        page.Items.ShouldHaveSingleItem().Delegation.ShouldBe(delegation);
        page.Items[0].Delegation!.Authorization.ShouldBe(authorization);
    }

    [Fact]
    public async Task CreateAsync_WhenTheEncodingCannotFitTheConfiguredRecordBound_FailsInitializationAndPersistsNothing()
    {
        using var root = new JsonGoalStoreTestRoot();
        var settings = new JsonGoalStoreSettings(64, 1_048_576, 4_096, [], JsonEncodingSettings.CreateDefault());
        using var store = Open(root, settings: settings);
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        var request = new GoalRequestFactory(_grants, store.Descriptor.SecurityAudience).Create(GoalTestData.Goal(agent, session, run), authorization, "create");

        _ = await Should.ThrowAsync<Exception>(async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Options_WhenDefaulted_ExposeTheDocumentedBounds()
    {
        var options = new JsonGoalStoreOptions();

        (options.MaximumRecordBytes, options.MaximumDocumentBytes, options.CompactionRecordThreshold).ShouldBe((1_048_576, 1_048_576, 4_096));
        options.AuthorizedIntentScanners.ShouldBeEmpty();
    }

    [Fact]
    public void Settings_WhenABoundIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreSettings(0, 1, 1, [], encoding)).ParamName.ShouldBe("maximumRecordBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreSettings(1, 0, 1, [], encoding)).ParamName.ShouldBe("maximumDocumentBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreSettings(1, 1, 0, [], encoding)).ParamName.ShouldBe("compactionRecordThreshold");
        Should.Throw<ArgumentNullException>(() => new JsonGoalStoreSettings(1, 1, 1, null!, encoding)).ParamName.ShouldBe("authorizedIntentScanners");
        Should.Throw<ArgumentNullException>(() => new JsonGoalStoreSettings(1, 1, 1, [], null!)).ParamName.ShouldBe("encoding");
    }

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var id = new JsonGoalStoreInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "x");

        Should.Throw<ArgumentNullException>(() => new JsonGoalStoreTarget(null!, id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonGoalStoreTarget(" ", id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonGoalStoreTarget("relative/path", id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreTarget(path, default, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("expectedStoreInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreTarget(path, id, (JsonStoreOpenMode) 9, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreTarget(path, id, JsonStoreOpenMode.OpenExisting, (JsonStoreRecoveryMode) 9)).ParamName.ShouldBe("recoveryMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreTarget(path, id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("recoveryMode");
    }

    [Fact]
    public void InstanceId_WhenEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonGoalStoreInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void AddJsonGoalStore_WhenResolved_ReturnsOneSingletonPerKeyAndValidatesArguments()
    {
        using var root = new JsonGoalStoreTestRoot();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(_grants);
        _ = services.AddJsonGoalStore(new GoalStoreKey("a"), root.Target(), options => options.CompactionRecordThreshold = 10);
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IGoalStore>("a");

        provider.GetRequiredKeyedService<IGoalStore>("a").ShouldBeSameAs(store);
        (store as IDisposable)!.Dispose();
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddJsonGoalStore(new GoalStoreKey("a"), root.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonGoalStore(new GoalStoreKey("a"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddJsonGoalStore(default, root.Target())).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddJsonGoalStore_WhenABoundIsInvalid_FailsAtRegistration()
    {
        using var root = new JsonGoalStoreTestRoot();

        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddJsonGoalStore(new GoalStoreKey("a"), root.Target(), options => options.MaximumRecordBytes = 0))
            .ParamName.ShouldBe("maximumRecordBytes");
    }

    private sealed class Ids: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }
}
