// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Coordinates atomic directory projection transactions in the selected session database.</summary>
internal sealed class SqliteSessionDirectoryDatabase
{
    private readonly SqliteSessionStoreTarget _target;
    private readonly SqliteSessionStoreSettings _settings;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes the durable directory table.</summary>
    /// <param name="target">The explicit database target.</param>
    /// <param name="settings">Finite database bounds.</param>
    internal SqliteSessionDirectoryDatabase(SqliteSessionStoreTarget target, SqliteSessionStoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(settings);
        _target = target; _settings = settings;
        _json = new JsonSerializerOptions { TypeInfoResolver = SqliteSessionJsonTypeResolver.Create() };
        _json.Converters.Add(new SqliteValueObjectJsonConverterFactory());
        using var connection = Connection(target.OpenMode == SqliteDatabaseOpenMode.CreateIfMissing);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS agentkit_session_directory(singleton INTEGER PRIMARY KEY CHECK(singleton=1),state_json BLOB NOT NULL); INSERT OR IGNORE INTO agentkit_session_directory VALUES(1,$state);";
        _ = command.Parameters.AddWithValue("$state", JsonSerializer.SerializeToUtf8Bytes(new SqliteSessionDirectoryState(), _json));
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Loads the latest directory projection.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The complete committed state.</returns>
    internal async ValueTask<SqliteSessionDirectoryState> LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = Connection(false); await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs one mutation against the latest state and commits it atomically.</summary>
    /// <typeparam name="T">The mutation outcome.</typeparam>
    /// <param name="action">The synchronous semantic mutation.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The committed semantic outcome.</returns>
    internal async ValueTask<T> MutateAsync<T>(Func<SqliteSessionDirectoryState, T> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var connection = Connection(false); await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var state = await ReadAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var result = action(state);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE agentkit_session_directory SET state_json=$state WHERE singleton=1;";
        _ = command.Parameters.AddWithValue("$state", JsonSerializer.SerializeToUtf8Bytes(state, _json));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return result;
    }

    private SqliteConnection Connection(bool create) => new(new SqliteConnectionStringBuilder { DataSource = _target.DatabasePath, Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite, Pooling = false, DefaultTimeout = (int) _settings.LockTimeout.TotalSeconds }.ConnectionString);

    private async ValueTask<SqliteSessionDirectoryState> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT state_json FROM agentkit_session_directory WHERE singleton=1;";
        var payload = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
        return JsonSerializer.Deserialize<SqliteSessionDirectoryState>(payload!, _json)!;
    }
}
