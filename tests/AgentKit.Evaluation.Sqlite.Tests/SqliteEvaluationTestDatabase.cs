// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

/// <summary>Owns one temporary database path for a test case and removes it afterwards.</summary>
public sealed class SqliteEvaluationTestDatabase: IDisposable
{
    private static readonly SqliteEvaluationStoreInstanceId _id = new(Guid.Parse("66666666-7777-8888-9999-000000000000"));
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agentkit-evaluation-sqlite-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates a fresh unique directory for the database file.</summary>
    public SqliteEvaluationTestDatabase() => _ = Directory.CreateDirectory(_directory);

    /// <summary>Gets the database file path.</summary>
    public string Path => System.IO.Path.Combine(_directory, "evaluation.db");

    /// <summary>Builds a target that creates the database and schema when missing.</summary>
    public SqliteEvaluationStoreTarget Target(SqliteEvaluationStoreInstanceId? id = null, SqliteDatabaseOpenMode open = SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode schema = SqliteSchemaMode.ApplyKnownMigrations) =>
        new(Path, id ?? _id, open, schema);

    /// <summary>Opens a store over the database.</summary>
    public SqliteEvaluationResultStore Open(SqliteEvaluationStoreTarget? target = null, SqliteEvaluationStoreSettings? settings = null) =>
        new(target ?? Target(), settings ?? SqliteEvaluationStoreSettings.CreateDefault(), TimeProvider.System);

    /// <summary>Runs one raw statement against the database, for tests that corrupt or inspect state.</summary>
    public object? Execute(string sql, bool scalar = false)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return scalar ? command.ExecuteScalar() : command.ExecuteNonQuery();
    }

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
