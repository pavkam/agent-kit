// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Supplies isolated SQLite vector indexes to the shared conformance suite.</summary>
public sealed class SqliteVectorIndexConformanceFixture: IVectorIndexConformanceFixture, IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();
    private int _created;

    /// <summary>Initializes the isolated composition.</summary>
    public SqliteVectorIndexConformanceFixture() => Index = Open("default", MemoryTestData.Space());

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IVectorIndex Index { get; private set; }

    /// <inheritdoc/>
    public IVectorIndex CreateIndex(VectorSpaceDescriptor space) => Open($"extra-{++_created}", space);

    /// <inheritdoc/>
    public ValueTask<IVectorIndex> ReopenAsync(CancellationToken cancellationToken = default)
    {
        Index = Open("default", MemoryTestData.Space());
        return ValueTask.FromResult(Index);
    }

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    private SqliteVectorIndex Open(string name, VectorSpaceDescriptor space) =>
        new(space, _database.Target(name), SqliteMemorySettings.CreateDefault(), Grants, new TestIntentIds(), TimeProvider.System);
}
