// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Supplies an isolated SQLite document store to the shared conformance suite.</summary>
public sealed class SqliteDocumentStoreConformanceFixture: IDocumentStoreConformanceFixture, IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();

    /// <summary>Initializes the isolated composition.</summary>
    public SqliteDocumentStoreConformanceFixture() => Store = Open();

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IDocumentStore Store { get; private set; }

    /// <inheritdoc/>
    public ValueTask<IDocumentStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        Store = Open();
        return ValueTask.FromResult(Store);
    }

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    private SqliteDocumentStore Open() => new(new DocumentStoreKey("documents"), _database.Target(), SqliteMemorySettings.CreateDefault(), Grants, new TestIntentIds(), TimeProvider.System);
}
