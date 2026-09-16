// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Owns bootstrap, schema validation, and per-operation transactions for the relational session schema.</summary>
/// <remarks>
/// Every read runs inside a deferred (read) transaction so a multi-table load observes one consistent snapshot.
/// Every write runs inside a non-deferred ("<c>BEGIN IMMEDIATE</c>") transaction, which acquires SQLite's write
/// lock before the caller's business logic reads anything; the optimistic-concurrency check a caller performs
/// against the row it just read inside that same transaction is therefore atomic with the row updates it commits,
/// without any process-local gate. Two writers targeting the same session serialize on this database-level lock.
/// </remarks>
internal sealed class SqliteSessionDatabase
{
    private readonly SqliteSessionStoreTarget _target;
    private readonly SqliteSessionStoreSettings _settings;
    private readonly ISessionEntryCodecCatalog _entryCodecs;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes and validates the exact configured database.</summary>
    /// <param name="target">The fixed bootstrap target.</param>
    /// <param name="settings">Finite operational bounds.</param>
    /// <param name="entryCodecs">The frozen portable entry codecs used for durable payloads.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    internal SqliteSessionDatabase(SqliteSessionStoreTarget target, SqliteSessionStoreSettings settings, ISessionEntryCodecCatalog entryCodecs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(entryCodecs);
        _target = target;
        _settings = settings;
        _entryCodecs = entryCodecs;
        _json = new JsonSerializerOptions
        {
            TypeInfoResolver = SqliteSessionJsonTypeResolver.Create(),
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            PropertyNameCaseInsensitive = false,
        };
        _json.Converters.Add(new SqliteValueObjectJsonConverterFactory());
        _json.Converters.Add(new SqliteSessionEntryJsonConverterFactory(entryCodecs));
        Initialize();
    }

    /// <summary>Runs one read-only operation inside a deferred transaction over a consistent multi-table snapshot.</summary>
    /// <typeparam name="TResult">The typed semantic outcome.</typeparam>
    /// <param name="action">The read-only operation.</param>
    /// <param name="cancellationToken">Cancels before or during the read.</param>
    /// <returns>The value returned by <paramref name="action"/>.</returns>
    internal async ValueTask<TResult> RunReadAsync<TResult>(
        Func<SqliteSessionUnitOfWork, CancellationToken, ValueTask<TResult>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: true);
        var unitOfWork = new SqliteSessionUnitOfWork(connection, transaction, _entryCodecs, _json);
        var result = await action(unitOfWork, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Runs one mutating operation inside a non-deferred transaction: the write lock is acquired before the
    /// action reads anything, so its read-decide-write sequence is atomic with the commit.
    /// </summary>
    /// <typeparam name="TResult">The typed semantic outcome.</typeparam>
    /// <param name="action">The mutating operation.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The value returned by <paramref name="action"/>.</returns>
    internal async ValueTask<TResult> RunWriteAsync<TResult>(
        Func<SqliteSessionUnitOfWork, CancellationToken, ValueTask<TResult>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var unitOfWork = new SqliteSessionUnitOfWork(connection, transaction, _entryCodecs, _json);
        var result = await action(unitOfWork, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private SqliteConnection CreateConnection() => new(new SqliteConnectionStringBuilder
    {
        DataSource = _target.DatabasePath,
        Mode = SqliteOpenMode.ReadWrite,
        Cache = SqliteCacheMode.Private,
        DefaultTimeout = (int) _settings.LockTimeout.TotalSeconds,
        Pooling = false,
    }.ConnectionString);

    private void Initialize()
    {
        var parent = Path.GetDirectoryName(_target.DatabasePath)
            ?? throw new InvalidOperationException("The SQLite session target has no parent directory.");
        if (!Directory.Exists(parent))
        {
            throw new InvalidOperationException("The SQLite session target parent directory does not exist.");
        }

        var exists = File.Exists(_target.DatabasePath);
        if (!exists && _target.OpenMode == SqliteDatabaseOpenMode.OpenExisting)
        {
            throw new InvalidOperationException("The SQLite session target does not exist.");
        }

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _target.DatabasePath,
            Mode = exists ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ConnectionString);
        connection.Open();
        if (_target.SchemaMode == SqliteSchemaMode.ApplyKnownMigrations)
        {
            using var schema = connection.CreateCommand();
            schema.CommandText = SqliteSessionSchema.CreateSchema;
            _ = schema.ExecuteNonQuery();
            using var insert = connection.CreateCommand();
            insert.CommandText = $"INSERT OR IGNORE INTO {SqliteSessionSchema.SchemaTable} VALUES (1, 1, $id);";
            _ = insert.Parameters.AddWithValue("$id", _target.ExpectedStoreInstanceId.Value.ToString("D"));
            _ = insert.ExecuteNonQuery();
        }

        if (!TableExists(connection, SqliteSessionSchema.SchemaTable))
        {
            throw new InvalidOperationException("The SQLite session schema or persistent store identity is unavailable.");
        }

        using var validate = connection.CreateCommand();
        validate.CommandText = $"SELECT schema_version, store_instance_id FROM {SqliteSessionSchema.SchemaTable} WHERE singleton = 1;";
        using var reader = validate.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1
            || !string.Equals(reader.GetString(1), _target.ExpectedStoreInstanceId.Value.ToString("D"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The SQLite session schema or persistent store identity is unavailable.");
        }
        reader.Close();

        foreach (var table in SqliteSessionSchema.RequiredTables)
        {
            if (!TableExists(connection, table))
            {
                throw new InvalidOperationException("The SQLite session schema or persistent store identity is unavailable.");
            }
        }
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        _ = command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }
}
