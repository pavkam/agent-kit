// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Owns the schema, identity binding, and connection policy of one SQLite goal database.</summary>
/// <remarks>
/// A connection is opened per operation and never pooled, so no file handle outlives its transaction. Writes run in an
/// immediate transaction, which takes the database write lock before the first read, so the optimistic version check,
/// idempotent replay, sequence allocation, and child ordinal are atomic across processes that share the file.
/// </remarks>
internal sealed class SqliteGoalDatabase
{
    internal const string GoalsTable = "goals";

    private const int _schemaVersion = 1;
    private const string _metadataSql = "CREATE TABLE goal_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), schema_version INTEGER NOT NULL)";
    private const string _goalsSql = "CREATE TABLE goals (tenant TEXT NOT NULL, goal_id BLOB NOT NULL CHECK(length(goal_id) = 16), parent_id BLOB NULL, sequence INTEGER NOT NULL, child_ordinal INTEGER NULL, status INTEGER NOT NULL, delegated INTEGER NOT NULL, settled_sequence INTEGER NULL, document TEXT NOT NULL, PRIMARY KEY(tenant, goal_id))";
    private const string _creationsSql = "CREATE TABLE goal_creations (tenant TEXT NOT NULL, creation_key TEXT NOT NULL, goal_id BLOB NOT NULL, PRIMARY KEY(tenant, creation_key))";
    private const string _expectedTables = "goal_creations|goal_metadata|goals";

    private readonly Lock _gate = new();
    private bool _initialized;

    internal SqliteGoalDatabase(SqliteGoalStoreTarget target, SqliteGoalStoreSettings settings)
    {
        Debug.Assert(target is not null, "The store validates its target before creating the database.");
        Debug.Assert(settings is not null, "The store validates its settings before creating the database.");
        Target = target;
        Settings = settings;
    }

    internal SqliteGoalStoreTarget Target { get; }

    internal SqliteGoalStoreSettings Settings { get; }

    internal static byte[] Encode(Guid value) => value.ToByteArray();

    internal static InvalidOperationException Unavailable(string message, Exception? inner = null) => new(message, inner);

    /// <summary>Creates or validates the schema exactly once and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance, or has an unsupported schema.</exception>
    internal void EnsureInitialized(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(Target.DatabasePath) && Target.OpenMode != SqliteDatabaseOpenMode.CreateIfMissing)
            {
                throw Unavailable("The configured SQLite goal database does not exist.");
            }

            if (Path.GetDirectoryName(Target.DatabasePath) is { } directory && !Directory.Exists(directory))
            {
                throw Unavailable("The configured SQLite goal database directory does not exist.");
            }

            using var connection = Open(create: Target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
            using var transaction = connection.BeginTransaction(deferred: false);
            var tables = ReadTables(connection, transaction);
            if (tables.Length == 0)
            {
                if (Target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                {
                    throw Unavailable("The SQLite target has no initialized goal schema.");
                }

                CreateSchema(connection, transaction);
                transaction.Commit();
                using var wal = connection.CreateCommand();
                wal.CommandText = "PRAGMA journal_mode = WAL";
                _ = wal.ExecuteScalar();
                _initialized = true;
                return;
            }

            if (!string.Equals(string.Join('|', tables), _expectedTables, StringComparison.Ordinal))
            {
                throw Unavailable("The SQLite goal schema does not match the supported layout.");
            }

            ValidateIdentity(connection, transaction);
            transaction.Commit();
            _initialized = true;
        }
    }

    /// <summary>Opens one unpooled connection with the configured lock timeout.</summary>
    /// <param name="create">Whether the database file may be created.</param>
    /// <returns>An open connection the caller owns and disposes.</returns>
    internal SqliteConnection Open(bool create = false)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Target.DatabasePath,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false,
            DefaultTimeout = (int) Settings.LockTimeout.TotalSeconds,
        };
        var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static string[] ReadTables(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }

    private void CreateSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        foreach (var statement in new[]
        {
            _metadataSql,
            _goalsSql,
            _creationsSql,
            "CREATE UNIQUE INDEX goals_sequence ON goals(sequence)",
            "CREATE INDEX goals_children ON goals(tenant, parent_id, child_ordinal)",
            "CREATE INDEX goals_open_intents ON goals(delegated, status, sequence)",
        })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            _ = command.ExecuteNonQuery();
        }

        using var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = "INSERT INTO goal_metadata(store_id, schema_version) VALUES ($id, $version)";
        _ = metadata.Parameters.AddWithValue("$id", Encode(Target.ExpectedStoreInstanceId.Value));
        _ = metadata.Parameters.AddWithValue("$version", _schemaVersion);
        _ = metadata.ExecuteNonQuery();
    }

    private void ValidateIdentity(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT store_id, schema_version FROM goal_metadata";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw Unavailable("The SQLite goal database carries no identity.");
        }

        var identity = (byte[]) reader["store_id"];
        if (!identity.AsSpan().SequenceEqual(Encode(Target.ExpectedStoreInstanceId.Value)))
        {
            throw Unavailable("The SQLite goal database identity does not match bootstrap configuration.");
        }

        if (reader.GetInt32(1) != _schemaVersion || reader.Read())
        {
            throw Unavailable("The SQLite goal database schema version is unsupported.");
        }
    }
}
