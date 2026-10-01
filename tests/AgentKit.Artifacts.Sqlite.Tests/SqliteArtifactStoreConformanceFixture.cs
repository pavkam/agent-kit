// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Composes an isolated SQLite artifact store over a temporary database and the shared deterministic grant authority.</summary>
public sealed class SqliteArtifactStoreConformanceFixture: ArtifactStoreConformanceFixtureBase
{
    private SqliteArtifactStore? _current;

    /// <summary>Gets the database the store under test uses.</summary>
    internal SqliteArtifactTestDatabase Database { get; } = new();

    /// <summary>Gets the authoritative grant store shared with every store this fixture opens.</summary>
    internal Permissions.InMemory.InMemorySecurityGrantStore GrantStore => Grants;

    /// <summary>Gets the deterministic clock shared with every store this fixture opens.</summary>
    internal TimeProvider ClockProvider => Clock;

    /// <summary>Closes the current store, releasing its exclusive lock, and opens a fresh one over the same database file.</summary>
    /// <returns>The reopened store, which shares no in-process state with the closed one.</returns>
    internal SqliteArtifactStore Reopen()
    {
        _current?.Dispose();
        _current = NewStore();
        return _current;
    }

    /// <inheritdoc/>
    protected override IArtifactStore CreateStore() => _current = NewStore();

    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        _current?.Dispose();
        Database.Dispose();
        return ValueTask.CompletedTask;
    }

    private SqliteArtifactStore NewStore() => new(
        Database.Target(), SqliteArtifactSettings.CreateDefault(), Grants, new SequentialIntentIds(), Clock);
}
