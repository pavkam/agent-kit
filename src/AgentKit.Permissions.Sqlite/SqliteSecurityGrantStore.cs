// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Persists authoritative security grants and enforcement-intent receipts in one fixed local SQLite database.</summary>
/// <remarks>
/// The singleton retains no connection. Every operation opens and validates the exact configured store identity and schema,
/// then uses an immediate SQLite transaction for one atomic state transition. SQLite coordinates processes on one host but
/// does not provide distributed fencing. Enforcement receipts are retained indefinitely. Call <see cref="InitializeAsync"/>
/// explicitly during trusted bootstrap.
/// </remarks>
public sealed class SqliteSecurityGrantStore: ISecurityGrantStore
{
    private const int _applicationId = 0x414B5047;
    private const int _schemaVersion = 1;
    private const string _metadataTableSql = "CREATE TABLE security_store_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16))";
    private const string _grantsTableSql = "CREATE TABLE security_grants (grant_id BLOB PRIMARY KEY NOT NULL CHECK(length(grant_id) = 16), payload BLOB NOT NULL, payload_digest BLOB NOT NULL CHECK(length(payload_digest) = 32), remaining_uses INTEGER NOT NULL CHECK(remaining_uses >= 0), revoked INTEGER NOT NULL CHECK(revoked IN (0, 1)))";
    private const string _intentsTableSql = "CREATE TABLE security_intents (intent_id BLOB PRIMARY KEY NOT NULL CHECK(length(intent_id) = 16), grant_id BLOB NOT NULL CHECK(length(grant_id) = 16), request_id BLOB NOT NULL CHECK(length(request_id) = 16), enforcement BLOB NOT NULL, enforcement_digest BLOB NOT NULL CHECK(length(enforcement_digest) = 32), required_fence INTEGER NULL, effect_fingerprint TEXT NOT NULL, consumed_ticks INTEGER NOT NULL, consumed_offset_ticks INTEGER NOT NULL, FOREIGN KEY(grant_id) REFERENCES security_grants(grant_id))";
    private readonly SqliteSecurityGrantStoreTarget _target;
    private readonly SqliteSecurityGrantStoreSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteSecurityGrantStore> _logger;

    /// <summary>Initializes a store for one host-authorized fixed target without opening or creating it.</summary>
    /// <param name="target">The exact database and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable lock-wait and evidence bounds.</param>
    /// <param name="timeProvider">The clock used for validity checks and consumption receipts.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public SqliteSecurityGrantStore(
        SqliteSecurityGrantStoreTarget target,
        SqliteSecurityGrantStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<SqliteSecurityGrantStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<SqliteSecurityGrantStore>.Instance;
    }

    /// <summary>Creates or validates the exact version-one schema and persistent store identity under bootstrap policy.</summary>
    /// <param name="cancellationToken">Cancels before the schema transaction commits or, for an initialized target, before a permitted journal-mode change begins.</param>
    /// <returns>A task completed after the exact target is ready for grant operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the applicable bootstrap linearization point.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The target, schema, store identity, or persistence provider cannot be validated safely.</exception>
    /// <remarks>
    /// Creation may leave an empty version-zero database when cancellation or process loss occurs after the exact file is
    /// opened but before the schema commits. A later create-and-migrate initialization may recover only that structurally
    /// empty state. Once schema commit begins, initialization completes journal setup or reports a retryable typed failure;
    /// caller cancellation does not turn a known committed schema into an unknown result.
    /// </remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ExecuteVoidAsync(
        "initialize",
        () =>
        {
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
                        "The SQLite target has no initialized grant-store schema.");
                }
                InitializeNewSchema(connection, transaction, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                SetWalMode(connection);
                ValidateConnection(connection, requireWal: true, performIntegrityCheck: true);
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
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SetWalMode(connection);
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false);
        });

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="grant"/> contains malformed copied evidence that cannot be reconstructed exactly by the bounded codec.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="grant"/> contains an invalid scalar or exceeds a configured evidence bound.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before registration commits.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The exact target, schema, persisted evidence, lock, or provider operation cannot be validated safely.</exception>
    /// <remarks>The adapter owns and disposes the per-call connection and immediate transaction. A persistence failure while acknowledging commit may leave registration uncertain; retrying the exact immutable grant reconciles the authoritative row, while different evidence for the same ID remains a conflict.</remarks>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateGrant(grant);
        ArgumentException.ThrowIfNotPersistable(grant, _settings);
        var payload = SqliteSecurityGrantCodec.EncodeGrant(grant, _settings);
        return ExecuteVoidAsync("register", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenStoreConnection();
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var existing = ReadGrant(connection, transaction, grant.Id);
            cancellationToken.ThrowIfCancellationRequested();
            if (existing is not null)
            {
                if (!GrantMatches(existing.Value.Grant, grant))
                {
                    throw new InvalidOperationException("The grant identifier is already registered with different evidence.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO security_grants(grant_id, payload, payload_digest, remaining_uses, revoked) VALUES ($id, $payload, $digest, $uses, 0);";
            _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(grant.Id.Value));
            _ = command.Parameters.AddWithValue("$payload", payload);
            _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
            _ = command.Parameters.AddWithValue("$uses", grant.AllowedUses);
            cancellationToken.ThrowIfCancellationRequested();
            _ = command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
        }, grant);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="grant"/> or <paramref name="enforcement"/> contains malformed copied evidence that cannot be reconstructed exactly by the bounded codec.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="grant"/> or <paramref name="enforcement"/> contains an invalid scalar or exceeds a configured evidence bound.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before consumption commits.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The exact target, schema, persisted evidence, lock, or provider operation cannot be validated safely.</exception>
    /// <remarks>The adapter owns and disposes the per-call connection and immediate transaction. This legacy operation retains no enforcement-intent receipt, so an uncertain persistence acknowledgement cannot be retried automatically as proof of fresh effect authority.</remarks>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(grant, enforcement);
        ArgumentException.ThrowIfNotPersistable(grant, _settings);
        ArgumentException.ThrowIfNotPersistable(enforcement, _settings);
        var enforcementPayload = SqliteSecurityGrantCodec.EncodeEnforcement(enforcement, _settings);
        return ExecuteAsync("consume", () => ConsumeCore(
            grant, enforcement, enforcementPayload, null, cancellationToken), grant, null, null);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="grant"/> or <paramref name="enforcement"/> contains malformed copied evidence that cannot be reconstructed exactly by the bounded codec.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="grant"/> or <paramref name="enforcement"/> contains an invalid scalar or exceeds a configured evidence bound.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before consumption and receipt persistence commit together.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The exact target, schema, persisted evidence, lock, or provider operation cannot be validated safely.</exception>
    /// <remarks>The adapter owns and disposes the per-call connection and immediate transaction. After an uncertain acknowledgement, recovery presents the exact same grant, enforcement, and intent; a reconciled receipt is historical evidence and never fresh authority to repeat the protected effect.</remarks>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(grant, enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNotPersistable(grant, _settings);
        ArgumentException.ThrowIfNotPersistable(enforcement, _settings);
        var enforcementPayload = SqliteSecurityGrantCodec.EncodeEnforcement(enforcement, _settings);
        return ExecuteAsync("consume_intent", () => ConsumeCore(
            grant, enforcement, enforcementPayload, intent, cancellationToken), grant, intent, null);
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before revocation commits.</exception>
    /// <exception cref="SecurityGrantStoreUnavailableException">The exact target, schema, persisted evidence, lock, or provider operation cannot be validated safely.</exception>
    /// <remarks>The adapter owns and disposes the per-call connection and immediate transaction. Revocation is idempotent, so retrying the same grant identity reconciles an uncertain acknowledgement without restoring authority.</remarks>
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ExecuteAsync(
        "revoke",
        () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenStoreConnection();
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE security_grants SET revoked = 1 WHERE grant_id = $id;";
            _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(grantId.Value));
            cancellationToken.ThrowIfCancellationRequested();
            var found = command.ExecuteNonQuery() != 0;
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return found;
        }, null, null, grantId);

    private GrantConsumptionResult ConsumeCore(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        byte[] enforcementPayload,
        SecurityEnforcementIntent? intent,
        CancellationToken cancellationToken)
    {
        Debug.Assert(grant is not null, "Caller-validated grant evidence is required.");
        Debug.Assert(enforcement is not null, "Caller-validated enforcement evidence is required.");
        Debug.Assert(enforcementPayload is not null, "Pre-encoded bounded enforcement evidence is required.");
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenStoreConnection();
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = connection.BeginTransaction(deferred: false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConnection(connection, requireWal: true, performIntegrityCheck: false, transaction);
        cancellationToken.ThrowIfCancellationRequested();
        var state = ReadGrant(connection, transaction, grant.Id);
        cancellationToken.ThrowIfCancellationRequested();
        if (state is null)
        {
            return Result(GrantConsumptionStatus.Unknown, 0, "The security grant is unknown.");
        }
        var (authoritativeGrant, currentRemainingUses, revoked) = state.Value;
        if (!GrantMatches(authoritativeGrant, grant))
        {
            return Result(GrantConsumptionStatus.Tampered, currentRemainingUses, "The security grant evidence does not match its authoritative record.");
        }

        ContentHash? effectFingerprint = intent is null
            ? null
            : SecurityEnforcementBinding.Fingerprint(enforcement, intent);
        if (intent is not null)
        {
            Debug.Assert(effectFingerprint.HasValue, "An intent always has a computed effect fingerprint.");
            var expectedFingerprint = effectFingerprint.GetValueOrDefault();
            var historical = ReadReceipt(connection, transaction, intent.Id);
            cancellationToken.ThrowIfCancellationRequested();
            if (historical is not null)
            {
                return ReceiptMatches(historical, grant, enforcement, intent, expectedFingerprint)
                    ? Result(GrantConsumptionStatus.Reconciled, currentRemainingUses, "The enforcement intent was reconciled without granting another effect.", historical)
                    : Result(GrantConsumptionStatus.Mismatch, currentRemainingUses, "The enforcement intent identity was reused with different evidence.");
            }
        }

        if (revoked || enforcement.RevocationVersion != grant.RevocationVersion)
        {
            return Result(GrantConsumptionStatus.Revoked, currentRemainingUses, "The security grant is revoked or stale.");
        }
        var now = _timeProvider.GetUtcNow();
        cancellationToken.ThrowIfCancellationRequested();
        if (now < grant.NotBefore || now >= grant.ExpiresAt)
        {
            return Result(GrantConsumptionStatus.Expired, currentRemainingUses, "The security grant is outside its validity window.");
        }
        if (!EnforcementMatches(grant, enforcement))
        {
            return Result(GrantConsumptionStatus.Mismatch, currentRemainingUses, "The concrete effect does not match the security grant.");
        }
        if (currentRemainingUses == 0)
        {
            return Result(GrantConsumptionStatus.Exhausted, 0, "The security grant has no remaining uses.");
        }

        var remainingUses = currentRemainingUses - 1;
        var receipt = intent is null
            ? null
            : new SecurityEnforcementIntentReceipt(intent.Id, grant.Id, grant.RequestId, enforcement,
                intent.RequiredFence, effectFingerprint!.Value, now);
        var result = Result(GrantConsumptionStatus.Consumed, remainingUses,
            intent is null ? "The security grant was consumed." : "The security grant and enforcement intent were consumed.", receipt);
        cancellationToken.ThrowIfCancellationRequested();
        if (receipt is not null)
        {
            InsertReceipt(connection, transaction, receipt, enforcementPayload);
        }
        UpdateRemainingUses(connection, transaction, grant.Id, remainingUses);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return result;
    }

    private void InitializeNewSchema(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        Debug.Assert(connection is not null, "An open initialization connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        Debug.Assert(ExecuteInt64(connection, transaction, "PRAGMA user_version;") == 0,
            "Only a validated new database can initialize schema version one.");
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteNonQuery(connection, transaction,
            $"{_metadataTableSql};{_grantsTableSql};{_intentsTableSql};");
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

    private static bool IsUninitializedDatabase(SqliteConnection connection, SqliteTransaction transaction)
    {
        Debug.Assert(connection is not null, "An open candidate connection is required.");
        Debug.Assert(transaction is not null, "A serializing initialization transaction is required.");
        return ExecuteInt64(connection, transaction, "PRAGMA application_id;") == 0
            && ExecuteInt64(connection, transaction, "PRAGMA user_version;") == 0
            && ExecuteInt64(connection, transaction,
                "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('table','index','trigger','view') AND name NOT LIKE 'sqlite_%';") == 0;
    }

    private void ValidateConnection(
        SqliteConnection connection,
        bool requireWal,
        bool performIntegrityCheck,
        SqliteTransaction? transaction = null)
    {
        Debug.Assert(connection is not null, "An open store connection is required.");
        if (ExecuteInt64(connection, transaction, "PRAGMA application_id;") != _applicationId
            || ExecuteInt64(connection, transaction, "PRAGMA user_version;") != _schemaVersion)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported, "The SQLite grant-store schema is unsupported.");
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
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "The SQLite grant-store identity does not match bootstrap configuration.");
        }
        if (performIntegrityCheck
            && !string.Equals(Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA quick_check(1);"), CultureInfo.InvariantCulture), "ok", StringComparison.Ordinal))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "The SQLite grant-store integrity check failed during bootstrap validation.");
        }
        if (requireWal && !string.Equals(Convert.ToString(ExecuteScalar(connection, transaction, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported, "The SQLite grant store is not configured for WAL journaling.");
        }
    }

    private static void ValidateSchemaShape(SqliteConnection connection, SqliteTransaction? transaction)
    {
        Debug.Assert(connection is not null, "An open store connection is required.");
        var tables = Convert.ToString(ExecuteScalar(connection, transaction, """
            SELECT group_concat(name, '|') FROM
                (SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name);
            """), CultureInfo.InvariantCulture);
        if (!string.Equals(tables, "security_grants|security_intents|security_store_metadata", StringComparison.Ordinal)
            || ExecuteInt64(connection, transaction,
                "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('index','trigger','view') AND name NOT LIKE 'sqlite_autoindex_%';") != 0
            || !string.Equals(ReadSchemaSql(connection, transaction, "security_store_metadata"), _metadataTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "security_grants"), _grantsTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadSchemaSql(connection, transaction, "security_intents"), _intentsTableSql, StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "security_store_metadata"), "store_id:BLOB:1:0", StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "security_grants"),
                "grant_id:BLOB:1:1|payload:BLOB:1:0|payload_digest:BLOB:1:0|remaining_uses:INTEGER:1:0|revoked:INTEGER:1:0",
                StringComparison.Ordinal)
            || !string.Equals(ReadColumnShape(connection, transaction, "security_intents"),
                "intent_id:BLOB:1:1|grant_id:BLOB:1:0|request_id:BLOB:1:0|enforcement:BLOB:1:0|enforcement_digest:BLOB:1:0|required_fence:INTEGER:0:0|effect_fingerprint:TEXT:1:0|consumed_ticks:INTEGER:1:0|consumed_offset_ticks:INTEGER:1:0",
                StringComparison.Ordinal)
            || ExecuteInt64(connection, transaction, "SELECT COUNT(*) FROM pragma_foreign_key_list('security_intents') WHERE \"table\" = 'security_grants' AND \"from\" = 'grant_id' AND \"to\" = 'grant_id' AND on_update = 'NO ACTION' AND on_delete = 'NO ACTION';") != 1)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.SchemaUnsupported,
                "The SQLite grant-store schema shape or integrity is unsupported.");
        }
    }

    private static string? ReadColumnShape(SqliteConnection connection, SqliteTransaction? transaction, string table)
    {
        Debug.Assert(connection is not null, "An open store connection is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(table), "A compiled schema table name is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT group_concat(shape, '|') FROM (SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk AS shape FROM pragma_table_info('{table}') ORDER BY cid);";
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string? ReadSchemaSql(SqliteConnection connection, SqliteTransaction? transaction, string table)
    {
        Debug.Assert(connection is not null, "An open store connection is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(table), "A compiled schema table name is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = $table;";
        _ = command.Parameters.AddWithValue("$table", table);
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private void ValidateTargetBeforeOpen(bool allowMissingFile)
    {
        var parent = Path.GetDirectoryName(_target.DatabasePath);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite target parent directory does not exist.");
        }
        for (var current = new DirectoryInfo(parent); current is not null; current = current.Parent)
        {
            if (current.LinkTarget is not null || current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite target traverses a replaceable link.");
            }
        }
        var databaseFile = new FileInfo(_target.DatabasePath);
        if (databaseFile.LinkTarget is not null)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite target is a replaceable link.");
        }
        if (databaseFile.Exists)
        {
            if (databaseFile.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite target is a replaceable link.");
            }
        }
        else if (!allowMissingFile)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed, "The SQLite target does not exist.");
        }
        ValidateSidecarPath(_target.DatabasePath + "-wal", databaseFile.Exists);
        ValidateSidecarPath(_target.DatabasePath + "-shm", databaseFile.Exists);
        ValidateSidecarPath(_target.DatabasePath + "-journal", databaseFile.Exists);
    }

    private static void ValidateSidecarPath(string path, bool mainDatabaseExists)
    {
        var file = new FileInfo(path);
        if (file.LinkTarget is not null)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                "A SQLite target sidecar is a replaceable link.");
        }
        if (!file.Exists)
        {
            return;
        }
        if (!mainDatabaseExists)
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                "A SQLite sidecar exists without its configured main database.");
        }
        if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                "A SQLite target sidecar is a replaceable link.");
        }
    }

    private static void SetWalMode(SqliteConnection connection)
    {
        Debug.Assert(connection is not null, "An open initialized connection is required.");
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL;";
        _ = command.ExecuteNonQuery();
    }

    private (SecurityGrant Grant, int RemainingUses, bool Revoked)? ReadGrant(
        SqliteConnection connection,
        SqliteTransaction transaction,
        GrantId id)
    {
        Debug.Assert(connection is not null, "An open validated connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload, payload_digest, remaining_uses, revoked FROM security_grants WHERE grant_id = $id;";
        _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(id.Value));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        var payload = ReadBoundedBlob(reader, 0, _settings.MaximumGrantBytes);
        VerifyDigest(payload, ReadBoundedBlob(reader, 1, SHA256.HashSizeInBytes));
        var grant = SqliteSecurityGrantCodec.DecodeGrant(payload, _settings);
        _ = grant.Id == id
            ? true
            : throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "Persisted grant identity evidence is inconsistent.");
        var remainingUses = reader.GetInt32(2);
        _ = remainingUses >= 0 && remainingUses <= grant.AllowedUses && reader.GetInt64(3) is 0 or 1
            ? true
            : throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "Persisted grant state violates its authoritative bounds.");
        return (grant, remainingUses, reader.GetBoolean(3));
    }

    private SecurityEnforcementIntentReceipt? ReadReceipt(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SecurityEnforcementIntentId id)
    {
        Debug.Assert(connection is not null, "An open validated connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT grant_id, request_id, enforcement, enforcement_digest, required_fence, effect_fingerprint, consumed_ticks, consumed_offset_ticks FROM security_intents WHERE intent_id = $id;";
        _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(id.Value));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        var payload = ReadBoundedBlob(reader, 2, _settings.MaximumEnforcementBytes);
        VerifyDigest(payload, ReadBoundedBlob(reader, 3, SHA256.HashSizeInBytes));
        var enforcement = SqliteSecurityGrantCodec.DecodeEnforcement(payload, _settings);
        FencingToken? fence = reader.IsDBNull(4) ? null : new FencingToken(reader.GetInt64(4));
        var receipt = new SecurityEnforcementIntentReceipt(
            id,
            new GrantId(SqliteSecurityGrantCodec.DecodeGuid(ReadBoundedBlob(reader, 0, 16))),
            new SecurityRequestId(SqliteSecurityGrantCodec.DecodeGuid(ReadBoundedBlob(reader, 1, 16))),
            enforcement,
            fence,
            new ContentHash(ReadBoundedString(reader, 5, 1024)),
            new DateTimeOffset(reader.GetInt64(6), TimeSpan.FromTicks(reader.GetInt64(7))));
        _ = receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(
            enforcement, new SecurityEnforcementIntent(id, fence))
            ? true
            : throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "Persisted enforcement receipt evidence is inconsistent.");
        return receipt;
    }

    private static void InsertReceipt(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SecurityEnforcementIntentReceipt receipt,
        byte[] payload)
    {
        Debug.Assert(connection is not null, "An open validated connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        Debug.Assert(receipt is not null, "A validated receipt is required.");
        Debug.Assert(payload is not null, "Pre-encoded bounded enforcement evidence is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO security_intents(intent_id, grant_id, request_id, enforcement, enforcement_digest, required_fence, effect_fingerprint, consumed_ticks, consumed_offset_ticks) VALUES ($intent, $grant, $request, $enforcement, $digest, $fence, $fingerprint, $ticks, $offset);";
        _ = command.Parameters.AddWithValue("$intent", SqliteSecurityGrantCodec.EncodeGuid(receipt.IntentId.Value));
        _ = command.Parameters.AddWithValue("$grant", SqliteSecurityGrantCodec.EncodeGuid(receipt.GrantId.Value));
        _ = command.Parameters.AddWithValue("$request", SqliteSecurityGrantCodec.EncodeGuid(receipt.RequestId.Value));
        _ = command.Parameters.AddWithValue("$enforcement", payload);
        _ = command.Parameters.AddWithValue("$digest", SHA256.HashData(payload));
        _ = command.Parameters.AddWithValue("$fence", receipt.RequiredFence is { } fence ? fence.Value : DBNull.Value);
        _ = command.Parameters.AddWithValue("$fingerprint", receipt.EffectFingerprint.Value);
        _ = command.Parameters.AddWithValue("$ticks", receipt.ConsumedAt.Ticks);
        _ = command.Parameters.AddWithValue("$offset", receipt.ConsumedAt.Offset.Ticks);
        _ = command.ExecuteNonQuery();
    }

    private static void UpdateRemainingUses(SqliteConnection connection, SqliteTransaction transaction, GrantId id, int remainingUses)
    {
        Debug.Assert(connection is not null, "An open validated connection is required.");
        Debug.Assert(transaction is not null, "An active immediate transaction is required.");
        Debug.Assert(remainingUses >= 0, "Remaining uses cannot be negative.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE security_grants SET remaining_uses = $uses WHERE grant_id = $id;";
        _ = command.Parameters.AddWithValue("$uses", remainingUses);
        _ = command.Parameters.AddWithValue("$id", SqliteSecurityGrantCodec.EncodeGuid(id.Value));
        _ = command.ExecuteNonQuery();
    }

    private static byte[] ReadBoundedBlob(SqliteDataReader reader, int ordinal, int maximumLength)
    {
        Debug.Assert(reader is not null, "An active row reader is required.");
        Debug.Assert(ordinal >= 0, "A nonnegative column ordinal is required.");
        Debug.Assert(maximumLength > 0, "A positive evidence bound is required.");
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

    private static string ReadBoundedString(SqliteDataReader reader, int ordinal, int maximumLength)
    {
        Debug.Assert(reader is not null, "An active row reader is required.");
        Debug.Assert(ordinal >= 0, "A nonnegative column ordinal is required.");
        Debug.Assert(maximumLength > 0, "A positive evidence bound is required.");
        var length = reader.GetChars(ordinal, 0, null, 0, 0);
        _ = length > 0 && length <= maximumLength
            ? true
            : throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "Persisted text evidence exceeds its configured bound.");
        return reader.GetString(ordinal);
    }

    private static void VerifyDigest(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expected)
    {
        Debug.Assert(!payload.IsEmpty, "Nonempty persisted evidence is required.");
        Debug.Assert(expected.Length == SHA256.HashSizeInBytes, "An exact SHA-256 digest is required.");
        Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.TryHashData(payload, actual, out var written);
        Debug.Assert(written == actual.Length, "SHA-256 always writes thirty-two bytes.");
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "Persisted security evidence failed its integrity check.");
        }
    }

    private static bool GrantMatches(SecurityGrant expected, SecurityGrant actual)
    {
        Debug.Assert(expected is not null, "Authoritative grant evidence is required.");
        Debug.Assert(actual is not null, "Presented grant evidence is required.");
        return expected == actual;
    }

    private static bool EnforcementMatches(SecurityGrant grant, SecurityEnforcementRequest enforcement) =>
        grant.Scope == enforcement.Scope
        && grant.Identity == enforcement.Identity
        && grant.Authorization == enforcement.Authorization
        && grant.Audience == enforcement.Audience
        && grant.Kind == enforcement.Kind
        && grant.Effect == enforcement.Effect
        && grant.Resources.SequenceEqual(enforcement.Resources)
        && grant.InputFingerprint == enforcement.InputFingerprint;

    private static bool ReceiptMatches(
        SecurityEnforcementIntentReceipt receipt,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        ContentHash fingerprint)
    {
        Debug.Assert(receipt is not null, "Authoritative receipt evidence is required.");
        Debug.Assert(grant is not null, "Presented grant evidence is required.");
        Debug.Assert(enforcement is not null, "Presented enforcement evidence is required.");
        Debug.Assert(intent is not null, "Presented intent evidence is required.");
        return receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && EnforcementEvidenceMatches(receipt.Enforcement, enforcement)
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == fingerprint;
    }

    private static bool EnforcementEvidenceMatches(SecurityEnforcementRequest expected, SecurityEnforcementRequest actual)
    {
        Debug.Assert(expected is not null, "Authoritative enforcement evidence is required.");
        Debug.Assert(actual is not null, "Presented enforcement evidence is required.");
        return expected == actual;
    }

    private static void ValidateArguments(SecurityGrant grant, SecurityEnforcementRequest enforcement)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ValidateGrant(grant);
        ArgumentNullException.ThrowIfNull(enforcement.Scope);
        ArgumentNullException.ThrowIfNull(enforcement.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(enforcement.Resources);
    }

    private static void ValidateGrant(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant.Scope);
        ArgumentNullException.ThrowIfNull(grant.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(grant.Resources);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(grant.ExpiresAt, grant.NotBefore);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grant.AllowedUses);
    }

    private ValueTask<T> ExecuteAsync<T>(
        string operation,
        Func<T> action,
        SecurityGrant? grant = null,
        SecurityEnforcementIntent? intent = null,
        GrantId? grantId = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous provider action is required.");
        using var activity = StartActivity(operation, grant, intent, grantId);
        var measured = TryGetTimestamp(out var startedAt);
        try
        {
            var result = action();
            var outcome = result is GrantConsumptionResult consumption
                ? consumption.Status.ToString().ToLowerInvariant()
                : "success";
            var successful = result is not GrantConsumptionResult terminal
                || terminal.Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled;
            ObserveTerminal(activity.Activity, operation, outcome, successful, null,
                grant, intent, grantId, measured, startedAt);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            ObserveTerminal(activity.Activity, operation, "cancelled", false, null,
                grant, intent, grantId, measured, startedAt);
            throw;
        }
        catch (SecurityGrantStoreUnavailableException exception)
        {
            ObserveTerminal(activity.Activity, operation, "unavailable", false, exception,
                grant, intent, grantId, measured, startedAt);
            throw;
        }
        catch (SqliteException exception)
        {
            var mapped = exception.SqliteErrorCode switch
            {
                5 or 6 => Unavailable(SecurityGrantStoreFailureKind.Busy,
                    "The SQLite grant store could not acquire its bounded lock.", exception),
                11 or 26 => Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                    "The SQLite grant-store target is corrupt or is not a supported database.", exception),
                14 => Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                    "The SQLite grant-store target could not be opened.", exception),
                _ => Unavailable(SecurityGrantStoreFailureKind.PersistenceFailed,
                    "The SQLite grant store operation failed.", exception),
            };
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                grant, intent, grantId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException
            or InvalidCastException or OverflowException or ArgumentException)
        {
            var mapped = Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence, "Persisted security evidence is corrupt or unsupported.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                grant, intent, grantId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var mapped = Unavailable(SecurityGrantStoreFailureKind.OpenFailed,
                "The SQLite grant-store target could not be accessed.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                grant, intent, grantId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception)
        {
            ObserveTerminal(activity.Activity, operation, "faulted", false, exception,
                grant, intent, grantId, measured, startedAt);
            throw;
        }
    }

    private ValueTask ExecuteVoidAsync(string operation, Action action, SecurityGrant? grant = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous provider action is required.");
        return new ValueTask(ExecuteAsync(operation, () =>
        {
            action();
            return true;
        }, grant).AsTask());
    }

    private static AgentKitActivityScope StartActivity(
        string operation,
        SecurityGrant? grant,
        SecurityEnforcementIntent? intent,
        GrantId? grantId)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        return AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityGrantStoreOperation,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, operation },
                { AgentKitTagNames.SecurityRequestId, grant?.RequestId.ToString() },
                { AgentKitTagNames.SecurityGrantId, (grant?.Id ?? grantId)?.ToString() },
                { AgentKitTagNames.SecurityEnforcementIntentId, intent?.Id.ToString() },
                { AgentKitTagNames.AgentId, grant?.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, grant?.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, grant?.Scope.Correlation.OperationId.ToString() },
            });
    }

    private void ObserveTerminal(
        Activity? activity,
        string operation,
        string outcome,
        bool successful,
        Exception? exception,
        SecurityGrant? grant,
        SecurityEnforcementIntent? intent,
        GrantId? grantId,
        bool measured,
        long startedAt)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        // Activity.SetSuccessful/SetFailed never throws for the always-bounded, nonblank outcome and failure-code
        // values produced here, so the catch has no reachable trigger; it guards only against a future regression.
        try { if (successful) { activity.SetSuccessful(outcome); } else { activity.SetFailed(outcome, GetBoundedFailureCode(outcome, exception)); } } catch { }
        try
        {
            SqliteSecurityGrantStoreMetrics.Operations.Add(1,
                new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            if (measured)
            {
                SqliteSecurityGrantStoreMetrics.Duration.Record(
                    _timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                    new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            }
        }
        catch
        {
            // Meter listeners are observational.
        }
        try
        {
            var requestText = grant?.RequestId.ToString();
            var grantText = (grant?.Id ?? grantId)?.ToString();
            var intentText = intent?.Id.ToString();
            if (successful)
            {
                SqliteSecurityGrantStoreLog.OperationCompleted(
                    _logger, operation, outcome, requestText, grantText, intentText);
            }
            else
            {
                SqliteSecurityGrantStoreLog.OperationFailed(
                    _logger, operation, outcome, GetBoundedFailureCode(outcome, exception),
                    requestText, grantText, intentText);
            }
        }
        catch
        {
            // Loggers are observational.
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

    private static string GetBoundedFailureCode(string outcome, Exception? exception)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        return exception switch
        {
            SecurityGrantStoreUnavailableException unavailable => unavailable.Kind.ToString().ToLowerInvariant(),
            OperationCanceledException => "cancelled",
            null => outcome,
            _ => "faulted",
        };
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

    private static GrantConsumptionResult Result(
        GrantConsumptionStatus status,
        int remainingUses,
        string safeMessage,
        SecurityEnforcementIntentReceipt? receipt = null) => new(status, remainingUses, safeMessage, receipt);

    private static SecurityGrantStoreUnavailableException Unavailable(
        SecurityGrantStoreFailureKind kind,
        string message,
        Exception? exception = null) => new(kind, message, exception);

}
