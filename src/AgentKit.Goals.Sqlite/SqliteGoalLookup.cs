// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Answers the planner's reads from queries inside the caller's write transaction.</summary>
/// <remarks>Every answer is consistent with the write that follows because the immediate transaction already holds the database write lock. The lookup is not thread-safe and lives for one operation.</remarks>
internal sealed class SqliteGoalLookup: IGoalLookup
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction? _transaction;
    private readonly JsonSerializerOptions _json;

    internal SqliteGoalLookup(SqliteConnection connection, SqliteTransaction? transaction, JsonSerializerOptions json)
    {
        Debug.Assert(connection is not null, "A lookup needs an open connection.");
        Debug.Assert(json is not null, "A lookup needs the encoding contract.");
        _connection = connection;
        _transaction = transaction;
        _json = json;
    }

    public long NextSequence => Scalar("SELECT COALESCE(MAX(sequence), 0) + 1 FROM goals");

    public long NextSettledSequence => Scalar("SELECT COALESCE(MAX(settled_sequence), 0) + 1 FROM goals");

    public GoalRecord? FindByCreationKey(TenantId tenant, string key)
    {
        using var command = Command("SELECT goal_id FROM goal_creations WHERE tenant = $tenant AND creation_key = $key");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() is byte[] id ? Find(tenant, new GoalId(new Guid(id))) : null;
    }

    public GoalRecord? Find(TenantId tenant, GoalId goalId)
    {
        using var command = Command("SELECT document FROM goals WHERE tenant = $tenant AND goal_id = $goal");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$goal", SqliteGoalDatabase.Encode(goalId.Value));
        return command.ExecuteScalar() is string document ? Decode(document) : null;
    }

    public int CountChildren(TenantId tenant, GoalId parentId)
    {
        using var command = Command("SELECT COUNT(*) FROM goals WHERE tenant = $tenant AND parent_id = $parent");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$parent", SqliteGoalDatabase.Encode(parentId.Value));
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Reads one page of a goal's children in child-ordinal order.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="request">The children request.</param>
    /// <returns>Up to one more record than the page size, so the caller can tell whether another page exists.</returns>
    internal List<GoalRecord> ReadChildren(TenantId tenant, GoalChildrenRequest request)
    {
        using var command = Command(
            "SELECT document FROM goals WHERE tenant = $tenant AND parent_id = $parent AND child_ordinal > $after ORDER BY child_ordinal LIMIT $limit");
        _ = command.Parameters.AddWithValue("$tenant", tenant.Value);
        _ = command.Parameters.AddWithValue("$parent", SqliteGoalDatabase.Encode(request.ParentId.Value));
        _ = command.Parameters.AddWithValue("$after", request.AfterOrdinal);
        _ = command.Parameters.AddWithValue("$limit", request.Limit + 1);
        return Documents(command);
    }

    /// <summary>Reads one page of open delegated children across tenants in creation order.</summary>
    /// <param name="request">The scan request.</param>
    /// <returns>Up to one more record than the page size.</returns>
    internal List<GoalRecord> ReadIntents(GoalIntentScanRequest request)
    {
        using var command = Command(
            "SELECT document FROM goals WHERE delegated = 1 AND status IN ($ready, $active) AND sequence > $after ORDER BY sequence LIMIT $limit");
        _ = command.Parameters.AddWithValue("$ready", (int) GoalStatus.Ready);
        _ = command.Parameters.AddWithValue("$active", (int) GoalStatus.Active);
        _ = command.Parameters.AddWithValue("$after", request.AfterSequence);
        _ = command.Parameters.AddWithValue("$limit", request.Limit + 1);
        return Documents(command);
    }

    private List<GoalRecord> Documents(SqliteCommand command)
    {
        var records = new List<GoalRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(Decode(reader.GetString(0)));
        }

        return records;
    }

    private GoalRecord Decode(string document) =>
        JsonSerializer.Deserialize<PersistedGoalDocument>(document, _json)?.ToDomain().Record
            ?? throw SqliteGoalDatabase.Unavailable("A persisted goal document decoded to null.");

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
