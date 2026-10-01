// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Owns the schema, identity binding, and exclusive connection of one SQLite artifact database.</summary>
/// <remarks>
/// The connection is opened once, takes SQLite's exclusive lock on its first transaction, and keeps it until disposal, so a second
/// process or store is refused instead of diverging from the in-memory projection this process replays from the database. The
/// owning backend serializes every call, so the connection is never used concurrently.
/// </remarks>
internal sealed class SqliteArtifactDatabase: IDisposable
{
    private const int _schemaVersion = 1;

    private readonly SqliteArtifactSchema _schema;
    private SqliteConnection? _connection;

    internal SqliteArtifactDatabase(SqliteArtifactTarget target, SqliteArtifactSettings settings, SqliteArtifactSchema schema)
    {
        Debug.Assert(target is not null, "The store validates its target before creating the database.");
        Debug.Assert(settings is not null, "The store validates its settings before creating the database.");
        Debug.Assert(schema is not null, "The store names the schema it owns.");
        Target = target;
        Settings = settings;
        _schema = schema;
    }

    internal SqliteArtifactTarget Target { get; }

    internal SqliteArtifactSettings Settings { get; }

    /// <summary>Gets the exclusive connection once <see cref="Open"/> succeeded.</summary>
    /// <exception cref="InvalidOperationException">The database was not opened.</exception>
    internal SqliteConnection Connection => _connection ?? throw new InvalidOperationException("The SQLite artifact database is not open.");

    /// <summary>Creates or validates the schema and binds the database to its expected identity, holding it exclusively.</summary>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance, has an unsupported schema, or is held by another process.</exception>
    internal void Open()
    {
        if (_connection is not null)
        {
            return;
        }

        if (!File.Exists(Target.DatabasePath) && Target.OpenMode != SqliteDatabaseOpenMode.CreateIfMissing)
        {
            throw Unavailable($"The configured SQLite {_schema.DisplayName} database does not exist.");
        }

        if (Path.GetDirectoryName(Target.DatabasePath) is { } directory && !Directory.Exists(directory))
        {
            throw Unavailable($"The configured SQLite {_schema.DisplayName} database directory does not exist.");
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Target.DatabasePath,
            Mode = Target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false,
            DefaultTimeout = (int) Settings.LockTimeout.TotalSeconds,
        };
        var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();
            using (var exclusive = connection.CreateCommand())
            {
                exclusive.CommandText = "PRAGMA locking_mode = EXCLUSIVE";
                _ = exclusive.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction(deferred: false);
            var tables = ReadTables(connection, transaction);
            if (tables.Length == 0)
            {
                if (Target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                {
                    throw Unavailable($"The SQLite target has no initialized {_schema.DisplayName} schema.");
                }

                CreateSchema(connection, transaction);
            }
            else
            {
                if (!string.Equals(string.Join('|', tables), _schema.ExpectedTables, StringComparison.Ordinal))
                {
                    throw Unavailable($"The SQLite {_schema.DisplayName} schema does not match the supported layout.");
                }

                ValidateIdentity(connection, transaction);
            }

            transaction.Commit();
        }
        catch (SqliteException exception)
        {
            connection.Dispose();
            throw Unavailable($"The SQLite {_schema.DisplayName} database could not be opened exclusively.", exception);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        _connection = connection;
    }

    /// <summary>Closes the connection and releases the exclusive database lock.</summary>
    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }

    private static InvalidOperationException Unavailable(string message, Exception? inner = null) => new(message, inner);

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
        foreach (var statement in _schema.Statements)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            _ = command.ExecuteNonQuery();
        }

        using var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = "INSERT INTO artifact_metadata(store_id, schema_version) VALUES ($id, $version)";
        _ = metadata.Parameters.AddWithValue("$id", Target.ExpectedInstanceId.Value.ToByteArray());
        _ = metadata.Parameters.AddWithValue("$version", _schemaVersion);
        _ = metadata.ExecuteNonQuery();
    }

    private void ValidateIdentity(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT store_id, schema_version FROM artifact_metadata";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw Unavailable($"The SQLite {_schema.DisplayName} database carries no identity.");
        }

        var identity = (byte[]) reader["store_id"];
        if (!identity.AsSpan().SequenceEqual(Target.ExpectedInstanceId.Value.ToByteArray()))
        {
            throw Unavailable($"The SQLite {_schema.DisplayName} database identity does not match bootstrap configuration.");
        }

        if (reader.GetInt32(1) != _schemaVersion || reader.Read())
        {
            throw Unavailable($"The SQLite {_schema.DisplayName} database schema version is unsupported.");
        }
    }
}
