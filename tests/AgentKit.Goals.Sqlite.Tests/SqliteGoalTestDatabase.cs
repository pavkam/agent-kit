// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite.Tests;

/// <summary>Owns one temporary database path for a test case and removes it afterwards.</summary>
public sealed class SqliteGoalTestDatabase: IDisposable
{
    private static readonly SqliteGoalStoreInstanceId _id = new(Guid.Parse("66666666-7777-8888-9999-000000000000"));
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agentkit-goals-sqlite-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a fresh unique directory for the database file.</summary>
    public SqliteGoalTestDatabase() => _ = Directory.CreateDirectory(_directory);

    /// <summary>Gets the database file path.</summary>
    public string Path => System.IO.Path.Combine(_directory, "goals.db");

    /// <summary>Builds a target that creates the database and schema when missing.</summary>
    public SqliteGoalStoreTarget Target(SqliteGoalStoreInstanceId? id = null, SqliteDatabaseOpenMode open = SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode schema = SqliteSchemaMode.ApplyKnownMigrations) =>
        new(Path, id ?? _id, open, schema);

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
