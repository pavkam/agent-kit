// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

internal sealed partial class SqliteSessionUnitOfWork
{
    // ---- Session- and branch-scoped idempotency (agentkit_session_scope_idempotency) ----

    /// <summary>Loads one session- or branch-scoped idempotency receipt.</summary>
    /// <typeparam name="TRequest">The cached request type.</typeparam>
    /// <typeparam name="TResult">The cached terminal result type.</typeparam>
    /// <param name="address">The addressed session.</param>
    /// <param name="scopeKind">One of the session-scoped <see cref="SqliteSessionIdempotencyScope"/> values.</param>
    /// <param name="branchId">The scoping branch for <see cref="SqliteSessionIdempotencyScope.Append"/>; otherwise <see langword="null"/>.</param>
    /// <param name="key">The caller idempotency key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decoded receipt, or <see langword="null"/> when no row matches.</returns>
    internal async ValueTask<IdempotencyReceipt<TRequest, TResult>?> GetSessionScopeReceiptAsync<TRequest, TResult>(
        string scopeKind, SessionAddress address, BranchId? branchId, IdempotencyKey key, CancellationToken cancellationToken)
        where TRequest : class
        where TResult : class
    {
        await using var command = CreateCommand($"""
            SELECT request, result FROM {SqliteSessionSchema.SessionScopeIdempotencyTable}
            WHERE agent_id = $agent AND session_id = $session AND scope_kind = $kind AND branch_id = $branch AND idempotency_key = $key;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$kind", scopeKind);
        _ = command.Parameters.AddWithValue("$branch", branchId is { } branch ? ToText(branch.Value) : string.Empty);
        _ = command.Parameters.AddWithValue("$key", key.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return !await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? null
            : new IdempotencyReceipt<TRequest, TResult>(Deserialize<TRequest>((byte[]) reader[0]), Deserialize<TResult>((byte[]) reader[1]));
    }

    /// <summary>Persists one session- or branch-scoped idempotency receipt.</summary>
    /// <typeparam name="TRequest">The cached request type.</typeparam>
    /// <typeparam name="TResult">The cached terminal result type.</typeparam>
    /// <param name="address">The addressed session.</param>
    /// <param name="scopeKind">One of the session-scoped <see cref="SqliteSessionIdempotencyScope"/> values.</param>
    /// <param name="branchId">The scoping branch for <see cref="SqliteSessionIdempotencyScope.Append"/>; otherwise <see langword="null"/>.</param>
    /// <param name="key">The caller idempotency key.</param>
    /// <param name="request">The accepted canonical request.</param>
    /// <param name="result">The terminal result returned to an equivalent replay.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask PutSessionScopeReceiptAsync<TRequest, TResult>(
        string scopeKind, SessionAddress address, BranchId? branchId, IdempotencyKey key, TRequest request, TResult result,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResult : class
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.SessionScopeIdempotencyTable}
                (agent_id, session_id, scope_kind, branch_id, idempotency_key, request, result)
            VALUES ($agent, $session, $kind, $branch, $key, $request, $result);
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$kind", scopeKind);
        _ = command.Parameters.AddWithValue("$branch", branchId is { } branch ? ToText(branch.Value) : string.Empty);
        _ = command.Parameters.AddWithValue("$key", key.Value);
        _ = command.Parameters.AddWithValue("$request", Serialize(request));
        _ = command.Parameters.AddWithValue("$result", Serialize(result));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // ---- Tenant/agent-scoped idempotency (agentkit_store_scope_idempotency) ----

    /// <summary>Loads one create or delete receipt scoped by tenant, agent, and (for delete) established address.</summary>
    internal async ValueTask<IdempotencyReceipt<TRequest, TResult>?> GetStoreScopeReceiptAsync<TRequest, TResult>(
        string scopeKind, TenantId tenantId, AgentId agentId, SessionId? sessionId, IdempotencyKey key, CancellationToken cancellationToken)
        where TRequest : class
        where TResult : class
    {
        await using var command = CreateCommand($"""
            SELECT request, result FROM {SqliteSessionSchema.StoreScopeIdempotencyTable}
            WHERE scope_kind = $kind AND tenant_id = $tenant AND agent_id = $agent AND session_id = $session AND idempotency_key = $key;
            """);
        AddStoreScopeKey(command, scopeKind, tenantId, agentId, sessionId, key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return !await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || reader.IsDBNull(1)
            ? null
            : new IdempotencyReceipt<TRequest, TResult>(Deserialize<TRequest>((byte[]) reader[0]), Deserialize<TResult>((byte[]) reader[1]));
    }

    /// <summary>Loads a retained deleted-create request (no result) scoped by tenant, agent, and idempotency key.</summary>
    internal async ValueTask<TRequest?> GetDeletedCreateRequestAsync<TRequest>(
        TenantId tenantId, AgentId agentId, IdempotencyKey key, CancellationToken cancellationToken)
        where TRequest : class
    {
        await using var command = CreateCommand($"""
            SELECT request FROM {SqliteSessionSchema.StoreScopeIdempotencyTable}
            WHERE scope_kind = $kind AND tenant_id = $tenant AND agent_id = $agent AND session_id = '' AND idempotency_key = $key;
            """);
        AddStoreScopeKey(command, SqliteSessionIdempotencyScope.DeletedCreate, tenantId, agentId, sessionId: null, key);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] payload
            ? Deserialize<TRequest>(payload)
            : null;
    }

    /// <summary>Persists a successful session-creation receipt, indexed both by its retry key and by its resulting address.</summary>
    internal async ValueTask PutCreateReceiptAsync<TRequest, TResult>(
        TenantId tenantId, AgentId agentId, IdempotencyKey key, TRequest request, TResult result, SessionAddress createdAddress,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResult : class
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.StoreScopeIdempotencyTable}
                (scope_kind, tenant_id, agent_id, session_id, idempotency_key, request, result, address_agent_id, address_session_id)
            VALUES ($kind, $tenant, $agent, '', $key, $request, $result, $addressAgent, $addressSession);
            """);
        _ = command.Parameters.AddWithValue("$kind", SqliteSessionIdempotencyScope.Create);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        _ = command.Parameters.AddWithValue("$agent", ToText(agentId.Value));
        _ = command.Parameters.AddWithValue("$key", key.Value);
        _ = command.Parameters.AddWithValue("$request", Serialize(request));
        _ = command.Parameters.AddWithValue("$result", Serialize(result));
        _ = command.Parameters.AddWithValue("$addressAgent", ToText(createdAddress.AgentId.Value));
        _ = command.Parameters.AddWithValue("$addressSession", ToText(createdAddress.SessionId.Value));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Persists one delete receipt scoped by tenant, established address, and idempotency key.</summary>
    internal async ValueTask PutDeleteReceiptAsync<TRequest, TResult>(
        TenantId tenantId, SessionAddress address, IdempotencyKey key, TRequest request, TResult result, CancellationToken cancellationToken)
        where TRequest : class
        where TResult : class
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.StoreScopeIdempotencyTable}
                (scope_kind, tenant_id, agent_id, session_id, idempotency_key, request, result)
            VALUES ($kind, $tenant, $agent, $session, $key, $request, $result);
            """);
        _ = command.Parameters.AddWithValue("$kind", SqliteSessionIdempotencyScope.Delete);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        _ = command.Parameters.AddWithValue("$agent", ToText(address.AgentId.Value));
        _ = command.Parameters.AddWithValue("$session", ToText(address.SessionId.Value));
        _ = command.Parameters.AddWithValue("$key", key.Value);
        _ = command.Parameters.AddWithValue("$request", Serialize(request));
        _ = command.Parameters.AddWithValue("$result", Serialize(result));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// If a create receipt resolved to <paramref name="address"/>, moves it from the live create index into the
    /// deleted-create index so a later retry of that same creation key reports deletion instead of resurrecting it.
    /// </summary>
    /// <param name="address">The address of the session that was just deleted.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask MigrateCreateReceiptToDeletedAsync(SessionAddress address, CancellationToken cancellationToken)
    {
        string? tenantId = null, agentId = null, key = null;
        byte[]? request = null;
        await using (var find = CreateCommand($"""
            SELECT tenant_id, agent_id, idempotency_key, request FROM {SqliteSessionSchema.StoreScopeIdempotencyTable}
            WHERE scope_kind = $kind AND address_agent_id = $addressAgent AND address_session_id = $addressSession LIMIT 1;
            """))
        {
            _ = find.Parameters.AddWithValue("$kind", SqliteSessionIdempotencyScope.Create);
            _ = find.Parameters.AddWithValue("$addressAgent", ToText(address.AgentId.Value));
            _ = find.Parameters.AddWithValue("$addressSession", ToText(address.SessionId.Value));
            await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                tenantId = reader.GetString(0);
                agentId = reader.GetString(1);
                key = reader.GetString(2);
                request = (byte[]) reader[3];
            }
        }

        if (tenantId is null)
        {
            return;
        }

        await using (var delete = CreateCommand($"""
            DELETE FROM {SqliteSessionSchema.StoreScopeIdempotencyTable}
            WHERE scope_kind = $kind AND tenant_id = $tenant AND agent_id = $agent AND session_id = '' AND idempotency_key = $key;
            """))
        {
            _ = delete.Parameters.AddWithValue("$kind", SqliteSessionIdempotencyScope.Create);
            _ = delete.Parameters.AddWithValue("$tenant", tenantId);
            _ = delete.Parameters.AddWithValue("$agent", agentId);
            _ = delete.Parameters.AddWithValue("$key", key);
            _ = await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var insert = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.StoreScopeIdempotencyTable}
                (scope_kind, tenant_id, agent_id, session_id, idempotency_key, request, result)
            VALUES ($kind, $tenant, $agent, '', $key, $request, NULL);
            """);
        _ = insert.Parameters.AddWithValue("$kind", SqliteSessionIdempotencyScope.DeletedCreate);
        _ = insert.Parameters.AddWithValue("$tenant", tenantId);
        _ = insert.Parameters.AddWithValue("$agent", agentId);
        _ = insert.Parameters.AddWithValue("$key", key);
        _ = insert.Parameters.AddWithValue("$request", request);
        _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddStoreScopeKey(
        SqliteCommand command, string scopeKind, TenantId tenantId, AgentId agentId, SessionId? sessionId, IdempotencyKey key)
    {
        _ = command.Parameters.AddWithValue("$kind", scopeKind);
        _ = command.Parameters.AddWithValue("$tenant", tenantId.Value);
        _ = command.Parameters.AddWithValue("$agent", ToText(agentId.Value));
        _ = command.Parameters.AddWithValue("$session", sessionId is { } id ? ToText(id.Value) : string.Empty);
        _ = command.Parameters.AddWithValue("$key", key.Value);
    }
}
