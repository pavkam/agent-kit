// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Supplies an isolated SQLite memory store to the shared conformance suite.</summary>
public sealed class SqliteMemoryStoreConformanceFixture: IMemoryStoreConformanceFixture, IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();

    /// <summary>Initializes the isolated composition.</summary>
    public SqliteMemoryStoreConformanceFixture() => Store = Open();

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IMemoryStore Store { get; private set; }

    /// <inheritdoc/>
    public ValueTask<IMemoryStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        Store = Open();
        return ValueTask.FromResult(Store);
    }

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    private SqliteMemoryStore Open() => new(new MemoryStoreKey("memory"), _database.Target(), SqliteMemorySettings.CreateDefault(), Grants, new TestIntentIds(), TimeProvider.System);
}
