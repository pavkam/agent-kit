// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Owns one unique temporary directory whose database files serve as independent SQLite store databases.</summary>
public sealed class SqliteMemoryTestDatabase: IDisposable
{
    private static readonly SqliteMemoryInstanceId _id = new(Guid.Parse("66666666-7777-8888-9999-000000000000"));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "agentkit-memory-sqlite-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a fresh unique directory for the database files.</summary>
    public SqliteMemoryTestDatabase() => _ = Directory.CreateDirectory(_directory);

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
    public SqliteMemoryTarget Target(string name = "store", SqliteMemoryInstanceId? id = null, SqliteDatabaseOpenMode open = SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode schema = SqliteSchemaMode.ApplyKnownMigrations) =>
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
