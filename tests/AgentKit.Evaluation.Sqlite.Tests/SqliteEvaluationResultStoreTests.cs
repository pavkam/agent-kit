// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class SqliteEvaluationResultStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        using var database = new SqliteEvaluationTestDatabase();

        Should.Throw<ArgumentNullException>(() => new SqliteEvaluationResultStore(null!, SqliteEvaluationStoreSettings.CreateDefault(), TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteEvaluationResultStore(database.Target(), null!, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteEvaluationResultStore(database.Target(), SqliteEvaluationStoreSettings.CreateDefault(), null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public async Task InitializeAsync_WhenCalled_CreatesTheSchemaAndBindsTheIdentity()
    {
        using var database = new SqliteEvaluationTestDatabase();

        await database.Open().InitializeAsync(Token);
        await database.Open().InitializeAsync(Token);

        database.Execute("SELECT group_concat(name, '|') FROM (SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name)", scalar: true)
            .ShouldBe("evaluation_metadata|evaluation_results|evaluation_runs");
    }

    [Fact]
    public async Task InitializeAsync_WhenTokenIsCancelled_Throws()
    {
        using var database = new SqliteEvaluationTestDatabase();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await database.Open().InitializeAsync(cancellation.Token));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndCreationIsNotAllowed_Refuses()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var store = database.Open(database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(Token));

        exception.Message.ShouldContain("does not exist");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDirectoryIsMissing_Refuses()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var missing = new SqliteEvaluationStoreTarget(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "x.db"),
            new SqliteEvaluationStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await database.Open(missing).InitializeAsync(Token));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSchemaIsMissingAndMigrationIsNotAllowed_RefusesWithoutCreatingIt()
    {
        using var database = new SqliteEvaluationTestDatabase();
        _ = database.Execute("CREATE TABLE placeholder (x INTEGER)");
        _ = database.Execute("DROP TABLE placeholder");
        var store = database.Open(database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(Token));

        exception.Message.ShouldContain("no initialized");
        database.Execute("SELECT count(*) FROM sqlite_master WHERE name LIKE 'evaluation_%'", scalar: true).ShouldBe(0L);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnotherInstance_Refuses()
    {
        using var database = new SqliteEvaluationTestDatabase();
        await database.Open().InitializeAsync(Token);

        var wrong = database.Open(database.Target(new SqliteEvaluationStoreInstanceId(Guid.NewGuid())));

        (await Should.ThrowAsync<InvalidOperationException>(async () => await wrong.InitializeAsync(Token))).Message.ShouldContain("identity");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSchemaOrVersionIsUnsupported_Refuses()
    {
        using var database = new SqliteEvaluationTestDatabase();
        await database.Open().InitializeAsync(Token);
        _ = database.Execute("UPDATE evaluation_metadata SET schema_version = 99");

        (await Should.ThrowAsync<InvalidOperationException>(async () => await database.Open().InitializeAsync(Token))).Message.ShouldContain("version");

        _ = database.Execute("UPDATE evaluation_metadata SET schema_version = 1");
        _ = database.Execute("CREATE TABLE intruder (x INTEGER)");
        (await Should.ThrowAsync<InvalidOperationException>(async () => await database.Open().InitializeAsync(Token))).Message.ShouldContain("layout");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheIdentityRowIsMissing_Refuses()
    {
        using var database = new SqliteEvaluationTestDatabase();
        await database.Open().InitializeAsync(Token);
        _ = database.Execute("DELETE FROM evaluation_metadata");

        (await Should.ThrowAsync<InvalidOperationException>(async () => await database.Open().InitializeAsync(Token))).Message.ShouldContain("no identity");
    }

    [Fact]
    public async Task AppendAsync_WhenAcknowledged_PersistsAcrossReopenWithTheRunIdentityRetained()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var run = EvaluationResultConformanceData.NewRun();
        _ = await database.Open().AppendAsync(EvaluationResultConformanceData.Result(run, plan: "pinned", version: 3), Token);

        database.Execute("SELECT plan_id || ':' || plan_version FROM evaluation_runs", scalar: true).ShouldBe("pinned:3");
        database.Execute("SELECT count(*) FROM evaluation_results", scalar: true).ShouldBe(1L);
        (await database.Open().AppendAsync(EvaluationResultConformanceData.Result(run, 1, plan: "pinned", version: 4), Token))
            .ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
    }

    [Fact]
    public async Task AppendAsync_WhenAStoredDocumentIsCorrupt_FailsClosedInsteadOfReturningInvalidEvidence()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var run = EvaluationResultConformanceData.NewRun();
        var store = database.Open();
        _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run), Token);
        _ = database.Execute("UPDATE evaluation_results SET document = '{\"unknown\":1}'");

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.ReadAsync(new EvaluationResultQuery(run, 5), Token));
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.AppendAsync(EvaluationResultConformanceData.Result(run), Token));
    }

    [Fact]
    public async Task AppendAsync_WhenTheEncodedResultExceedsTheRecordBound_RejectsWithLimitExceededAndWritesNothing()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var store = database.Open(settings: new SqliteEvaluationStoreSettings(TimeSpan.FromSeconds(5), 256));

        var answer = await store.AppendAsync(EvaluationResultConformanceData.Complete(EvaluationResultConformanceData.NewRun()), Token);

        answer.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.LimitExceeded);
        database.Execute("SELECT count(*) FROM evaluation_results", scalar: true).ShouldBe(0L);
    }

    [Fact]
    public async Task AppendAsync_WhenAnotherConnectionHoldsTheWriteLock_RejectsAsUnavailableAfterTheConfiguredWait()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var store = database.Open(settings: new SqliteEvaluationStoreSettings(TimeSpan.FromSeconds(1), 1_048_576));
        await store.InitializeAsync(Token);
        using var holder = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database.Path, Pooling = false }.ToString());
        holder.Open();
        using var transaction = holder.BeginTransaction(deferred: false);

        var answer = await store.AppendAsync(EvaluationResultConformanceData.Result(EvaluationResultConformanceData.NewRun()), Token);

        answer.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.Unavailable);
        transaction.Rollback();
    }

    [Fact]
    public async Task AppendAsync_WhenTwoStoreInstancesShareTheFile_AppendsAtomicallyAcrossThem()
    {
        using var database = new SqliteEvaluationTestDatabase();
        var first = database.Open();
        var second = database.Open();
        var run = EvaluationResultConformanceData.NewRun();

        var answers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
            await Task.Run(async () => await (index % 2 == 0 ? first : second).AppendAsync(EvaluationResultConformanceData.Result(run), Token), Token)));

        answers.Count(static a => a is EvaluationStoreAppended { Replayed: false }).ShouldBe(1);
        answers.Count(static a => a is EvaluationStoreAppended { Replayed: true }).ShouldBe(19);
        database.Execute("SELECT count(*) FROM evaluation_results", scalar: true).ShouldBe(1L);
    }
}
