// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Answers the vector planner's reads from queries inside the caller's write transaction.</summary>
/// <remarks>Every answer is consistent with the write that follows because the immediate transaction already holds the database write lock. The lookup is not thread-safe and lives for one operation.</remarks>
internal sealed class SqliteVectorLookup: IVectorLookup
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;
    private readonly JsonSerializerOptions _json;

    internal SqliteVectorLookup(SqliteConnection connection, SqliteTransaction? transaction, JsonSerializerOptions json)
    {
        Debug.Assert(connection is not null, "A lookup needs an open connection.");
        Debug.Assert(json is not null, "A lookup needs the encoding contract.");
        _connection = connection;
        _transaction = transaction;
        _json = json;
    }

    public long Watermark => Scalar("SELECT watermark FROM vector_metadata");

    public VectorReceipt? FindReceipt(TenantId tenant, string key)
    {
        using var command = Command("SELECT document FROM vector_receipts WHERE tenant = $tenant AND receipt_key = $key");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() is string document
            ? JsonSerializer.Deserialize<VectorReceiptDocument>(document, _json)?.ToDomain()
                ?? throw SqliteMemoryDatabase.Unavailable("A persisted vector receipt decoded to null.")
            : null;
    }

    public VectorEntry? Find(TenantId tenant, ChunkId chunkId)
    {
        using var command = Command("SELECT document FROM vectors WHERE tenant = $tenant AND chunk_id = $chunk");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$chunk", SqliteMemoryDatabase.Encode(chunkId.Value));
        return command.ExecuteScalar() is string document ? Decode(tenant, document) : null;
    }

    public IEnumerable<VectorEntry> Scan(TenantId tenant, AgentId agent)
    {
        using var command = Command("SELECT document FROM vectors WHERE tenant = $tenant AND agent_id = $agent");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$agent", SqliteMemoryDatabase.Encode(agent.Value));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return Decode(tenant, reader.GetString(0));
        }
    }

    private VectorEntry Decode(TenantId tenant, string document) => new(
        tenant,
        JsonSerializer.Deserialize<VectorRecordDocument>(document, _json)?.ToDomain()
            ?? throw SqliteMemoryDatabase.Unavailable("A persisted vector decoded to null."));

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
