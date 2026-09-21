// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Persists approval requests and terminal responses in one fixed local SQLite database.</summary>
/// <remarks>
/// The singleton retains no connection. Every operation opens and validates the exact configured store identity and schema,
/// then uses an immediate SQLite transaction for one atomic state transition. SQLite coordinates processes on one host but
/// does not provide distributed fencing. Requests and terminal responses are retained indefinitely. Call
/// <see cref="InitializeAsync"/> explicitly during trusted bootstrap.
/// </remarks>
public sealed partial class SqliteApprovalStore: IApprovalStore
{
    private const int _applicationId = 0x414B5041;
    private const int _schemaVersion = 1;
    private const string _metadataTableSql = "CREATE TABLE security_store_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16))";
    private const string _requestsTableSql = "CREATE TABLE approval_requests (request_id BLOB PRIMARY KEY NOT NULL CHECK(length(request_id) = 16), request_payload BLOB NOT NULL, request_digest BLOB NOT NULL CHECK(length(request_digest) = 32), response_payload BLOB NULL, response_digest BLOB NULL, CHECK ((response_payload IS NULL AND response_digest IS NULL) OR (response_payload IS NOT NULL AND response_digest IS NOT NULL AND length(response_digest) = 32)))";
    private readonly SqliteApprovalStoreTarget _target;
    private readonly SqliteApprovalStoreSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteApprovalStore> _logger;
    private readonly Lock _gate = new();
    private bool _initialized;

    /// <summary>Initializes a store for one host-authorized fixed target without opening or creating it.</summary>
    /// <param name="target">The exact database and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable lock-wait and evidence bounds.</param>
    /// <param name="timeProvider">The clock used for operation duration diagnostics.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public SqliteApprovalStore(
        SqliteApprovalStoreTarget target,
        SqliteApprovalStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<SqliteApprovalStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<SqliteApprovalStore>.Instance;
    }

    /// <inheritdoc/>
    /// <value>Durable, trusted-control-plane capabilities backed by local SQLite persistence.</value>
    public ApprovalStoreCapabilities Capabilities { get; } = new(
        IsDurable: true,
        ProvidesTrustedControlPlane: true);

    /// <summary>Creates or validates the exact version-one schema and persistent store identity under bootstrap policy.</summary>
    /// <param name="cancellationToken">Cancels before the schema transaction commits or, for an initialized target, before a permitted journal-mode change begins.</param>
    /// <returns>A task completed after the exact target is ready for approval operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the applicable bootstrap linearization point.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The target, schema, store identity, or persistence provider cannot be validated safely.</exception>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ExecuteVoidAsync(
        "initialize",
        () =>
        {
            lock (_gate)
            {
                if (_initialized)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                ValidateTargetBeforeOpen(allowMissingFile: _target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
                using var connection = OpenConnection(forInitialization: true);
                cancellationToken.ThrowIfCancellationRequested();
                using var transaction = connection.BeginTransaction(deferred: false);
                cancellationToken.ThrowIfCancellationRequested();
                var uninitialized = IsUninitializedDatabase(connection, transaction);
                if (uninitialized)
                {
                    if (_target.OpenMode != SqliteDatabaseOpenMode.CreateIfMissing
                        || _target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                    {
                        throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported,
                            "The SQLite target has no initialized approval-store schema.");
                    }

                    InitializeNewSchema(connection, transaction, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    transaction.Commit();
                    SetWalMode(connection);
                    ValidateConnection(connection, requireWal: true, performIntegrityCheck: true);
                    _initialized = true;
                    return;
                }

                ValidateConnection(connection,
                    requireWal: _target.SchemaMode == SqliteSchemaMode.ValidateExact,
                    performIntegrityCheck: true,
                    transaction);
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                if (_target.SchemaMode == SqliteSchemaMode.ValidateExact)
                {
                    _initialized = true;
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                SetWalMode(connection);
                ValidateConnection(connection, requireWal: true, performIntegrityCheck: false);
                _initialized = true;
            }
        });

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the insert commits.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized or persistence failed.</exception>
    public ValueTask<ApprovalStoreCreateResult> CreateAsync(
        ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync("create", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenStoreConnection();
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var existing = TryReadEntry(connection, transaction, request.Id);
            cancellationToken.ThrowIfCancellationRequested();
            if (existing is not null)
            {
                return existing.Value.Request == request
                    ? ApprovalStoreCreateResult.AlreadyExists
                    : ApprovalStoreCreateResult.Conflict;
            }

            var payload = SqliteApprovalCodec.EncodeRequest(request, _settings);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO approval_requests(request_id, request_payload, request_digest)
                VALUES ($id, $payload, $digest);
                """;
            _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(request.Id.Value));
            _ = command.Parameters.AddWithValue("$payload", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            cancellationToken.ThrowIfCancellationRequested();
            _ = command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return ApprovalStoreCreateResult.Created;
        }, request.Id, null, request.Binding);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the update commits.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized or persistence failed.</exception>
    public ValueTask<ApprovalStoreResolveResult> ResolveAsync(
        ApprovalResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ExecuteAsync("resolve", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenStoreConnection();
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var existing = TryReadEntry(connection, transaction, response.RequestId);
            cancellationToken.ThrowIfCancellationRequested();
            if (existing is null)
            {
                return ApprovalStoreResolveResult.NotFound;
            }

            if (existing.Value.Response is { } terminal)
            {
                return terminal == response
                    ? ApprovalStoreResolveResult.AlreadyResolved
                    : ApprovalStoreResolveResult.Conflict;
            }

            if (existing.Value.Request.Binding != response.Binding)
            {
                return ApprovalStoreResolveResult.Conflict;
            }

            var payload = SqliteApprovalCodec.EncodeResponse(response, _settings);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE approval_requests
                SET response_payload = $payload, response_digest = $digest
                WHERE request_id = $id AND response_payload IS NULL;
                """;
            _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(response.RequestId.Value));
            _ = command.Parameters.AddWithValue("$payload", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            cancellationToken.ThrowIfCancellationRequested();
            if (command.ExecuteNonQuery() != 1)
            {
                throw Unavailable(SecurityGrantStoreFailureKind.PersistenceFailed,
                    "The SQLite approval resolution could not be committed atomically.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return ApprovalStoreResolveResult.Resolved;
        }, response.RequestId, response.Id, response.Binding);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestId"/> is empty.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the read completes.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized or persistence failed.</exception>
    public ValueTask<ApprovalStoreReadResult> ReadAsync(
        ApprovalRequestId requestId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId.Value, Guid.Empty);
        return ExecuteAsync("read", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenStoreConnection();
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var existing = TryReadEntry(connection, transaction, requestId);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return existing is null
                ? new ApprovalStoreReadResult(null, null)
                : new ApprovalStoreReadResult(existing.Value.Request, existing.Value.Response);
        }, requestId);
    }

    private void InitializeNewSchema(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null, "An open initialization connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteNonQuery(connection, transaction, $"{_metadataTableSql};{_requestsTableSql};");
        using var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = "INSERT INTO security_store_metadata(store_id) VALUES ($id);";
        _ = metadata.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(_target.ExpectedStoreInstanceId.Value));
        _ = metadata.ExecuteNonQuery();
        ExecuteNonQuery(connection, transaction, $"PRAGMA application_id = {_applicationId}; PRAGMA user_version = {_schemaVersion};");
    }

    private StoredEntry? TryReadEntry(SqliteConnection connection, SqliteTransaction transaction, ApprovalRequestId requestId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT request_payload, request_digest, response_payload, response_digest
            FROM approval_requests WHERE request_id = $id;
            """;
        _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(requestId.Value));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var requestPayload = ReadBoundedBlob(reader, 0, _settings.MaximumRecordBytes);
        VerifyDigest(requestPayload, ReadBoundedBlob(reader, 1, SHA256.HashSizeInBytes));
        ApprovalResponse? response = null;
        if (!reader.IsDBNull(2))
        {
            var responsePayload = ReadBoundedBlob(reader, 2, _settings.MaximumRecordBytes);
            VerifyDigest(responsePayload, ReadBoundedBlob(reader, 3, SHA256.HashSizeInBytes));
            response = SqliteApprovalCodec.DecodeResponse(responsePayload, _settings);
        }

        return new StoredEntry(SqliteApprovalCodec.DecodeRequest(requestPayload, _settings), response);
    }

    private SqliteConnection OpenStoreConnection()
    {
        ValidateTargetBeforeOpen(allowMissingFile: false);
        return OpenConnection(forInitialization: false);
    }

    private SqliteConnection OpenConnection(bool forInitialization)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _target.DatabasePath,
            Mode = forInitialization && _target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing
                ? SqliteOpenMode.ReadWriteCreate
                : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            ForeignKeys = true,
            DefaultTimeout = checked((int) _settings.LockTimeout.TotalSeconds),
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
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static bool IsUninitializedDatabase(SqliteConnection connection, SqliteTransaction transaction) =>
        ExecuteInt64(connection, transaction, "PRAGMA application_id;") == 0
        && ExecuteInt64(connection, transaction, "PRAGMA user_version;") == 0
        && ExecuteInt64(connection, transaction,
            "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('table','index','trigger','view') AND name NOT LIKE 'sqlite_%';") == 0;

    private void ValidateConnection(
        SqliteConnection connection,
        bool requireWal,
        bool performIntegrityCheck,
        SqliteTransaction? transaction = null)
    {
        if (ExecuteInt64(connection, transaction, "PRAGMA application_id;") != _applicationId
            || ExecuteInt64(connection, transaction, "PRAGMA user_version;") != _schemaVersion)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported, "The SQLite approval-store schema is unsupported.");
        }

        ValidateSchemaShape(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT store_id FROM security_store_metadata LIMIT 2;";
        using var reader = command.ExecuteReader();
        if (!reader.Read()
            || SqliteSecurityGrantCodec.DecodeGuid(ReadBoundedBlob(reader, 0, 16)) != _target.ExpectedStoreInstanceId.Value
            || reader.Read())
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "The SQLite approval-store identity does not match bootstrap configuration.");
        }

        if (performIntegrityCheck
            && !string.Equals(Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA quick_check(1);"), CultureInfo.InvariantCulture), "ok", StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "The SQLite approval-store integrity check failed during bootstrap validation.");
        }

        if (requireWal && !string.Equals(Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported, "The SQLite approval store is not configured for WAL journaling.");
        }
    }

    private static void ValidateSchemaShape(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var tables = Convert.ToString(ExecuteScalar(connection, transaction, """
            SELECT group_concat(name, '|') FROM
                (SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name);
            """), CultureInfo.InvariantCulture);
        if (!string.Equals(tables, "approval_requests|security_store_metadata", StringComparison.Ordinal)
            || ExecuteInt64(connection, transaction,
                "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('index','trigger','view') AND name NOT LIKE 'sqlite_autoindex_%';") != 0
            || !string.Equals(ReadSchemaSql(connection, transaction, "security_store_metadata"), _metadataTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "approval_requests"), _requestsTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "security_store_metadata"), "store_id:BLOB:1:0", StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "approval_requests"),
                "request_id:BLOB:1:1|request_payload:BLOB:1:0|request_digest:BLOB:1:0|response_payload:BLOB:0:0|response_digest:BLOB:0:0", StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported,
                "The SQLite approval-store schema shape or integrity is unsupported.");
        }
    }

    private void ValidateTargetBeforeOpen(bool allowMissingFile)
    {
        var parent = Path.GetDirectoryName(_target.DatabasePath);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite target parent directory does not exist.");
        }

        var databaseFile = new FileInfo(_target.DatabasePath);
        if (!databaseFile.Exists && !allowMissingFile)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite approval-store target does not exist.");
        }
    }

    private void RequireInitialized()
    {
        lock (_gate)
        {
            if (!_initialized)
            {
                throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                    "The SQLite approval store was used before trusted bootstrap initialization.");
            }
        }
    }

    private static void VerifyDigest(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> digest)
    {
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), digest))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "Persisted approval payload digest is inconsistent.");
        }
    }

    private static byte[] ReadBoundedBlob(SqliteDataReader reader, int ordinal, int maximumLength)
    {
        var length = reader.GetBytes(ordinal, 0, null, 0, 0);
        if (length <= 0 || length > maximumLength)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "Persisted binary evidence exceeds its configured bound.");
        }

        var payload = new byte[checked((int) length)];
        _ = reader.GetBytes(ordinal, 0, payload, 0, payload.Length);
        return payload;
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

    private static SecurityGrantStoreUnavailableException Unavailable(
        SecurityGrantStoreFailureKind kind,
        string message,
        Exception? exception = null) => new(kind, message, exception);

    private readonly record struct StoredEntry(ApprovalRequest Request, ApprovalResponse? Response);
}
