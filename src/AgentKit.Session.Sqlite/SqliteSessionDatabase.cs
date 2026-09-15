// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Owns fixed-target bootstrap and whole-state SQLite transactions for one session adapter.</summary>
internal sealed class SqliteSessionDatabase
{
    private const string _schema = """
        CREATE TABLE IF NOT EXISTS agentkit_session_metadata (
            singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
            schema_version INTEGER NOT NULL,
            store_instance_id TEXT NOT NULL,
            state_json BLOB NOT NULL
        );
        """;
    private readonly SqliteSessionStoreTarget _target;
    private readonly SqliteSessionStoreSettings _settings;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes and validates the exact configured database.</summary>
    /// <param name="target">The fixed bootstrap target.</param>
    /// <param name="settings">Finite operational bounds.</param>
    /// <param name="entryCodecs">The frozen portable entry codecs used for durable payloads.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    internal SqliteSessionDatabase(SqliteSessionStoreTarget target, SqliteSessionStoreSettings settings,
        ISessionEntryCodecCatalog entryCodecs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(entryCodecs);
        _target = target;
        _settings = settings;
        _json = new JsonSerializerOptions
        {
            TypeInfoResolver = SqliteSessionJsonTypeResolver.Create(),
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            PropertyNameCaseInsensitive = false,
        };
        AddKeyConverter<SessionAddress>();
        AddKeyConverter<(TenantId, AgentId, IdempotencyKey)>();
        AddKeyConverter<(TenantId, SessionAddress, IdempotencyKey)>();
        AddKeyConverter<BranchId>();
        AddKeyConverter<IdempotencyKey>();
        AddKeyConverter<InputId>();
        AddKeyConverter<AdmissionId>();
        AddKeyConverter<ExecutionLaneId>();
        _json.Converters.Add(new SqliteValueObjectJsonConverterFactory());
        _json.Converters.Add(new SqliteSessionEntryJsonConverterFactory(entryCodecs));
        Initialize();
    }

    /// <summary>Adds one exact reversible dictionary-key converter.</summary>
    /// <typeparam name="T">The immutable key type.</typeparam>
    private void AddKeyConverter<T>() where T : notnull =>
        _json.Converters.Add(new SqliteDictionaryKeyJsonConverter<T>());

    /// <summary>Loads the latest committed state under the caller's serialized operation boundary.</summary>
    /// <returns>The complete persisted state.</returns>
    internal async ValueTask<SqliteSessionStoreState> LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(connection, transaction: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs one semantic mutation and publishes its complete state atomically.</summary>
    /// <typeparam name="TResult">The terminal store outcome.</typeparam>
    /// <param name="hydrate">Replaces process state from the transaction snapshot.</param>
    /// <param name="action">Performs the already-authorized semantic operation.</param>
    /// <param name="capture">Captures complete state after the operation.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The semantic outcome committed with the resulting state.</returns>
    internal async ValueTask<TResult> MutateAsync<TResult>(
        Action<SqliteSessionStoreState> hydrate,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<SqliteSessionStoreState> capture,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        hydrate(await ReadAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
        var result = await action(cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.SerializeToUtf8Bytes(capture(), _json);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE agentkit_session_metadata SET state_json = $state WHERE singleton = 1;";
        _ = command.Parameters.AddWithValue("$state", payload);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _target.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = (int) _settings.LockTimeout.TotalSeconds,
            Pooling = false,
        };
        return new SqliteConnection(builder.ConnectionString);
    }

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

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _target.DatabasePath,
            Mode = exists ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        if (_target.SchemaMode == SqliteSchemaMode.ApplyKnownMigrations)
        {
            using var schema = connection.CreateCommand();
            schema.CommandText = _schema;
            _ = schema.ExecuteNonQuery();
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO agentkit_session_metadata VALUES (1, 1, $id, $state);";
            _ = insert.Parameters.AddWithValue("$id", _target.ExpectedStoreInstanceId.Value.ToString("D"));
            _ = insert.Parameters.AddWithValue("$state", JsonSerializer.SerializeToUtf8Bytes(new SqliteSessionStoreState(), _json));
            _ = insert.ExecuteNonQuery();
        }

        using var validate = connection.CreateCommand();
        validate.CommandText = "SELECT schema_version, store_instance_id FROM agentkit_session_metadata WHERE singleton = 1;";
        using var reader = validate.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1
            || !string.Equals(reader.GetString(1), _target.ExpectedStoreInstanceId.Value.ToString("D"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The SQLite session schema or persistent store identity is unavailable.");
        }
    }

    private async ValueTask<SqliteSessionStoreState> ReadAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT state_json FROM agentkit_session_metadata WHERE singleton = 1;";
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var payload = scalar as byte[]
            ?? throw new InvalidOperationException("The SQLite session state is unavailable.");
        return JsonSerializer.Deserialize<SqliteSessionStoreState>(payload, _json)
            ?? throw new InvalidOperationException("The SQLite session state is invalid.");
    }
}
