// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddSqliteEvaluationResultStore_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        using var database = new SqliteEvaluationTestDatabase();

        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddSqliteEvaluationResultStore(null!, new EvaluationResultStoreKey("k"), database.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteEvaluationResultStore(new EvaluationResultStoreKey("k"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddSqliteEvaluationResultStore(default, database.Target())).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddSqliteEvaluationResultStore_WhenAConfiguredBoundIsInvalid_FailsAtRegistration()
    {
        using var database = new SqliteEvaluationTestDatabase();

        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddSqliteEvaluationResultStore(new EvaluationResultStoreKey("k"), database.Target(), static o => o.MaximumRecordBytes = 0))
            .ParamName.ShouldBe("maximumRecordBytes");
    }

    [Fact]
    public async Task AddSqliteEvaluationResultStore_WhenResolved_ReturnsOneKeyedSingletonThatOpensLazily()
    {
        using var database = new SqliteEvaluationTestDatabase();
        await using var provider = new ServiceCollection()
            .AddSqliteEvaluationResultStore(new EvaluationResultStoreKey("sqlite"), database.Target())
            .AddSqliteEvaluationResultStore(new EvaluationResultStoreKey("sqlite"), database.Target())
            .BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IEvaluationResultStore>("sqlite");

        _ = store.ShouldBeOfType<SqliteEvaluationResultStore>();
        provider.GetRequiredKeyedService<IEvaluationResultStore>("sqlite").ShouldBeSameAs(store);
        File.Exists(database.Path).ShouldBeFalse();
        _ = await store.ReadAsync(new EvaluationResultQuery(EvaluationResultConformanceData.NewRun(), 1), TestContext.Current.CancellationToken);
        File.Exists(database.Path).ShouldBeTrue();
    }
}
