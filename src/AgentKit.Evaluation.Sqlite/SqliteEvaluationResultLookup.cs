// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite;

/// <summary>Answers the shared planner lookups with queries inside one open transaction.</summary>
/// <remarks>The lookup is bound to a single connection and transaction, so every answer is consistent with the write that follows it. It is not thread-safe and lives only for one operation.</remarks>
internal sealed class SqliteEvaluationResultLookup: IEvaluationResultLookup
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;

    /// <summary>Initializes a lookup over an open transaction.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The active transaction.</param>
    internal SqliteEvaluationResultLookup(SqliteConnection connection, SqliteTransaction transaction)
    {
        Debug.Assert(connection is not null && transaction is not null, "The store supplies an open transaction.");
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>Gets the serializer options every document is encoded and decoded with.</summary>
    internal static JsonSerializerOptions Json { get; } = JsonStoreSerialization.CreateCanonicalOptions();

    /// <inheritdoc/>
    public EvaluationRunPin? FindRun(EvaluationRunId runId)
    {
        using var command = Command("SELECT plan_id, plan_version FROM evaluation_runs WHERE run_id = $run");
        _ = command.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(runId.Value));
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new EvaluationRunPin(new EvaluationPlanId(reader.GetString(0)), new EvaluationPlanVersion(reader.GetInt64(1)))
            : null;
    }

    /// <inheritdoc/>
    public EvaluationCaseResult? Find(EvaluationRunId runId, int caseOrdinal, int repetition)
    {
        using var command = Command("SELECT document FROM evaluation_results WHERE run_id = $run AND case_ordinal = $ordinal AND repetition = $repetition");
        _ = command.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(runId.Value));
        _ = command.Parameters.AddWithValue("$ordinal", caseOrdinal);
        _ = command.Parameters.AddWithValue("$repetition", repetition);
        return command.ExecuteScalar() is string document ? Decode(document) : null;
    }

    /// <inheritdoc/>
    public EvaluationCaseId? FindCaseAt(EvaluationRunId runId, int caseOrdinal)
    {
        using var command = Command("SELECT case_id FROM evaluation_results WHERE run_id = $run AND case_ordinal = $ordinal LIMIT 1");
        _ = command.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(runId.Value));
        _ = command.Parameters.AddWithValue("$ordinal", caseOrdinal);
        return command.ExecuteScalar() is string caseId ? new EvaluationCaseId(caseId) : null;
    }

    /// <inheritdoc/>
    public int? FindOrdinalOf(EvaluationRunId runId, EvaluationCaseId caseId)
    {
        using var command = Command("SELECT case_ordinal FROM evaluation_results WHERE run_id = $run AND case_id = $case LIMIT 1");
        _ = command.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(runId.Value));
        _ = command.Parameters.AddWithValue("$case", caseId.Value);
        return command.ExecuteScalar() is long ordinal ? checked((int) ordinal) : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EvaluationCaseResult> Read(EvaluationRunId runId, EvaluationResultCursor? after, int limit)
    {
        Debug.Assert(limit > 0, "The planner requests a positive limit.");
        using var command = Command(
            "SELECT document FROM evaluation_results WHERE run_id = $run AND (case_ordinal > $ordinal OR (case_ordinal = $ordinal AND repetition > $repetition)) ORDER BY case_ordinal, repetition LIMIT $limit");
        _ = command.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(runId.Value));
        _ = command.Parameters.AddWithValue("$ordinal", after?.CaseOrdinal ?? -1);
        _ = command.Parameters.AddWithValue("$repetition", after?.Repetition ?? 0);
        _ = command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        var results = new List<EvaluationCaseResult>();
        while (reader.Read())
        {
            results.Add(Decode(reader.GetString(0)));
        }

        return results;
    }

    private static EvaluationCaseResult Decode(string document)
    {
        try
        {
            return EvaluationResultCodec.Decode(System.Text.Encoding.UTF8.GetBytes(document), Json);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            throw SqliteEvaluationStoreDatabase.Unavailable("A stored evaluation result is not valid evidence.", exception);
        }
    }

    private SqliteCommand Command(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }
}
