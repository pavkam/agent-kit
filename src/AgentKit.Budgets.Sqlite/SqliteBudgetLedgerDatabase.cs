// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Owns fixed-target SQLite bootstrap and per-operation connection validation for the budget ledger.</summary>
/// <remarks>The helper retains no connection. Its caller owns every returned connection and transaction.</remarks>
internal sealed class SqliteBudgetLedgerDatabase
{
    private const int _applicationId = 0x414B4247;
    private const int _schemaVersion = 1;
    private const string _metadataSql = "CREATE TABLE budget_ledger_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), revision INTEGER NOT NULL CHECK(revision >= 0))";
    private const string _scopesSql = "CREATE TABLE budget_scopes (scope_id BLOB PRIMARY KEY NOT NULL CHECK(length(scope_id) = 16), parent_scope_id BLOB NULL CHECK(parent_scope_id IS NULL OR length(parent_scope_id) = 16), depth INTEGER NOT NULL CHECK(depth > 0), request BLOB NOT NULL, request_digest BLOB NOT NULL CHECK(length(request_digest) = 32), FOREIGN KEY(parent_scope_id) REFERENCES budget_scopes(scope_id))";
    private const string _scopeKeysSql = "CREATE TABLE budget_scope_keys (idempotency_key TEXT PRIMARY KEY NOT NULL, scope_id BLOB NOT NULL CHECK(length(scope_id) = 16), FOREIGN KEY(scope_id) REFERENCES budget_scopes(scope_id))";
    private const string _reservationsSql = "CREATE TABLE budget_reservations (reservation_id BLOB PRIMARY KEY NOT NULL CHECK(length(reservation_id) = 16), scope_id BLOB NOT NULL CHECK(length(scope_id) = 16), receipt BLOB NOT NULL, receipt_digest BLOB NOT NULL CHECK(length(receipt_digest) = 32), aggregation INTEGER NOT NULL, started_ticks INTEGER NULL, started_offset_ticks INTEGER NULL, start_revision INTEGER NOT NULL CHECK(start_revision >= 0), released INTEGER NOT NULL CHECK(released IN (0, 1)), start_expiration BLOB NULL, start_expiration_digest BLOB NULL CHECK(start_expiration_digest IS NULL OR length(start_expiration_digest) = 32), original_commit BLOB NULL, original_commit_digest BLOB NULL CHECK(original_commit_digest IS NULL OR length(original_commit_digest) = 32), current_commit BLOB NULL, current_commit_digest BLOB NULL CHECK(current_commit_digest IS NULL OR length(current_commit_digest) = 32), accounting_revision INTEGER NULL, latest_correction_revision INTEGER NOT NULL CHECK(latest_correction_revision >= 0), FOREIGN KEY(scope_id) REFERENCES budget_scopes(scope_id))";
    private const string _chargesSql = "CREATE TABLE budget_reservation_charges (scope_id BLOB NOT NULL CHECK(length(scope_id) = 16), reservation_id BLOB NOT NULL CHECK(length(reservation_id) = 16), active INTEGER NOT NULL CHECK(active IN (0, 1)), PRIMARY KEY(scope_id, reservation_id), FOREIGN KEY(scope_id) REFERENCES budget_scopes(scope_id), FOREIGN KEY(reservation_id) REFERENCES budget_reservations(reservation_id))";
    private const string _batchKeysSql = "CREATE TABLE budget_batch_keys (idempotency_key TEXT PRIMARY KEY NOT NULL, request BLOB NOT NULL, request_digest BLOB NOT NULL CHECK(length(request_digest) = 32), result BLOB NOT NULL, result_digest BLOB NOT NULL CHECK(length(result_digest) = 32))";
    private const string _correctionsSql = "CREATE TABLE budget_corrections (reservation_id BLOB NOT NULL CHECK(length(reservation_id) = 16), correction_revision INTEGER NOT NULL CHECK(correction_revision > 0), result BLOB NOT NULL, result_digest BLOB NOT NULL CHECK(length(result_digest) = 32), PRIMARY KEY(reservation_id, correction_revision), FOREIGN KEY(reservation_id) REFERENCES budget_reservations(reservation_id))";
    private const string _reconciliationsSql = "CREATE TABLE budget_reconciliations (idempotency_key TEXT NOT NULL, reservation_id BLOB NOT NULL CHECK(length(reservation_id) = 16), evidence BLOB NOT NULL, evidence_digest BLOB NOT NULL CHECK(length(evidence_digest) = 32), result BLOB NOT NULL, result_digest BLOB NOT NULL CHECK(length(result_digest) = 32), PRIMARY KEY(reservation_id,idempotency_key), FOREIGN KEY(reservation_id) REFERENCES budget_reservations(reservation_id))";
    private const string _holdsSql = "CREATE TABLE budget_overrun_holds (boundary_scope_id BLOB NOT NULL CHECK(length(boundary_scope_id) = 16), reservation_id BLOB NOT NULL CHECK(length(reservation_id) = 16), accounting_revision INTEGER NOT NULL CHECK(accounting_revision > 0), evidence BLOB NOT NULL, evidence_digest BLOB NOT NULL CHECK(length(evidence_digest) = 32), automatically_cleared INTEGER NOT NULL CHECK(automatically_cleared IN (0, 1)), resolution BLOB NULL, resolution_digest BLOB NULL CHECK(resolution_digest IS NULL OR length(resolution_digest) = 32), PRIMARY KEY(boundary_scope_id, reservation_id, accounting_revision), FOREIGN KEY(boundary_scope_id) REFERENCES budget_scopes(scope_id), FOREIGN KEY(reservation_id) REFERENCES budget_reservations(reservation_id))";
    private const string _resolutionKeysSql = "CREATE TABLE budget_resolution_keys (idempotency_key TEXT PRIMARY KEY NOT NULL, request BLOB NOT NULL, request_digest BLOB NOT NULL CHECK(length(request_digest) = 32), result BLOB NOT NULL, result_digest BLOB NOT NULL CHECK(length(result_digest) = 32))";
    private const string _projectionsSql = "CREATE TABLE budget_dimension_projections (scope_id BLOB NOT NULL CHECK(length(scope_id) = 16), dimension TEXT NOT NULL, unit TEXT NOT NULL, aggregation INTEGER NOT NULL, reserved_coefficient BLOB NOT NULL, reserved_scale INTEGER NOT NULL CHECK(reserved_scale BETWEEN 0 AND 28), committed_coefficient BLOB NOT NULL, committed_scale INTEGER NOT NULL CHECK(committed_scale BETWEEN 0 AND 28), open_count INTEGER NOT NULL CHECK(open_count >= 0), PRIMARY KEY(scope_id, dimension), FOREIGN KEY(scope_id) REFERENCES budget_scopes(scope_id))";
    private const string _maximumValuesSql = "CREATE TABLE budget_maximum_values (scope_id BLOB NOT NULL CHECK(length(scope_id) = 16), dimension TEXT NOT NULL, amount_key BLOB NOT NULL, amount_text TEXT NOT NULL, live_count INTEGER NOT NULL CHECK(live_count >= 0), committed_count INTEGER NOT NULL CHECK(committed_count >= 0), PRIMARY KEY(scope_id, dimension, amount_key), FOREIGN KEY(scope_id) REFERENCES budget_scopes(scope_id))";
    private const string _maximumLiveIndexSql = "CREATE INDEX budget_maximum_live_idx ON budget_maximum_values(scope_id, dimension, amount_key DESC) WHERE live_count > 0";
    private const string _maximumCommittedIndexSql = "CREATE INDEX budget_maximum_committed_idx ON budget_maximum_values(scope_id, dimension, amount_key DESC) WHERE committed_count > 0";
    private const string _activeChargesIndexSql = "CREATE INDEX budget_active_charges_idx ON budget_reservation_charges(scope_id, reservation_id) WHERE active = 1";
    private const string _unresolvedIndexSql = "CREATE INDEX budget_unresolved_reservations_idx ON budget_reservations(scope_id, start_revision, reservation_id) WHERE started_ticks IS NOT NULL AND released = 0 AND current_commit IS NULL";
    private readonly SqliteBudgetLedgerTarget _target;
    private readonly SqliteBudgetLedgerSettings _settings;
    private readonly Action? _commitAcknowledgementFault;

    /// <summary>Creates a database boundary without touching the configured target.</summary>
    /// <param name="target">The exact host-authorized target.</param>
    /// <param name="settings">The immutable provider and evidence bounds.</param>
    /// <param name="commitAcknowledgementFault">Optional test-only loss injected after SQLite commits but before the result is acknowledged.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    internal SqliteBudgetLedgerDatabase(SqliteBudgetLedgerTarget target, SqliteBudgetLedgerSettings settings, Action? commitAcknowledgementFault = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        _target = target;
        _settings = settings;
        _commitAcknowledgementFault = commitAcknowledgementFault;
    }

    /// <summary>Creates or validates the exact schema selected by trusted bootstrap configuration.</summary>
    /// <param name="cancellationToken">Cancels before schema commit or a separately permitted WAL change.</param>
    /// <exception cref="OperationCanceledException">Cancellation occurs before the applicable linearization point.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The target or schema cannot be validated safely.</exception>
    internal void Initialize(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateTarget(_target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
            using var connection = Open(forInitialization: true);
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction(deferred: false);
            var uninitialized = IsUninitialized(connection, transaction);
            if (uninitialized)
            {
                if (_target.OpenMode != SqliteDatabaseOpenMode.CreateIfMissing
                    || _target.SchemaMode != SqliteSchemaMode.ApplyKnownMigrations)
                {
                    throw Unavailable("The SQLite target has no initialized budget-ledger schema.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                Execute(connection, transaction, string.Join(';', AllSchemaStatements) + ';');
                using var metadata = connection.CreateCommand();
                metadata.Transaction = transaction;
                metadata.CommandText = "INSERT INTO budget_ledger_metadata(store_id, revision) VALUES ($id, 0);";
                _ = metadata.Parameters.AddWithValue("$id", _target.ExpectedStoreInstanceId.Value.ToByteArray());
                _ = metadata.ExecuteNonQuery();
                Execute(connection, transaction, $"PRAGMA application_id = {_applicationId}; PRAGMA user_version = {_schemaVersion};");
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                SetWal(connection);
                Validate(connection, requireWal: true, integrityCheck: true);
                return;
            }
            Validate(connection, _target.SchemaMode == SqliteSchemaMode.ValidateExact, true, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            if (_target.SchemaMode == SqliteSchemaMode.ApplyKnownMigrations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetWal(connection);
                Validate(connection, requireWal: true, integrityCheck: false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BudgetLedgerPersistenceUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw Unavailable("The SQLite budget-ledger target could not be initialized.", exception);
        }
    }

    /// <summary>Executes one serialized write and owns its commit acknowledgement classification.</summary>
    /// <typeparam name="T">The immutable operation result type.</typeparam>
    /// <param name="action">The non-null transition that stages writes in the supplied immediate transaction.</param>
    /// <param name="cancellationToken">Cancels before commit begins.</param>
    /// <returns>The known committed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">Storage fails before commit or while commit acknowledgement is uncertain.</exception>
    internal T Write<T>(Func<SqliteConnection, SqliteTransaction, T> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var commitAttempted = false;
        SqliteConnection? connection = null;
        SqliteTransaction? transaction = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateTarget(allowMissing: false);
            connection = Open(forInitialization: false);
            transaction = connection.BeginTransaction(deferred: false);
            Validate(connection, requireWal: true, integrityCheck: false, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            var result = action(connection, transaction);
            cancellationToken.ThrowIfCancellationRequested();
            commitAttempted = true;
            transaction.Commit();
            _commitAcknowledgementFault?.Invoke();
            return result;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new BudgetLedgerPersistenceUnavailableException(
                "The SQLite budget ledger could not confirm the operation.", commitAttempted, exception);
        }
        finally
        {
            // Cleanup is observational after a known commit and cannot erase its confirmed receipt.
            try
            {
                transaction?.Dispose();
            }
            catch (Exception)
            {
            }

            try
            {
                connection?.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }

    private static IReadOnlyList<string> SchemaStatements =>
    [
        _metadataSql, _scopesSql, _scopeKeysSql, _reservationsSql, _chargesSql, _batchKeysSql,
        _correctionsSql, _reconciliationsSql, _holdsSql, _resolutionKeysSql,
        _projectionsSql, _maximumValuesSql,
    ];

    private static IReadOnlyList<string> IndexStatements => [_maximumLiveIndexSql, _maximumCommittedIndexSql, _activeChargesIndexSql, _unresolvedIndexSql];

    private static IEnumerable<string> AllSchemaStatements => SchemaStatements.Concat(IndexStatements);

    private SqliteConnection Open(bool forInitialization)
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
            try
            {
                connection.Dispose();
            }
            catch (Exception)
            {
            }
            throw;
        }
    }

    private static bool IsUninitialized(SqliteConnection connection, SqliteTransaction transaction) =>
        // Both objects are created and owned by the enclosing initialization transaction.
        ScalarInt64(connection, transaction, "PRAGMA application_id;") == 0
        && ScalarInt64(connection, transaction, "PRAGMA user_version;") == 0
        && ScalarInt64(connection, transaction, "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('table','index','trigger','view') AND name NOT LIKE 'sqlite_%';") == 0;

    private void Validate(SqliteConnection connection, bool requireWal, bool integrityCheck, SqliteTransaction? transaction = null)
    {
        Debug.Assert(connection is not null, "An open adapter-owned connection is required.");
        if (ScalarInt64(connection, transaction, "PRAGMA application_id;") != _applicationId
            || ScalarInt64(connection, transaction, "PRAGMA user_version;") != _schemaVersion)
        {
            throw Unavailable("The SQLite budget-ledger schema version is unsupported.");
        }
        var tables = Convert.ToString(Scalar(connection, transaction, "SELECT group_concat(name, '|') FROM (SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name);"), CultureInfo.InvariantCulture);
        var expected = string.Join('|', SchemaStatements.Select(static sql => sql.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2]).Order(StringComparer.Ordinal));
        if (!string.Equals(tables, expected, StringComparison.Ordinal))
        {
            throw Unavailable("The SQLite budget-ledger schema shape is unsupported.");
        }
        using (var schema = connection.CreateCommand())
        {
            schema.Transaction = transaction;
            schema.CommandText = "SELECT name,sql FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var schemaReader = schema.ExecuteReader();
            var expectedDefinitions = SchemaStatements.ToDictionary(
                static statement => statement.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2],
                NormalizeSchema,
                StringComparer.Ordinal);
            var seen = 0;
            while (schemaReader.Read())
            {
                var name = schemaReader.GetString(0);
                var definition = schemaReader.IsDBNull(1) ? string.Empty : NormalizeSchema(schemaReader.GetString(1));
                if (!expectedDefinitions.TryGetValue(name, out var expectedDefinition)
                    || !string.Equals(definition, expectedDefinition, StringComparison.Ordinal))
                {
                    throw Unavailable("The SQLite budget-ledger table definition is unsupported.");
                }
                seen++;
            }
            if (seen != expectedDefinitions.Count)
            {
                throw Unavailable("The SQLite budget-ledger table set is incomplete.");
            }
        }
        using (var indexes = connection.CreateCommand())
        {
            indexes.Transaction = transaction;
            indexes.CommandText = "SELECT name,sql FROM sqlite_schema WHERE type='index' AND sql IS NOT NULL ORDER BY name;";
            using var indexReader = indexes.ExecuteReader();
            var expectedIndexes = IndexStatements.ToDictionary(
                static statement => statement.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2], NormalizeSchema, StringComparer.Ordinal);
            var seenIndexes = 0;
            while (indexReader.Read())
            {
                if (!expectedIndexes.TryGetValue(indexReader.GetString(0), out var expectedIndex)
                    || !string.Equals(NormalizeSchema(indexReader.GetString(1)), expectedIndex, StringComparison.Ordinal))
                {
                    throw Unavailable("The SQLite budget-ledger index definition is unsupported.");
                }
                seenIndexes++;
            }
            if (seenIndexes != expectedIndexes.Count)
            {
                throw Unavailable("The SQLite budget-ledger index set is incomplete.");
            }
        }
        if (ScalarInt64(connection, transaction, "SELECT COUNT(*) FROM sqlite_schema WHERE type IN ('trigger','view');") != 0)
        {
            throw Unavailable("The SQLite budget-ledger schema contains unsupported indexes, views, or triggers.");
        }
        using var identity = connection.CreateCommand();
        identity.Transaction = transaction;
        identity.CommandText = "SELECT store_id FROM budget_ledger_metadata LIMIT 2;";
        using var reader = identity.ExecuteReader();
        if (!reader.Read())
        {
            throw Unavailable("The SQLite budget-ledger identity does not match bootstrap configuration.");
        }
        var identityLength = reader.GetBytes(0, 0, null, 0, 0);
        if (identityLength != 16)
        {
            throw Unavailable("The SQLite budget-ledger identity does not match bootstrap configuration.");
        }
        var identityBytes = new byte[16];
        _ = reader.GetBytes(0, 0, identityBytes, 0, identityBytes.Length);
        if (!identityBytes.AsSpan().SequenceEqual(_target.ExpectedStoreInstanceId.Value.ToByteArray()) || reader.Read())
        {
            throw Unavailable("The SQLite budget-ledger identity does not match bootstrap configuration.");
        }
        if (integrityCheck && !string.Equals(Convert.ToString(Scalar(connection, transaction, "PRAGMA quick_check(1);"), CultureInfo.InvariantCulture), "ok", StringComparison.Ordinal))
        {
            throw Unavailable("The SQLite budget-ledger integrity check failed.");
        }
        if (requireWal && !string.Equals(Convert.ToString(Scalar(connection, transaction, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw Unavailable("The SQLite budget ledger is not configured for WAL journaling.");
        }
    }

    private void ValidateTarget(bool allowMissing)
    {
        var parent = Path.GetDirectoryName(_target.DatabasePath);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            throw Unavailable("The SQLite target parent directory does not exist.");
        }
        for (var current = new DirectoryInfo(parent); current is not null; current = current.Parent)
        {
            if (current.LinkTarget is not null || current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw Unavailable("The SQLite target traverses a replaceable link.");
            }
        }
        var main = new FileInfo(_target.DatabasePath);
        if (main.LinkTarget is not null || (main.Exists && main.Attributes.HasFlag(FileAttributes.ReparsePoint)))
        {
            throw Unavailable("The SQLite target is a replaceable link.");
        }
        if (!main.Exists && !allowMissing)
        {
            throw Unavailable("The SQLite target does not exist.");
        }
        ValidateSidecar(_target.DatabasePath + "-wal", main.Exists);
        ValidateSidecar(_target.DatabasePath + "-shm", main.Exists);
        ValidateSidecar(_target.DatabasePath + "-journal", main.Exists);
    }

    private static void ValidateSidecar(string path, bool mainExists)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(path), "A derived SQLite sidecar path is required.");
        var sidecar = new FileInfo(path);
        if (sidecar.LinkTarget is not null || (sidecar.Exists && sidecar.Attributes.HasFlag(FileAttributes.ReparsePoint)) || (sidecar.Exists && !mainExists))
        {
            throw Unavailable("A SQLite target sidecar is inconsistent or replaceable.");
        }
    }

    private static void SetWal(SqliteConnection connection)
    {
        Debug.Assert(connection is not null, "An open adapter-owned connection is required.");
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL;";
        _ = command.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        Debug.Assert(connection is not null && transaction is not null, "An active adapter-owned transaction is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(sql), "A fixed schema statement is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private static long ScalarInt64(SqliteConnection connection, SqliteTransaction? transaction, string sql) =>
        Convert.ToInt64(Scalar(connection, transaction, sql), CultureInfo.InvariantCulture);

    private static object? Scalar(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        Debug.Assert(connection is not null, "An open adapter-owned connection is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(sql), "A fixed scalar query is required.");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static BudgetLedgerPersistenceUnavailableException Unavailable(string message, Exception? exception = null) =>
        exception is null
            ? new BudgetLedgerPersistenceUnavailableException(message, acknowledgementUnknown: false)
            : new BudgetLedgerPersistenceUnavailableException(message, acknowledgementUnknown: false, exception);

    private static string NormalizeSchema(string statement) => string.Join(' ', statement.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));
}
