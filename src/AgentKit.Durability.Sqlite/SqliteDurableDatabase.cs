// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Owns the exact schema, connection policy, and integrity validation shared by one SQLite durable store.</summary>
/// <remarks>
/// <para>
/// The journal and the lease manager are separate contracts but one database, because a fencing token only fences a
/// write when both are allocated and honored under the same atomic store. This boundary is what they share: it creates
/// or validates the exact version-one schema, verifies the persistent store identity on every connection, and hands out
/// connections configured for durable local writes.
/// </para>
/// <para>
/// The boundary retains no connection. Every operation opens, validates, and closes one, which keeps SQLite's
/// single-writer semantics visible rather than hidden behind a shared handle. Initialization is idempotent and is
/// performed once by each owning store during trusted host bootstrap.
/// </para>
/// <para>
/// Applications construct one instance per durable store and hand the same instance to both the journal and the lease
/// manager registration, which is how the composition states that those two contracts share one atomic store.
/// </para>
/// </remarks>
public sealed class SqliteDurableDatabase
{
    internal const string OpenFailed = "open_failed";
    internal const string CorruptEvidence = "corrupt_evidence";
    internal const string SchemaUnsupported = "schema_unsupported";
    internal const string PersistenceFailed = "persistence_failed";

    private const int _applicationId = 0x414B4455;
    private const int _schemaVersion = 1;
    private const string _failureKindKey = "agentkit.failure_kind";
    private const string _metadataTableSql = "CREATE TABLE durable_store_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16))";
    private const string _operationsTableSql = "CREATE TABLE durable_operations (agent_id BLOB NOT NULL CHECK(length(agent_id) = 16), session_id BLOB NOT NULL CHECK(length(session_id) = 16), run_id BLOB NOT NULL CHECK(length(run_id) = 16), operation_id BLOB NOT NULL CHECK(length(operation_id) = 16), turn_id BLOB NOT NULL CHECK(length(turn_id) = 16), state INTEGER NOT NULL, last_writer_token INTEGER NOT NULL CHECK(last_writer_token > 0), projection BLOB NOT NULL, projection_digest BLOB NOT NULL CHECK(length(projection_digest) = 32), PRIMARY KEY(agent_id, session_id, run_id, operation_id, turn_id))";
    private const string _leasesTableSql = "CREATE TABLE durable_leases (agent_id BLOB NOT NULL CHECK(length(agent_id) = 16), session_id BLOB NOT NULL CHECK(length(session_id) = 16), run_id BLOB NOT NULL CHECK(length(run_id) = 16), operation_id BLOB NOT NULL CHECK(length(operation_id) = 16), turn_id BLOB NOT NULL CHECK(length(turn_id) = 16), lease_id BLOB NOT NULL CHECK(length(lease_id) = 16), worker_id BLOB NOT NULL CHECK(length(worker_id) = 16), fencing_token INTEGER NOT NULL CHECK(fencing_token > 0), duration_ticks INTEGER NOT NULL CHECK(duration_ticks > 0), expires_at_ticks INTEGER NOT NULL, expires_at_offset_ticks INTEGER NOT NULL, PRIMARY KEY(agent_id, session_id, run_id, operation_id, turn_id))";
    private const string _sequenceTableSql = "CREATE TABLE durable_lease_sequence (id INTEGER PRIMARY KEY CHECK(id = 1), next_token INTEGER NOT NULL CHECK(next_token >= 0))";
    private const string _expectedTables = "durable_lease_sequence|durable_leases|durable_operations|durable_store_metadata";
    private const string _metadataColumns = "store_id:BLOB:1:0";
    private const string _operationColumns = "agent_id:BLOB:1:1|session_id:BLOB:1:2|run_id:BLOB:1:3|operation_id:BLOB:1:4|turn_id:BLOB:1:5|state:INTEGER:1:0|last_writer_token:INTEGER:1:0|projection:BLOB:1:0|projection_digest:BLOB:1:0";
    private const string _leaseColumns = "agent_id:BLOB:1:1|session_id:BLOB:1:2|run_id:BLOB:1:3|operation_id:BLOB:1:4|turn_id:BLOB:1:5|lease_id:BLOB:1:0|worker_id:BLOB:1:0|fencing_token:INTEGER:1:0|duration_ticks:INTEGER:1:0|expires_at_ticks:INTEGER:1:0|expires_at_offset_ticks:INTEGER:1:0";
    private const string _sequenceColumns = "id:INTEGER:0:1|next_token:INTEGER:1:0";

    private readonly Lock _gate = new();
    private bool _initialized;

    /// <summary>Binds the boundary to one host-authorized target and its bounds without touching the database.</summary>
    /// <param name="target">The non-null exact database and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The non-null immutable lock-wait and evidence bounds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="settings"/> is null.</exception>
    public SqliteDurableDatabase(SqliteDurableStoreTarget target, SqliteDurableStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        Target = target;
        Settings = settings;
    }

    /// <summary>Gets the exact host-authorized target this boundary opens.</summary>
    /// <value>The immutable bootstrap configuration; its path is sensitive and is never logged or tagged.</value>
    public SqliteDurableStoreTarget Target { get; }

    /// <summary>Gets the immutable operational and evidence bounds every operation enforces.</summary>
    /// <value>The lock-wait and encoded-record bounds selected at composition.</value>
    public SqliteDurableStoreSettings Settings { get; }

    /// <summary>Encodes durable coordinates into the exact five-column primary key the schema uses.</summary>
    /// <param name="address">The operation coordinates to encode.</param>
    /// <returns>The five network-order key blobs in schema column order.</returns>
    /// <remarks>
    /// An absent turn is encoded as sixteen zero bytes. That sentinel can never collide with a real turn, because
    /// <see cref="TurnId"/> rejects the empty GUID, so an after-run operation and an in-run operation on the same run
    /// remain distinct rows rather than overwriting one another.
    /// </remarks>
    internal static DurableOperationKey EncodeKey(DurableOperationAddress address) => new(
        SqliteDurableCodec.EncodeGuid(address.AgentId.Value),
        SqliteDurableCodec.EncodeGuid(address.SessionId.Value),
        SqliteDurableCodec.EncodeGuid(address.RunId.Value),
        SqliteDurableCodec.EncodeGuid(address.OperationId.Value),
        SqliteDurableCodec.EncodeGuid(address.TurnId?.Value ?? Guid.Empty));

    /// <summary>Creates a bounded store failure carrying a stable classification for diagnostics.</summary>
    /// <param name="failureKind">The bounded classification recorded in the exception's data.</param>
    /// <param name="message">The content-free safe failure description.</param>
    /// <param name="inner">The originating provider exception, when one exists.</param>
    /// <returns>An exception whose data carries the classification without exposing the target path.</returns>
    internal static InvalidOperationException Unavailable(
        string failureKind,
        string message,
        Exception? inner = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(failureKind), "A bounded failure classification is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        var exception = new InvalidOperationException(message, inner);
        exception.Data[_failureKindKey] = failureKind;
        return exception;
    }

    /// <summary>Creates or validates the exact version-one schema and persistent store identity under bootstrap policy.</summary>
    /// <param name="cancellationToken">Cancels before the schema transaction commits or before a permitted journal-mode change begins.</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the applicable bootstrap linearization point.</exception>
    /// <exception cref="InvalidOperationException">The target, schema, store identity, encoding contract, or persistence provider cannot be validated safely.</exception>
    /// <remarks>Repeated initialization after the first success is a no-op, so the journal and the lease manager may each initialize the database they share.</remarks>
    internal void Initialize(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SqliteDurableCodec.VerifyRoundTrip();
            ValidateTargetBeforeOpen(allowMissingFile: Target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
            using var connection = Open(forInitialization: true);
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsUninitialized(connection, transaction))
            {
                if (Target.OpenMode != SqliteDatabaseOpenMode.CreateIfMissing
                    || Target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                {
                    throw Unavailable(SchemaUnsupported, "The SQLite target has no initialized durable-store schema.");
                }

                CreateSchema(connection, transaction, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                SetWalMode(connection);
                Validate(connection, requireWal: true, performIntegrityCheck: true);
                _initialized = true;
                return;
            }

            Validate(
                connection,
                requireWal: Target.SchemaMode == SqliteSchemaMode.ValidateExact,
                performIntegrityCheck: true,
                transaction);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            if (Target.SchemaMode == SqliteSchemaMode.ValidateExact)
            {
                _initialized = true;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SetWalMode(connection);
            Validate(connection, requireWal: true, performIntegrityCheck: false);
            _initialized = true;
        }
    }

    /// <summary>Throws unless trusted bootstrap already initialized this database.</summary>
    /// <param name="what">The content-free name of the store reporting the violation.</param>
    /// <exception cref="InvalidOperationException">The store was used before trusted bootstrap initialization.</exception>
    internal void RequireInitialized(string what)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(what), "A content-free store name is required.");
        lock (_gate)
        {
            if (!_initialized)
            {
                throw Unavailable(
                    OpenFailed, $"The SQLite durable {what} was used before trusted bootstrap initialization.");
            }
        }
    }

    /// <summary>Opens and validates one connection to the already initialized store.</summary>
    /// <returns>An open connection the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">The target cannot be opened or no longer matches bootstrap configuration.</exception>
    internal SqliteConnection OpenValidated()
    {
        ValidateTargetBeforeOpen(allowMissingFile: false);
        return Open(forInitialization: false);
    }

    /// <summary>Validates the schema, identity, and journal mode of an open connection.</summary>
    /// <param name="connection">The non-null open connection to validate.</param>
    /// <param name="requireWal">Whether write-ahead journaling must already be configured.</param>
    /// <param name="performIntegrityCheck">Whether a bounded integrity check must also pass.</param>
    /// <param name="transaction">The active transaction to run validation inside, when one exists.</param>
    /// <exception cref="InvalidOperationException">The schema, identity, integrity, or journal mode is unsupported.</exception>
    internal void Validate(
        SqliteConnection connection,
        bool requireWal,
        bool performIntegrityCheck,
        SqliteTransaction? transaction = null)
    {
        Debug.Assert(connection is not null, "An open connection is required.");
        if (ExecuteInt64(connection, transaction, "PRAGMA application_id;") != _applicationId
            || ExecuteInt64(connection, transaction, "PRAGMA user_version;") != _schemaVersion)
        {
            throw Unavailable(SchemaUnsupported, "The SQLite durable-store schema is unsupported.");
        }

        ValidateSchemaShape(connection, transaction);
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT store_id FROM durable_store_metadata LIMIT 2;";
            using var reader = command.ExecuteReader();
            if (!reader.Read()
                || SqliteDurableCodec.DecodeGuid(ReadBoundedBlob(reader, 0, 16)) != Target.ExpectedStoreInstanceId.Value
                || reader.Read())
            {
                throw Unavailable(
                    CorruptEvidence, "The SQLite durable-store identity does not match bootstrap configuration.");
            }
        }

        if (performIntegrityCheck
            && !string.Equals(
                Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA quick_check(1);"), CultureInfo.InvariantCulture),
                "ok",
                StringComparison.Ordinal))
        {
            throw Unavailable(
                CorruptEvidence, "The SQLite durable-store integrity check failed during bootstrap validation.");
        }

        if (requireWal
            && !string.Equals(
                Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture),
                "wal",
                StringComparison.OrdinalIgnoreCase))
        {
            throw Unavailable(SchemaUnsupported, "The SQLite durable store is not configured for WAL journaling.");
        }
    }

    /// <summary>Reads one bounded binary column, refusing an empty or oversized value.</summary>
    /// <param name="reader">The non-null active reader positioned on a row.</param>
    /// <param name="ordinal">The zero-based column ordinal.</param>
    /// <param name="maximumLength">The positive inclusive maximum accepted length.</param>
    /// <returns>The exact persisted bytes.</returns>
    /// <exception cref="InvalidOperationException">The persisted value is empty or exceeds its configured bound.</exception>
    internal static byte[] ReadBoundedBlob(SqliteDataReader reader, int ordinal, int maximumLength)
    {
        Debug.Assert(reader is not null, "An active reader is required.");
        var length = reader.GetBytes(ordinal, 0, null, 0, 0);
        if (length <= 0 || length > maximumLength)
        {
            throw Unavailable(CorruptEvidence, "Persisted durable evidence exceeds its configured bound.");
        }

        var payload = new byte[checked((int) length)];
        _ = reader.GetBytes(ordinal, 0, payload, 0, payload.Length);
        return payload;
    }

    /// <summary>Verifies a persisted payload against its recorded digest.</summary>
    /// <param name="payload">The persisted payload bytes.</param>
    /// <param name="digest">The persisted digest bytes.</param>
    /// <exception cref="InvalidOperationException">The digest does not match the payload.</exception>
    internal static void VerifyDigest(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> digest)
    {
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), digest))
        {
            throw Unavailable(CorruptEvidence, "Persisted durable payload digest is inconsistent.");
        }
    }

    private void CreateSchema(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null && transaction is not null, "An open initialization transaction is required.");
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteNonQuery(
            connection, transaction,
            $"{_metadataTableSql};{_operationsTableSql};{_leasesTableSql};{_sequenceTableSql};");
        using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = transaction;
            metadata.CommandText = "INSERT INTO durable_store_metadata(store_id) VALUES ($id); INSERT INTO durable_lease_sequence(id, next_token) VALUES (1, 0);";
            _ = metadata.Parameters.AddWithValue("$id", SqliteDurableCodec.EncodeGuid(Target.ExpectedStoreInstanceId.Value));
            _ = metadata.ExecuteNonQuery();
        }

        ExecuteNonQuery(
            connection, transaction,
            $"PRAGMA application_id = {_applicationId}; PRAGMA user_version = {_schemaVersion};");
    }

    private SqliteConnection Open(bool forInitialization)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Target.DatabasePath,
            Mode = forInitialization && Target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing
                ? SqliteOpenMode.ReadWriteCreate
                : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            ForeignKeys = true,
            DefaultTimeout = checked((int) Settings.LockTimeout.TotalSeconds),
        };
        var connection = new SqliteConnection(builder.ConnectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL; PRAGMA temp_store = MEMORY;";
            _ = command.ExecuteNonQuery();
            return connection;
        }
        catch (SqliteException exception)
        {
            connection.Dispose();
            throw Unavailable(OpenFailed, "The SQLite durable-store target could not be opened.", exception);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void ValidateTargetBeforeOpen(bool allowMissingFile)
    {
        var parent = Path.GetDirectoryName(Target.DatabasePath);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            throw Unavailable(OpenFailed, "The SQLite target parent directory does not exist.");
        }

        if (!File.Exists(Target.DatabasePath) && !allowMissingFile)
        {
            throw Unavailable(OpenFailed, "The SQLite durable-store target does not exist.");
        }
    }

    private static bool IsUninitialized(SqliteConnection connection, SqliteTransaction transaction) =>
        ExecuteInt64(connection, transaction, "PRAGMA application_id;") == 0
        && ExecuteInt64(connection, transaction, "PRAGMA user_version;") == 0
        && ExecuteInt64(
            connection,
            transaction,
            "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('table','index','trigger','view') AND name NOT LIKE 'sqlite_%';") == 0;

    private static void ValidateSchemaShape(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var tables = Convert.ToString(
            ExecuteScalar(connection, transaction, """
                SELECT group_concat(name, '|') FROM
                    (SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name);
                """),
            CultureInfo.InvariantCulture);
        if (!string.Equals(tables, _expectedTables, StringComparison.Ordinal)
            || ExecuteInt64(
                connection,
                transaction,
                "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('index','trigger','view') AND name NOT LIKE 'sqlite_autoindex_%';") != 0
            || !string.Equals(ReadSchemaSql(connection, transaction, "durable_store_metadata"), _metadataTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "durable_operations"), _operationsTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "durable_leases"), _leasesTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "durable_lease_sequence"), _sequenceTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "durable_store_metadata"), _metadataColumns, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "durable_operations"), _operationColumns, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "durable_leases"), _leaseColumns, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "durable_lease_sequence"), _sequenceColumns, StringComparison.Ordinal))
        {
            throw Unavailable(SchemaUnsupported, "The SQLite durable-store schema shape or integrity is unsupported.");
        }
    }

    private static string? ReadSchemaSql(SqliteConnection connection, SqliteTransaction? transaction, string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = $name;";
        _ = command.Parameters.AddWithValue("$name", table);
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)?.Trim();
    }

    private static string? ReadColumnShape(SqliteConnection connection, SqliteTransaction? transaction, string table)
    {
        Debug.Assert(table.All(char.IsLetterOrDigit) || table.Contains('_', StringComparison.Ordinal), "Only package-owned literal table names are inspected.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT group_concat(shape, '|') FROM (SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk AS shape FROM pragma_table_info('{table}') ORDER BY cid);";
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void SetWalMode(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL;";
        _ = command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static long ExecuteInt64(SqliteConnection connection, SqliteTransaction? transaction, string sql) =>
        Convert.ToInt64(ExecuteScalar(connection, transaction, sql), CultureInfo.InvariantCulture);

    private static object? ExecuteScalar(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
