// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Answers the document planner's reads from queries inside the caller's write transaction.</summary>
/// <remarks>Every answer is consistent with the write that follows because the immediate transaction already holds the database write lock. The lookup is not thread-safe and lives for one operation.</remarks>
internal sealed class SqliteDocumentLookup: IDocumentLookup
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;
    private readonly JsonSerializerOptions _json;

    internal SqliteDocumentLookup(SqliteConnection connection, SqliteTransaction? transaction, JsonSerializerOptions json)
    {
        Debug.Assert(connection is not null, "A lookup needs an open connection.");
        Debug.Assert(json is not null, "A lookup needs the encoding contract.");
        _connection = connection;
        _transaction = transaction;
        _json = json;
    }

    public long NextSequence => Scalar("SELECT COALESCE(MAX(sequence), 0) + 1 FROM documents");

    public long CurrentGeneration => Scalar("SELECT COALESCE(MAX(generation), 0) FROM documents");

    public DocumentEntry? Find(TenantId tenant, DocumentId id)
    {
        using var command = Command("SELECT document FROM documents WHERE tenant = $tenant AND document_id = $id");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$id", SqliteMemoryDatabase.Encode(id.Value));
        return command.ExecuteScalar() is string document
            ? JsonSerializer.Deserialize<DocumentEntryDocument>(document, _json)?.ToDomain()
                ?? throw SqliteMemoryDatabase.Unavailable("A persisted document decoded to null.")
            : null;
    }

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
