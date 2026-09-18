// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>
/// Executes exactly the narrow SQL a directory operation needs against the relational schema declared by
/// <see cref="SqliteSessionDirectorySchema"/>, inside one caller-owned connection and transaction.
/// </summary>
/// <remarks>
/// A routed address's location and owning principal share one row, so there is no second parallel structure that
/// could disagree about who owns an address. Instances are confined to the caller's transaction and are not
/// thread-safe; callers create one per <see cref="SqliteSessionDirectoryDatabase.RunReadAsync{TResult}"/> or
/// <see cref="SqliteSessionDirectoryDatabase.RunWriteAsync{TResult}"/> invocation.
/// </remarks>
internal sealed class SqliteSessionDirectoryUnitOfWork
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes a unit of work bound to one open connection and transaction.</summary>
    /// <param name="connection">The caller-owned open connection.</param>
    /// <param name="transaction">The caller-owned active transaction.</param>
    /// <param name="json">The serializer options used for retained request and location evidence.</param>
    internal SqliteSessionDirectoryUnitOfWork(SqliteConnection connection, SqliteTransaction transaction, JsonSerializerOptions json)
    {
        Debug.Assert(connection is not null, "The database owns an open connection.");
        Debug.Assert(transaction is not null, "The database owns an active transaction.");
        Debug.Assert(json is not null, "The database owns shared serializer options.");
        _connection = connection;
        _transaction = transaction;
        _json = json;
    }

    private SqliteCommand CreateCommand(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }

    /// <summary>Loads the authoritative location and owner for one session address.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The location and its recording owner, or <see langword="null"/> when no route is recorded.</returns>
    internal async ValueTask<(SessionLocation Location, PrincipalId Owner)?> GetLocationAsync(
        SessionAddress address, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT tenant_id, store_key, directory_revision, recorded_at, schema_version, owner_principal_id
            FROM {SqliteSessionDirectorySchema.LocationsTable} WHERE agent_id = $agent AND session_id = $session;
            """);
        AddAddress(command, address);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var location = new SessionLocation(
            address, new TenantId(reader.GetString(0)), new SessionStoreKey(reader.GetString(1)),
            new SessionDirectoryRevision(reader.GetInt64(2)), ParseTimestamp(reader.GetString(3)), new SchemaVersion(reader.GetString(4)));
        return (location, new PrincipalId(reader.GetString(5)));
    }

    /// <summary>Inserts one new authoritative location row together with its recording owner.</summary>
    /// <param name="location">The complete new location.</param>
    /// <param name="owner">The principal recording this route.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask InsertLocationAsync(SessionLocation location, PrincipalId owner, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionDirectorySchema.LocationsTable}
                (agent_id, session_id, tenant_id, store_key, directory_revision, recorded_at, schema_version, owner_principal_id)
            VALUES ($agent, $session, $tenant, $store, $revision, $recorded, $schema, $owner);
            """);
        AddAddress(command, location.Address);
        _ = command.Parameters.AddWithValue("$tenant", location.TenantId.Value);
        _ = command.Parameters.AddWithValue("$store", location.StoreKey.Value);
        _ = command.Parameters.AddWithValue("$revision", location.DirectoryRevision.Value);
        _ = command.Parameters.AddWithValue("$recorded", ToTimestamp(location.RecordedAt));
        _ = command.Parameters.AddWithValue("$schema", location.SchemaVersion.Value);
        _ = command.Parameters.AddWithValue("$owner", owner.Value);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads one retained creation-retry route.</summary>
    /// <param name="tenantId">The isolating tenant.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="key">The caller retry identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The retained request and the location it committed, or <see langword="null"/> when no route exists.</returns>
    internal async ValueTask<(SessionCreateRequest Request, SessionLocation Location)?> GetCreationRouteAsync(
        TenantId tenantId, AgentId agentId, IdempotencyKey key, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT request, location FROM {SqliteSessionDirectorySchema.CreationRoutesTable}
            WHERE tenant_id = $tenant AND agent_id = $agent AND idempotency_key = $key;
            """);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        _ = command.Parameters.AddWithValue("$agent", ToText(agentId.Value));
        _ = command.Parameters.AddWithValue("$key", key.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return !await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? null
            : (Deserialize<SessionCreateRequest>((byte[]) reader[0]), Deserialize<SessionLocation>((byte[]) reader[1]));
    }

    /// <summary>Persists one creation-retry route.</summary>
    internal async ValueTask InsertCreationRouteAsync(
        TenantId tenantId, AgentId agentId, IdempotencyKey key, SessionCreateRequest request, SessionLocation location,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionDirectorySchema.CreationRoutesTable} (tenant_id, agent_id, idempotency_key, request, location)
            VALUES ($tenant, $agent, $key, $request, $location);
            """);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        _ = command.Parameters.AddWithValue("$agent", ToText(agentId.Value));
        _ = command.Parameters.AddWithValue("$key", key.Value);
        _ = command.Parameters.AddWithValue("$request", Serialize(request));
        _ = command.Parameters.AddWithValue("$location", Serialize(location));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads one retained write-retry route.</summary>
    /// <param name="tenantId">The isolating tenant.</param>
    /// <param name="address">The addressed session.</param>
    /// <param name="key">The caller retry identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The retained request and the location it committed, or <see langword="null"/> when no route exists.</returns>
    internal async ValueTask<(SessionDirectoryWriteRequest Request, SessionLocation Location)?> GetWriteRouteAsync(
        TenantId tenantId, SessionAddress address, IdempotencyKey key, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT request, location FROM {SqliteSessionDirectorySchema.WriteRoutesTable}
            WHERE tenant_id = $tenant AND agent_id = $agent AND session_id = $session AND idempotency_key = $key;
            """);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$key", key.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return !await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? null
            : (Deserialize<SessionDirectoryWriteRequest>((byte[]) reader[0]), Deserialize<SessionLocation>((byte[]) reader[1]));
    }

    /// <summary>Persists one write-retry route.</summary>
    internal async ValueTask InsertWriteRouteAsync(
        TenantId tenantId, SessionAddress address, IdempotencyKey key, SessionDirectoryWriteRequest request, SessionLocation location,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionDirectorySchema.WriteRoutesTable} (tenant_id, agent_id, session_id, idempotency_key, request, location)
            VALUES ($tenant, $agent, $session, $key, $request, $location);
            """);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$key", key.Value);
        _ = command.Parameters.AddWithValue("$request", Serialize(request));
        _ = command.Parameters.AddWithValue("$location", Serialize(location));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads one already-ordered, already-bounded page of routed locations owned by <paramref name="owner"/>
    /// for one tenant and agent.
    /// </summary>
    /// <param name="tenantId">The isolating tenant.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="owner">The exact owning principal a caller may list.</param>
    /// <param name="afterSessionId">
    /// The exclusive cursor: only session identities that sort strictly after this one, in the same ordinal
    /// text order as <see cref="SessionId.Value"/>'s <c>"D"</c>-format string, are returned. <see langword="null"/>
    /// to start from the beginning.
    /// </param>
    /// <param name="limit">
    /// The maximum number of rows to return. Callers pass one more than the page size requested so they can
    /// detect a further page without a second round trip.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// At most <paramref name="limit"/> matching locations, ordered ascending by <see cref="SessionId.Value"/>'s
    /// <c>"D"</c>-format text (SQLite's default <c>BINARY</c> collation on an ASCII string is byte-ordinal, so
    /// this SQL order agrees exactly with <see cref="string.CompareOrdinal(string?, string?)"/> over the same
    /// text, which every cursor comparison against this method's result must use).
    /// </returns>
    internal async ValueTask<ImmutableArray<SessionLocation>> ListCandidateLocationsAsync(
        TenantId tenantId, AgentId agentId, PrincipalId owner, SessionId? afterSessionId, int limit,
        CancellationToken cancellationToken)
    {
        Debug.Assert(limit > 0, "A caller always requests at least one row.");
        await using var command = CreateCommand($"""
            SELECT session_id, store_key, directory_revision, recorded_at, schema_version
            FROM {SqliteSessionDirectorySchema.LocationsTable}
            WHERE tenant_id = $tenant AND agent_id = $agent AND owner_principal_id = $owner
                AND ($after IS NULL OR session_id > $after)
            ORDER BY session_id
            LIMIT $limit;
            """);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        _ = command.Parameters.AddWithValue("$agent", ToText(agentId.Value));
        _ = command.Parameters.AddWithValue("$owner", owner.Value);
        _ = command.Parameters.AddWithValue("$after", (object?) afterSessionId?.Value.ToString("D") ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$limit", limit);
        var builder = ImmutableArray.CreateBuilder<SessionLocation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var address = new SessionAddress(agentId, new SessionId(Guid.Parse(reader.GetString(0))));
            builder.Add(new SessionLocation(
                address, tenantId, new SessionStoreKey(reader.GetString(1)), new SessionDirectoryRevision(reader.GetInt64(2)),
                ParseTimestamp(reader.GetString(3)), new SchemaVersion(reader.GetString(4))));
        }

        return builder.ToImmutable();
    }

    private static void AddAddress(SqliteCommand command, SessionAddress address)
    {
        _ = command.Parameters.AddWithValue("$agent", ToText(address.AgentId.Value));
        _ = command.Parameters.AddWithValue("$session", ToText(address.SessionId.Value));
    }

    private static string ToText(Guid value) => value.ToString("D");

    private static string ToTimestamp(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, _json);

    private T Deserialize<T>(byte[] payload) =>
        JsonSerializer.Deserialize<T>(payload, _json) ?? throw new JsonException($"The persisted {typeof(T).Name} payload is null.");
}
