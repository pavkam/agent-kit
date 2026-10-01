// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite.Tests;

/// <summary>Verifies SQLite goal-store initialization, identity binding, cross-instance coordination, and persisted delegation.</summary>
public sealed class SqliteGoalStoreTests
{
    private static readonly TestGoalGrants _grants = new();
    private static readonly ComponentId _scanner = new("worker");

    private static SqliteGoalStore Open(SqliteGoalTestDatabase database, SqliteGoalStoreTarget? target = null) =>
        new(target ?? database.Target(), new SqliteGoalStoreSettings(TimeSpan.FromSeconds(5), 1_048_576, [_scanner]), _grants, new Ids(), TimeProvider.System);

    [Fact]
    public void Descriptor_WhenRead_ClaimsDurabilityAndIntentDiscovery()
    {
        using var database = new SqliteGoalTestDatabase();

        var store = Open(database);

        store.Descriptor.IsDurable.ShouldBeTrue();
        store.Descriptor.SupportsIntentDiscovery.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        using var database = new SqliteGoalTestDatabase();
        var settings = SqliteGoalStoreSettings.CreateDefault();

        Should.Throw<ArgumentNullException>(() => new SqliteGoalStore(null!, settings, _grants, new Ids(), TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteGoalStore(database.Target(), null!, _grants, new Ids(), TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteGoalStore(database.Target(), settings, null!, new Ids(), TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new SqliteGoalStore(database.Target(), settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new SqliteGoalStore(database.Target(), settings, _grants, new Ids(), null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Constructor_WhenBuilt_DoesNotCreateTheDatabase()
    {
        using var database = new SqliteGoalTestDatabase();

        _ = Open(database);

        File.Exists(database.Path).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndCreationIsAllowed_CreatesTheSchemaAndIsIdempotent()
    {
        using var database = new SqliteGoalTestDatabase();
        var store = Open(database);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        File.Exists(database.Path).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndOnlyExistingOnesAreAllowed_Fails()
    {
        using var database = new SqliteGoalTestDatabase();
        var store = Open(database, database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("does not exist");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSchemaIsMissingAndMigrationIsNotAllowed_Fails()
    {
        using var database = new SqliteGoalTestDatabase();
        await using (var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
        }

        var store = Open(database, database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("no initialized goal schema");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnotherInstance_FailsClosed()
    {
        using var database = new SqliteGoalTestDatabase();
        await Open(database).InitializeAsync(TestContext.Current.CancellationToken);
        var foreign = Open(database, database.Target(new SqliteGoalStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await foreign.InitializeAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("identity");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSchemaHasBeenTamperedWith_FailsClosed()
    {
        using var database = new SqliteGoalTestDatabase();
        await Open(database).InitializeAsync(TestContext.Current.CancellationToken);
        await using (var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE intruder(x INTEGER)";
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var store = Open(database, database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Stores_WhenTwoInstancesShareOneDatabase_ObserveEachOthersCommittedGoals()
    {
        using var database = new SqliteGoalTestDatabase();
        var first = Open(database);
        var second = Open(database);
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        var goal = GoalTestData.Goal(agent, session, run);
        var factory = new GoalRequestFactory(_grants, first.Descriptor.SecurityAudience);
        _ = await first.CreateAsync(factory.Create(goal, authorization, "create"), TestContext.Current.CancellationToken);

        var loaded = await second.LoadAsync(factory.Load(goal.Id, authorization), TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<GoalLoaded>().Record.Goal.Id.ShouldBe(goal.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenTwoInstancesCreateTheSameKeyConcurrently_ExactlyOneCreatesAndTheOtherReplays()
    {
        using var database = new SqliteGoalTestDatabase();
        var stores = new[] { Open(database), Open(database) };
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        var goal = GoalTestData.Goal(agent, session, run);
        var factory = new GoalRequestFactory(_grants, stores[0].Descriptor.SecurityAudience);
        var requests = Enumerable.Range(0, 6).Select(_ => factory.Create(goal, authorization, "same")).ToArray();

        var results = await Task.WhenAll(requests.Select((request, index) => stores[index % 2].CreateAsync(request, TestContext.Current.CancellationToken).AsTask()));

        results.OfType<GoalCreated>().Count(static created => !created.Replayed).ShouldBe(1);
        results.OfType<GoalCreated>().Count(static created => created.Replayed).ShouldBe(5);
    }

    [Fact]
    public async Task ReadIntentsAsync_WhenAWorkerRestarts_RestoresThePersistedDelegationWithItsCapturedAuthorization()
    {
        using var database = new SqliteGoalTestDatabase();
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        DelegationRequest delegation;
        var store = Open(database);
        var factory = new GoalRequestFactory(_grants, store.Descriptor.SecurityAudience);
        var parent = (await store.CreateAsync(factory.Create(GoalTestData.Goal(agent, session, run), authorization, "root"), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalCreated>().Record;
        var ready = (await store.TransitionAsync(factory.Transition(GoalTestData.Transition(parent, GoalStatus.Ready, "r"), null, authorization), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalTransitioned>().Record;
        var attempt = GoalTestData.Attempt(parent.Goal.Id, 1, agent, session, run);
        var active = (await store.TransitionAsync(factory.Transition(GoalTestData.Transition(ready, GoalStatus.Active, "a"), new GoalAttemptStart(attempt), authorization), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalTransitioned>().Record;
        delegation = GoalTestData.Delegation(active.Goal, attempt.Id, run, GoalTestData.NewAgent(), "d", authorization);
        _ = await store.CreateAsync(factory.Create(GoalTestData.Goal(agent, session, run, parent.Goal.Id, GoalStatus.Ready), authorization, "child", delegation), TestContext.Current.CancellationToken);

        var restarted = Open(database);
        var page = (await restarted.ReadIntentsAsync(new GoalIntentScanRequest(_scanner, 0, 5), TestContext.Current.CancellationToken)).ShouldBeOfType<GoalPage>();

        page.Items.ShouldHaveSingleItem().Delegation.ShouldBe(delegation);
        page.Items[0].Delegation!.Authorization.ShouldBe(authorization);
    }

    [Fact]
    public void Settings_WhenABoundIsInvalid_ThrowsWithTheExactParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreSettings(TimeSpan.FromMilliseconds(500), 1, [])).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreSettings(TimeSpan.FromMilliseconds(1500), 1, [])).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreSettings(TimeSpan.FromSeconds(1), 0, [])).ParamName.ShouldBe("maximumRecordBytes");
        Should.Throw<ArgumentNullException>(() => new SqliteGoalStoreSettings(TimeSpan.FromSeconds(1), 1, null!)).ParamName.ShouldBe("authorizedIntentScanners");
    }

    [Fact]
    public void Options_WhenDefaulted_ExposeTheDocumentedBounds()
    {
        var options = new SqliteGoalStoreOptions();

        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.MaximumRecordBytes.ShouldBe(1_048_576);
        options.AuthorizedIntentScanners.ShouldBeEmpty();
    }

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var id = new SqliteGoalStoreInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "x.db");

        Should.Throw<ArgumentNullException>(() => new SqliteGoalStoreTarget(null!, id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentException>(() => new SqliteGoalStoreTarget(" ", id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentException>(() => new SqliteGoalStoreTarget("relative.db", id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreTarget(path, default, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("expectedStoreInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreTarget(path, id, (SqliteDatabaseOpenMode) 9, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreTarget(path, id, SqliteDatabaseOpenMode.OpenExisting, (SqliteSchemaMode) 9)).ParamName.ShouldBe("schemaMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreTarget(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("schemaMode");
    }

    [Theory]
    [InlineData(":memory:")]
    [InlineData("file:goals.db")]
    [InlineData("|DataDirectory|goals.db")]
    public void Target_WhenPathIsNotAnOrdinaryFilePath_ThrowsArgumentException(string path) =>
        Should.Throw<ArgumentException>(() => new SqliteGoalStoreTarget(path, new SqliteGoalStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));

    [Fact]
    public void InstanceId_WhenEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteGoalStoreInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void AddSqliteGoalStore_WhenResolved_ReturnsOneSingletonPerKeyAndValidatesArguments()
    {
        using var database = new SqliteGoalTestDatabase();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(_grants);
        _ = services.AddSqliteGoalStore(new GoalStoreKey("a"), database.Target(), options => options.LockTimeout = TimeSpan.FromSeconds(2));
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IGoalStore>("a");

        provider.GetRequiredKeyedService<IGoalStore>("a").ShouldBeSameAs(store);
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddSqliteGoalStore(new GoalStoreKey("a"), database.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteGoalStore(new GoalStoreKey("a"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddSqliteGoalStore(default, database.Target())).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddSqliteGoalStore_WhenABoundIsInvalid_FailsAtRegistration()
    {
        using var database = new SqliteGoalTestDatabase();

        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddSqliteGoalStore(new GoalStoreKey("a"), database.Target(), options => options.MaximumRecordBytes = 0))
            .ParamName.ShouldBe("maximumRecordBytes");
    }

    private sealed class Ids: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }
}
