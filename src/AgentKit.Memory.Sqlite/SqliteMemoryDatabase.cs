// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Owns the schema, identity binding, and connection policy of one SQLite memory, document, or vector database.</summary>
/// <remarks>
/// A connection is opened per operation and never pooled, so no file handle outlives its transaction. Writes run in an
/// immediate transaction, which takes the database write lock before the first read, so the version check, idempotent replay,
/// sequence allocation, and deletion generation are atomic across processes that share the file.
/// </remarks>
internal sealed class SqliteMemoryDatabase
{
    private const int _schemaVersion = 1;

    private readonly Lock _gate = new();
    private readonly MemoryStoreFamily _family;
    private bool _initialized;

    internal SqliteMemoryDatabase(SqliteMemoryTarget target, SqliteMemorySettings settings, MemoryStoreFamily family)
    {
        Debug.Assert(target is not null, "The store validates its target before creating the database.");
        Debug.Assert(settings is not null, "The store validates its settings before creating the database.");
        Target = target;
        Settings = settings;
        _family = family;
    }

    internal SqliteMemoryTarget Target { get; }

    internal SqliteMemorySettings Settings { get; }

    internal static byte[] Encode(Guid value) => value.ToByteArray();

    internal static InvalidOperationException Unavailable(string message, Exception? inner = null) => new(message, inner);

    /// <summary>Creates or validates the schema exactly once and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance or family, or has an unsupported schema.</exception>
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
                throw Unavailable("The configured SQLite memory database does not exist.");
            }

            if (Path.GetDirectoryName(Target.DatabasePath) is { } directory && !Directory.Exists(directory))
            {
                throw Unavailable("The configured SQLite memory database directory does not exist.");
            }

            using var connection = Open(create: Target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
            using var transaction = connection.BeginTransaction(deferred: false);
            var tables = ReadTables(connection, transaction);
            if (tables.Length == 0)
            {
                if (Target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                {
                    throw Unavailable("The SQLite target has no initialized memory schema.");
                }

                CreateSchema(connection, transaction);
                transaction.Commit();
                using var wal = connection.CreateCommand();
                wal.CommandText = "PRAGMA journal_mode = WAL";
                _ = wal.ExecuteScalar();
                _initialized = true;
                return;
            }

            if (!string.Equals(string.Join('|', tables), ExpectedTables(), StringComparison.Ordinal))
            {
                throw Unavailable("The SQLite memory schema does not match the supported layout for this store.");
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

    private string ExpectedTables() => _family switch
    {
        MemoryStoreFamily.Memory => "memories|memory_creations|memory_metadata",
        MemoryStoreFamily.Document => "document_metadata|documents",
        MemoryStoreFamily.Vector => "vector_metadata|vector_receipts|vectors",
        _ => throw new UnreachableException(),
    };

    private string MetadataTable() => _family switch
    {
        MemoryStoreFamily.Memory => "memory_metadata",
        MemoryStoreFamily.Document => "document_metadata",
        MemoryStoreFamily.Vector => "vector_metadata",
        _ => throw new UnreachableException(),
    };

    private string[] SchemaStatements() => _family switch
    {
        MemoryStoreFamily.Memory =>
        [
            "CREATE TABLE memory_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), schema_version INTEGER NOT NULL)",
            "CREATE TABLE memories (tenant TEXT NOT NULL, memory_id BLOB NOT NULL CHECK(length(memory_id) = 16), agent_id BLOB NOT NULL, sequence INTEGER NOT NULL, generation INTEGER NULL, document TEXT NOT NULL, PRIMARY KEY(tenant, memory_id))",
            "CREATE TABLE memory_creations (tenant TEXT NOT NULL, creation_key TEXT NOT NULL, memory_id BLOB NOT NULL, PRIMARY KEY(tenant, creation_key))",
            "CREATE UNIQUE INDEX memories_sequence ON memories(sequence)",
            "CREATE INDEX memories_scan ON memories(tenant, agent_id, sequence)",
        ],
        MemoryStoreFamily.Document =>
        [
            "CREATE TABLE document_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), schema_version INTEGER NOT NULL)",
            "CREATE TABLE documents (tenant TEXT NOT NULL, document_id BLOB NOT NULL CHECK(length(document_id) = 16), sequence INTEGER NOT NULL, generation INTEGER NULL, document TEXT NOT NULL, PRIMARY KEY(tenant, document_id))",
            "CREATE UNIQUE INDEX documents_sequence ON documents(sequence)",
        ],
        MemoryStoreFamily.Vector =>
        [
            "CREATE TABLE vector_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), schema_version INTEGER NOT NULL, watermark INTEGER NOT NULL)",
            "CREATE TABLE vectors (tenant TEXT NOT NULL, chunk_id BLOB NOT NULL CHECK(length(chunk_id) = 16), agent_id BLOB NOT NULL, document TEXT NOT NULL, PRIMARY KEY(tenant, chunk_id))",
            "CREATE INDEX vectors_scan ON vectors(tenant, agent_id)",
            "CREATE TABLE vector_receipts (position INTEGER PRIMARY KEY AUTOINCREMENT, tenant TEXT NOT NULL, receipt_key TEXT NOT NULL, document TEXT NOT NULL, UNIQUE(tenant, receipt_key))",
        ],
        _ => throw new UnreachableException(),
    };

    private void CreateSchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        foreach (var statement in SchemaStatements())
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            _ = command.ExecuteNonQuery();
        }

        using var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = _family == MemoryStoreFamily.Vector
            ? "INSERT INTO vector_metadata(store_id, schema_version, watermark) VALUES ($id, $version, 0)"
            : $"INSERT INTO {MetadataTable()}(store_id, schema_version) VALUES ($id, $version)";
        _ = metadata.Parameters.AddWithValue("$id", Encode(Target.ExpectedInstanceId.Value));
        _ = metadata.Parameters.AddWithValue("$version", _schemaVersion);
        _ = metadata.ExecuteNonQuery();
    }

    private void ValidateIdentity(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT store_id, schema_version FROM {MetadataTable()}";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw Unavailable("The SQLite memory database carries no identity.");
        }

        var identity = (byte[]) reader["store_id"];
        if (!identity.AsSpan().SequenceEqual(Encode(Target.ExpectedInstanceId.Value)))
        {
            throw Unavailable("The SQLite memory database identity does not match bootstrap configuration.");
        }

        if (reader.GetInt32(1) != _schemaVersion || reader.Read())
        {
            throw Unavailable("The SQLite memory database schema version is unsupported.");
        }
    }
}
