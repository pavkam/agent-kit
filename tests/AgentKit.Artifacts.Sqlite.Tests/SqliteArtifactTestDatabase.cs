// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Owns a unique temporary directory of SQLite artifact database files and builds targets over it.</summary>
public sealed class SqliteArtifactTestDatabase: IDisposable
{
    private static readonly SqliteArtifactInstanceId _id = new(Guid.Parse("66666666-7777-8888-9999-000000000001"));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "agentkit-artifacts-sqlite-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a fresh unique directory for the database files.</summary>
    public SqliteArtifactTestDatabase() => _ = Directory.CreateDirectory(_directory);

    /// <summary>Gets the path of one named database file.</summary>
    /// <param name="name">The database name.</param>
    /// <returns>The database file path.</returns>
    public string PathOf(string name = "store") => Path.Combine(_directory, name + ".db");

    /// <summary>Builds a target that creates the database and schema when missing.</summary>
    /// <param name="name">The database name.</param>
    /// <param name="id">The instance identity, or the shared test identity.</param>
    /// <param name="open">Whether a missing database may be created.</param>
    /// <param name="schema">Whether a missing schema may be created.</param>
    /// <returns>The target.</returns>
    public SqliteArtifactTarget Target(string name = "store", SqliteArtifactInstanceId? id = null, SqliteDatabaseOpenMode open = SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode schema = SqliteSchemaMode.ApplyKnownMigrations) =>
        new(PathOf(name), id ?? _id, open, schema);

    /// <inheritdoc/>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
