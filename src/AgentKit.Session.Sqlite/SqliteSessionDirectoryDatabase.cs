// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Coordinates atomic directory projection transactions in the selected session database.</summary>
/// <remarks>
/// Bootstrap honors the target's <see cref="SqliteDatabaseOpenMode"/> and <see cref="SqliteSchemaMode"/> exactly like
/// <see cref="SqliteSessionDatabase"/>: known schema is installed or migrated only under
/// <see cref="SqliteSchemaMode.ApplyKnownMigrations"/>, and every mode validates that the directory table exists at
/// schema version one and carries the configured <see cref="SqliteSessionStoreTarget.ExpectedStoreInstanceId"/>.
/// </remarks>
internal sealed class SqliteSessionDirectoryDatabase
{
    private const string _schema = """
        CREATE TABLE IF NOT EXISTS agentkit_session_directory (
            singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
            schema_version INTEGER NOT NULL,
            store_instance_id TEXT NOT NULL,
            state_json BLOB NOT NULL
        );
        """;
    private readonly SqliteSessionStoreTarget _target;
    private readonly SqliteSessionStoreSettings _settings;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes and validates the durable directory table in the exact configured database.</summary>
    /// <param name="target">The explicit database target.</param>
    /// <param name="settings">Finite database bounds.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The target parent directory does not exist, the database is missing under
    /// <see cref="SqliteDatabaseOpenMode.OpenExisting"/>, or the directory schema or persisted store identity does not
    /// match the target.
    /// </exception>
    internal SqliteSessionDirectoryDatabase(SqliteSessionStoreTarget target, SqliteSessionStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        _target = target;
        _settings = settings;
        _json = new JsonSerializerOptions { TypeInfoResolver = SqliteSessionJsonTypeResolver.Create() };
        _json.Converters.Add(new SqliteValueObjectJsonConverterFactory());
        Initialize();
    }

    /// <summary>Loads the latest directory projection.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The complete committed state.</returns>
    internal async ValueTask<SqliteSessionDirectoryState> LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs one mutation against the latest state and commits it atomically.</summary>
    /// <typeparam name="T">The mutation outcome.</typeparam>
    /// <param name="action">The synchronous semantic mutation.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The committed semantic outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    internal async ValueTask<T> MutateAsync<T>(Func<SqliteSessionDirectoryState, T> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var state = await ReadAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var result = action(state);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE agentkit_session_directory SET state_json = $state WHERE singleton = 1;";
        _ = command.Parameters.AddWithValue("$state", JsonSerializer.SerializeToUtf8Bytes(state, _json));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private SqliteConnection CreateConnection() => new(new SqliteConnectionStringBuilder
    {
        DataSource = _target.DatabasePath,
        Mode = SqliteOpenMode.ReadWrite,
        Cache = SqliteCacheMode.Private,
        Pooling = false,
        DefaultTimeout = (int) _settings.LockTimeout.TotalSeconds,
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
            ApplyKnownMigrations(connection);
        }

        if (!TableExists(connection)
            || !ColumnExists(connection, "schema_version")
            || !ColumnExists(connection, "store_instance_id")
            || !ColumnExists(connection, "state_json"))
        {
            throw new InvalidOperationException("The SQLite session directory schema or persistent store identity is unavailable.");
        }

        using var validate = connection.CreateCommand();
        validate.CommandText = "SELECT schema_version, store_instance_id FROM agentkit_session_directory WHERE singleton = 1;";
        using var reader = validate.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1
            || !string.Equals(reader.GetString(1), ExpectedInstanceId(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The SQLite session directory schema or persistent store identity is unavailable.");
        }
    }

    /// <summary>Installs the current directory schema or migrates the earlier identity-less table shape in place.</summary>
    /// <param name="connection">The open bootstrap connection.</param>
    private void ApplyKnownMigrations(SqliteConnection connection)
    {
        Debug.Assert(connection is not null, "Bootstrap owns an open connection.");
        if (TableExists(connection) && !ColumnExists(connection, "store_instance_id"))
        {
            using var migrate = connection.CreateCommand();
            migrate.CommandText = """
                ALTER TABLE agentkit_session_directory ADD COLUMN schema_version INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE agentkit_session_directory ADD COLUMN store_instance_id TEXT NOT NULL DEFAULT '';
                UPDATE agentkit_session_directory SET store_instance_id = $id WHERE singleton = 1 AND store_instance_id = '';
                """;
            _ = migrate.Parameters.AddWithValue("$id", ExpectedInstanceId());
            _ = migrate.ExecuteNonQuery();
        }

        using var schema = connection.CreateCommand();
        schema.CommandText = _schema;
        _ = schema.ExecuteNonQuery();
        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT OR IGNORE INTO agentkit_session_directory VALUES (1, 1, $id, $state);";
        _ = insert.Parameters.AddWithValue("$id", ExpectedInstanceId());
        _ = insert.Parameters.AddWithValue("$state", JsonSerializer.SerializeToUtf8Bytes(new SqliteSessionDirectoryState(), _json));
        _ = insert.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection)
    {
        Debug.Assert(connection is not null, "Bootstrap owns an open connection.");
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'agentkit_session_directory';";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static bool ColumnExists(SqliteConnection connection, string column)
    {
        Debug.Assert(connection is not null, "Bootstrap owns an open connection.");
        Debug.Assert(!string.IsNullOrWhiteSpace(column), "A column name is required.");
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('agentkit_session_directory') WHERE name = $column;";
        _ = command.Parameters.AddWithValue("$column", column);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private string ExpectedInstanceId() => _target.ExpectedStoreInstanceId.Value.ToString("D");

    private async ValueTask<SqliteSessionDirectoryState> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT state_json FROM agentkit_session_directory WHERE singleton = 1;";
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var payload = scalar as byte[]
            ?? throw new InvalidOperationException("The SQLite session directory state is unavailable.");
        return JsonSerializer.Deserialize<SqliteSessionDirectoryState>(payload, _json)
            ?? throw new InvalidOperationException("The SQLite session directory state is invalid.");
    }
}
