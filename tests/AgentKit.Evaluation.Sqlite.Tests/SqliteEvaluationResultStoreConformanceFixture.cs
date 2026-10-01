// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

/// <summary>Composes a <see cref="SqliteEvaluationResultStore"/> over a temporary database through its public registration for the shared suite.</summary>
public sealed class SqliteEvaluationResultStoreConformanceFixture: IEvaluationResultStoreConformanceFixture
{
    private readonly SqliteEvaluationTestDatabase _database = new();
    private ServiceProvider _provider;

    /// <summary>Initializes the isolated composition.</summary>
    public SqliteEvaluationResultStoreConformanceFixture()
    {
        _provider = Compose();
        Store = Resolve();
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public IEvaluationResultStore Store { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<IEvaluationResultStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        await _provider.DisposeAsync();
        _provider = Compose();
        Store = Resolve();
        return Store;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        _database.Dispose();
    }

    private ServiceProvider Compose() =>
        new ServiceCollection().AddSqliteEvaluationResultStore(new EvaluationResultStoreKey("sqlite"), _database.Target()).BuildServiceProvider();

    private IEvaluationResultStore Resolve() => _provider.GetRequiredKeyedService<IEvaluationResultStore>("sqlite");
}
