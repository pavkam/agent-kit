// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Answers the memory planner's reads from queries inside the caller's write transaction.</summary>
/// <remarks>Every answer is consistent with the write that follows because the immediate transaction already holds the database write lock. The lookup is not thread-safe and lives for one operation.</remarks>
internal sealed class SqliteMemoryLookup: IMemoryLookup
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;
    private readonly JsonSerializerOptions _json;

    internal SqliteMemoryLookup(SqliteConnection connection, SqliteTransaction? transaction, JsonSerializerOptions json)
    {
        Debug.Assert(connection is not null, "A lookup needs an open connection.");
        Debug.Assert(json is not null, "A lookup needs the encoding contract.");
        _connection = connection;
        _transaction = transaction;
        _json = json;
    }

    public long NextSequence => Scalar("SELECT COALESCE(MAX(sequence), 0) + 1 FROM memories");

    public long CurrentGeneration => Scalar("SELECT COALESCE(MAX(generation), 0) FROM memories");

    public MemoryEntry? FindByCreateKey(TenantId tenant, string key)
    {
        using var command = Command("SELECT memory_id FROM memory_creations WHERE tenant = $tenant AND creation_key = $key");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() is byte[] id ? Find(tenant, new MemoryId(new Guid(id))) : null;
    }

    public MemoryEntry? Find(TenantId tenant, MemoryId id)
    {
        using var command = Command("SELECT document FROM memories WHERE tenant = $tenant AND memory_id = $id");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$id", SqliteMemoryDatabase.Encode(id.Value));
        return command.ExecuteScalar() is string document ? Decode(document) : null;
    }

    public IEnumerable<MemoryEntry> Scan(TenantId tenant, AgentId agent, long afterSequence)
    {
        using var command = Command("SELECT document FROM memories WHERE tenant = $tenant AND agent_id = $agent AND sequence > $after ORDER BY sequence");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$agent", SqliteMemoryDatabase.Encode(agent.Value));
        _ = command.Parameters.AddWithValue("$after", afterSequence);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return Decode(reader.GetString(0));
        }
    }

    private MemoryEntry Decode(string document) =>
        JsonSerializer.Deserialize<MemoryEntryDocument>(document, _json)?.ToDomain()
            ?? throw SqliteMemoryDatabase.Unavailable("A persisted memory document decoded to null.");

    private long Scalar(string sql)
    {
        using var command = Command(sql);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private SqliteCommand Command(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }
}
