// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Persists terminal security decisions in one fixed local SQLite database append-only table.</summary>
/// <remarks>Call <see cref="InitializeAsync"/> explicitly during trusted bootstrap before recording.</remarks>
public sealed class SqliteSecurityDecisionStore: ISecurityDecisionStore
{
    private const int _applicationId = 0x414B5044;
    private const int _schemaVersion = 1;
    private const string _metadataTableSql = "CREATE TABLE security_store_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16))";
    private const string _decisionsTableSql = "CREATE TABLE security_decisions (sequence INTEGER PRIMARY KEY AUTOINCREMENT, payload BLOB NOT NULL, payload_digest BLOB NOT NULL CHECK(length(payload_digest) = 32))";
    private readonly SqliteSecurityDecisionStoreTarget _target;
    private readonly SqliteSecurityDecisionStoreSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteSecurityDecisionStore> _logger;
    private readonly Lock _gate = new();
    private readonly List<SecurityDecision> _decisions = [];
    private bool _initialized;

    /// <summary>Initializes a store for one host-authorized fixed target without opening or creating it.</summary>
    /// <param name="target">The exact database and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable lock-wait and evidence bounds.</param>
    /// <param name="timeProvider">The clock used for operation duration diagnostics.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public SqliteSecurityDecisionStore(
        SqliteSecurityDecisionStoreTarget target,
        SqliteSecurityDecisionStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<SqliteSecurityDecisionStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<SqliteSecurityDecisionStore>.Instance;
    }

    /// <summary>Gets every recorded decision in arrival order.</summary>
    /// <value>An immutable snapshot of decisions retained so far.</value>
    public IReadOnlyList<SecurityDecision> Decisions
    {
        get
        {
            lock (_gate)
            {
                RequireInitialized();
                return _decisions.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
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
                            "The SQLite target has no initialized decision-store schema.");
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
                LoadDecisions(connection, transaction, cancellationToken);
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
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the append commits.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The store is uninitialized or persistence failed.</exception>
    public ValueTask RecordAsync(SecurityDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var payload = SqliteSecurityDecisionCodec.Encode(decision, _settings);
        return ExecuteVoidAsync("record", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                RequireInitialized();
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenStoreConnection();
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO security_decisions(payload, payload_digest) VALUES ($payload, $digest);";
            _ = command.Parameters.AddWithValue("$payload", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            cancellationToken.ThrowIfCancellationRequested();
            _ = command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            lock (_gate)
            {
                _decisions.Add(decision);
            }
        }, decision.RequestId);
    }

    private void LoadDecisions(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        Debug.Assert(_decisions.Count == 0, "Load populates an empty projection.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload, payload_digest FROM security_decisions ORDER BY sequence ASC;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = ReadBoundedBlob(reader, 0, _settings.MaximumDecisionBytes);
            VerifyDigest(payload, ReadBoundedBlob(reader, 1, SHA256.HashSizeInBytes));
            _decisions.Add(SqliteSecurityDecisionCodec.Decode(payload, _settings));
        }
    }

    private void InitializeNewSchema(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null, "An open initialization connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteNonQuery(connection, transaction, $"{_metadataTableSql};{_decisionsTableSql};");
        using var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = "INSERT INTO security_store_metadata(store_id) VALUES ($id);";
        _ = metadata.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(_target.ExpectedStoreInstanceId.Value));
        _ = metadata.ExecuteNonQuery();
        ExecuteNonQuery(connection, transaction, $"PRAGMA application_id = {_applicationId}; PRAGMA user_version = {_schemaVersion};");
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
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported, "The SQLite decision-store schema is unsupported.");
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
                "The SQLite decision-store identity does not match bootstrap configuration.");
        }

        if (performIntegrityCheck
            && !string.Equals(Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA quick_check(1);"), CultureInfo.InvariantCulture), "ok", StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "The SQLite decision-store integrity check failed during bootstrap validation.");
        }

        if (requireWal && !string.Equals(Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported, "The SQLite decision store is not configured for WAL journaling.");
        }
    }

    private static void ValidateSchemaShape(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var tables = Convert.ToString(ExecuteScalar(connection, transaction, """
            SELECT group_concat(name, '|') FROM
                (SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name);
            """), CultureInfo.InvariantCulture);
        if (!string.Equals(tables, "security_decisions|security_store_metadata", StringComparison.Ordinal)
            || ExecuteInt64(connection, transaction,
                "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('index','trigger','view') AND name NOT LIKE 'sqlite_autoindex_%';") != 0
            || !string.Equals(ReadSchemaSql(connection, transaction, "security_store_metadata"), _metadataTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "security_decisions"), _decisionsTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "security_store_metadata"), "store_id:BLOB:1:0", StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "security_decisions"),
                "sequence:INTEGER:0:1|payload:BLOB:1:0|payload_digest:BLOB:1:0", StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported,
                "The SQLite decision-store schema shape or integrity is unsupported.");
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
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite decision-store target does not exist.");
        }
    }

    private void RequireInitialized()
    {
        if (!_initialized)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                "The SQLite security decision store was used before trusted bootstrap initialization.");
        }
    }

    private static void VerifyDigest(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> digest)
    {
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), digest))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "Persisted decision payload digest is inconsistent.");
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

    private ValueTask ExecuteVoidAsync(string operation, Action action, SecurityRequestId? requestId = null)
    {
        using var activity = AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityDecisionStoreOperation,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, operation },
                { AgentKitTagNames.SecurityRequestId, requestId?.ToString() },
            });
        var measured = TryGetTimestamp(out var startedAt);
        try
        {
            action();
            ObserveTerminal(activity.Activity, operation, "success", true, null, requestId, measured, startedAt);
            return ValueTask.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            ObserveTerminal(activity.Activity, operation, "cancelled", false, null, requestId, measured, startedAt);
            throw;
        }
        catch (SecurityGrantStoreUnavailableException exception)
        {
            ObserveTerminal(activity.Activity, operation, "unavailable", false, exception, requestId, measured, startedAt);
            throw;
        }
        catch (Exception exception)
        {
            ObserveTerminal(activity.Activity, operation, "faulted", false, exception, requestId, measured, startedAt);
            throw;
        }
    }

    private void ObserveTerminal(
        Activity? activity,
        string operation,
        string outcome,
        bool successful,
        Exception? exception,
        SecurityRequestId? requestId,
        bool measured,
        long startedAt)
    {
        try
        {
            if (successful)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, GetBoundedFailureCode(outcome, exception));
            }
        }
        catch
        {
            // Observational only.
        }

        try
        {
            SqliteSecurityDecisionStoreMetrics.Operations.Add(1,
                new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            if (measured)
            {
                SqliteSecurityDecisionStoreMetrics.Duration.Record(
                    _timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                    new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            }
        }
        catch
        {
            // Observational only.
        }

        try
        {
            var requestText = requestId?.ToString();
            if (successful)
            {
                SqliteSecurityDecisionStoreLog.OperationCompleted(_logger, operation, outcome, requestText);
            }
            else
            {
                SqliteSecurityDecisionStoreLog.OperationFailed(
                    _logger, operation, outcome, GetBoundedFailureCode(outcome, exception), requestText);
            }
        }
        catch
        {
            // Observational only.
        }
    }

    private bool TryGetTimestamp(out long timestamp)
    {
        try
        {
            timestamp = _timeProvider.GetTimestamp();
            return true;
        }
        catch
        {
            timestamp = 0;
            return false;
        }
    }

    private static string GetBoundedFailureCode(string outcome, Exception? exception) => exception switch
    {
        SecurityGrantStoreUnavailableException unavailable => unavailable.Kind.ToString().ToLowerInvariant(),
        OperationCanceledException => "cancelled",
        null => outcome,
        _ => "faulted",
    };

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
}
