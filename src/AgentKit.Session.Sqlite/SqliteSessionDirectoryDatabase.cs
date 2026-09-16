// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Owns bootstrap, schema validation, and per-operation transactions for the relational directory schema.</summary>
/// <remarks>
/// Bootstrap honors the target's <see cref="SqliteDatabaseOpenMode"/> and <see cref="SqliteSchemaMode"/> exactly like
/// <see cref="SqliteSessionDatabase"/>: known schema is installed only under
/// <see cref="SqliteSchemaMode.ApplyKnownMigrations"/>, and every mode validates that every required table exists and
/// that the schema marker row carries the configured <see cref="SqliteSessionStoreTarget.ExpectedStoreInstanceId"/>.
/// A pre-relational (whole-directory-blob) database is a foreign schema: it never satisfies this validation, so
/// <see cref="SqliteSchemaMode.ValidateExact"/> against one fails with a typed exception instead of reinterpreting it.
/// </remarks>
internal sealed class SqliteSessionDirectoryDatabase
{
    private readonly SqliteSessionStoreTarget _target;
    private readonly SqliteSessionStoreSettings _settings;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes and validates the directory tables in the exact configured database.</summary>
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

    /// <summary>Runs one read-only operation inside a deferred transaction over a consistent multi-table snapshot.</summary>
    internal async ValueTask<TResult> RunReadAsync<TResult>(
        Func<SqliteSessionDirectoryUnitOfWork, CancellationToken, ValueTask<TResult>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: true);
        var unitOfWork = new SqliteSessionDirectoryUnitOfWork(connection, transaction, _json);
        var result = await action(unitOfWork, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Runs one mutating operation inside a non-deferred ("<c>BEGIN IMMEDIATE</c>") transaction so its
    /// read-decide-write sequence is atomic with the commit.
    /// </summary>
    internal async ValueTask<TResult> RunWriteAsync<TResult>(
        Func<SqliteSessionDirectoryUnitOfWork, CancellationToken, ValueTask<TResult>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var unitOfWork = new SqliteSessionDirectoryUnitOfWork(connection, transaction, _json);
        var result = await action(unitOfWork, cancellationToken).ConfigureAwait(false);
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
            using var schema = connection.CreateCommand();
            schema.CommandText = SqliteSessionDirectorySchema.CreateSchema;
            _ = schema.ExecuteNonQuery();
            using var insert = connection.CreateCommand();
            insert.CommandText = $"INSERT OR IGNORE INTO {SqliteSessionDirectorySchema.SchemaTable} VALUES (1, 1, $id);";
            _ = insert.Parameters.AddWithValue("$id", _target.ExpectedStoreInstanceId.Value.ToString("D"));
            _ = insert.ExecuteNonQuery();
        }

        foreach (var table in SqliteSessionDirectorySchema.RequiredTables)
        {
            if (!TableExists(connection, table))
            {
                throw new InvalidOperationException("The SQLite session directory schema or persistent store identity is unavailable.");
            }
        }

        using var validate = connection.CreateCommand();
        validate.CommandText = $"SELECT schema_version, store_instance_id FROM {SqliteSessionDirectorySchema.SchemaTable} WHERE singleton = 1;";
        using var reader = validate.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1
            || !string.Equals(reader.GetString(1), _target.ExpectedStoreInstanceId.Value.ToString("D"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The SQLite session directory schema or persistent store identity is unavailable.");
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
