// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Composes an isolated SQLite reference-commit intent store over a temporary database through its normal registration.</summary>
public sealed class SqliteArtifactReferenceCommitIntentStoreConformanceFixture: IArtifactReferenceCommitIntentStoreConformanceFixture
{
    private ServiceProvider? _provider;
    private SqliteArtifactReferenceCommitIntentStore? _reopened;

    /// <summary>Gets the database the store under test uses.</summary>
    internal SqliteArtifactTestDatabase Database { get; } = new();

    /// <summary>Closes the current store, releasing its exclusive lock, and opens a fresh one over the same database file.</summary>
    /// <returns>The reopened store, which shares no in-process state with the closed one.</returns>
    internal SqliteArtifactReferenceCommitIntentStore Reopen()
    {
        _provider?.Dispose();
        _provider = null;
        _reopened?.Dispose();
        _reopened = new SqliteArtifactReferenceCommitIntentStore(Database.Target(), SqliteArtifactSettings.CreateDefault(), TimeProvider.System);
        return _reopened;
    }

    /// <inheritdoc/>
    public ValueTask<IArtifactReferenceCommitIntentStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _provider ??= new ServiceCollection()
            .AddSqliteArtifactReferenceCommitIntentStore(Database.Target())
            .BuildServiceProvider();
        return ValueTask.FromResult(_provider.GetRequiredService<IArtifactReferenceCommitIntentStore>());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        _reopened?.Dispose();
        Database.Dispose();
    }
}
