// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite;

/// <summary>Owns the schema, identity binding, and connection policy of one SQLite evaluation result database.</summary>
/// <remarks>
/// A connection is opened per operation and never pooled, so no file handle outlives its transaction. Writes run in an immediate
/// transaction, which takes the database write lock before the first read, so run pinning, idempotent replay, and the identity
/// checks are atomic across processes that share the file.
/// </remarks>
internal sealed class SqliteEvaluationStoreDatabase
{
    private const int _schemaVersion = 1;
    private const string _metadataSql = "CREATE TABLE evaluation_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), schema_version INTEGER NOT NULL)";
    private const string _runsSql = "CREATE TABLE evaluation_runs (run_id BLOB NOT NULL PRIMARY KEY CHECK(length(run_id) = 16), plan_id TEXT NOT NULL, plan_version INTEGER NOT NULL)";
    private const string _resultsSql = "CREATE TABLE evaluation_results (run_id BLOB NOT NULL, case_ordinal INTEGER NOT NULL, repetition INTEGER NOT NULL, case_id TEXT NOT NULL, document TEXT NOT NULL, PRIMARY KEY(run_id, case_ordinal, repetition))";
    private const string _expectedTables = "evaluation_metadata|evaluation_results|evaluation_runs";

    private readonly Lock _gate = new();
    private bool _initialized;

    /// <summary>Initializes a database bound to one host-authorized target without opening it.</summary>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    internal SqliteEvaluationStoreDatabase(SqliteEvaluationStoreTarget target, SqliteEvaluationStoreSettings settings)
    {
        Debug.Assert(target is not null, "The store validates its target before creating the database.");
        Debug.Assert(settings is not null, "The store validates its settings before creating the database.");
        Target = target;
        Settings = settings;
    }

    /// <summary>Gets the bound target.</summary>
    internal SqliteEvaluationStoreTarget Target { get; }

    /// <summary>Gets the lock and size bounds.</summary>
    internal SqliteEvaluationStoreSettings Settings { get; }

    /// <summary>Encodes a GUID as its stable 16-byte database form.</summary>
    /// <param name="value">The GUID.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Encode(Guid value) => value.ToByteArray();

    /// <summary>Creates the exception reported for an unusable database.</summary>
    /// <param name="message">The content-free message.</param>
    /// <param name="inner">The cause, if any.</param>
    /// <returns>The exception to throw.</returns>
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
                throw Unavailable("The configured SQLite evaluation database does not exist.");
            }

            if (Path.GetDirectoryName(Target.DatabasePath) is { } directory && !Directory.Exists(directory))
            {
                throw Unavailable("The configured SQLite evaluation database directory does not exist.");
            }

            try
            {
                using var connection = Open(create: Target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
                using var transaction = connection.BeginTransaction(deferred: false);
                var tables = ReadTables(connection, transaction);
                if (tables.Length == 0)
                {
                    if (Target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                    {
                        throw Unavailable("The SQLite target has no initialized evaluation schema.");
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
                    throw Unavailable("The SQLite evaluation schema does not match the supported layout.");
                }

                ValidateIdentity(connection, transaction);
                transaction.Commit();
                _initialized = true;
            }
            catch (SqliteException exception)
            {
                throw Unavailable("The SQLite evaluation database could not be opened.", exception);
            }
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
            _runsSql,
            _resultsSql,
            "CREATE INDEX evaluation_results_case ON evaluation_results(run_id, case_id)",
        })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            _ = command.ExecuteNonQuery();
        }

        using var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = "INSERT INTO evaluation_metadata(store_id, schema_version) VALUES ($id, $version)";
        _ = metadata.Parameters.AddWithValue("$id", Encode(Target.ExpectedInstanceId.Value));
        _ = metadata.Parameters.AddWithValue("$version", _schemaVersion);
        _ = metadata.ExecuteNonQuery();
    }

    private void ValidateIdentity(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT store_id, schema_version FROM evaluation_metadata";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw Unavailable("The SQLite evaluation database carries no identity.");
        }

        var identity = (byte[]) reader["store_id"];
        if (!identity.AsSpan().SequenceEqual(Encode(Target.ExpectedInstanceId.Value)))
        {
            throw Unavailable("The SQLite evaluation database identity does not match bootstrap configuration.");
        }

        if (reader.GetInt32(1) != _schemaVersion || reader.Read())
        {
            throw Unavailable("The SQLite evaluation database schema version is unsupported.");
        }
    }
}
