// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores evaluation results in one host-local SQLite database.</summary>
/// <remarks>
/// <para>
/// Each result is one row holding its document, with a run table that pins every run to the plan identity and version of its
/// first result. Every append runs in an immediate transaction that reads stored state through the shared planner and writes the
/// result, so run pinning, idempotent replay, and the identity checks are atomic even across processes sharing the file. An
/// acknowledged append is committed and survives process loss and reopen.
/// </para>
/// <para>
/// SQLite provides durable local storage only: it implies no distributed lease, fencing, or atomicity with report exporters.
/// A writer that cannot take the write lock within the configured wait receives a typed unavailable rejection. The instance is
/// thread-safe.
/// </para>
/// </remarks>
public sealed class SqliteEvaluationResultStore: IEvaluationResultStore
{
    private const string _adapter = "sqlite";

    private readonly SqliteEvaluationStoreDatabase _database;
    private readonly TimeProvider _time;
    private readonly ILogger<SqliteEvaluationResultStore> _logger;

    /// <summary>Initializes a store bound to one host-authorized database without opening it.</summary>
    /// <param name="target">The non-null exact database and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable lock and size bounds.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public SqliteEvaluationResultStore(
        SqliteEvaluationStoreTarget target,
        SqliteEvaluationStoreSettings settings,
        TimeProvider time,
        ILogger<SqliteEvaluationResultStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        _database = new SqliteEvaluationStoreDatabase(target, settings);
        _time = time;
        _logger = logger ?? NullLogger<SqliteEvaluationResultStore>.Instance;
    }

    /// <summary>Creates or validates the schema and binds the database to its expected identity.</summary>
    /// <param name="cancellationToken">Cancels before initialization completes.</param>
    /// <returns>A task completed once the database is ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The database is missing, uninitialized, belongs to another instance, or has an unsupported schema.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured database at boot. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _database.EnsureInitialized(cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The database cannot be opened safely.</exception>
    public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EvaluationResultStoreObservation.ObserveAsync(
            _logger, _time, _adapter, EvaluationResultStoreOperationKind.Append, result.EvaluationRunId,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _database.EnsureInitialized(cancellationToken);
                string document;
                try
                {
                    document = System.Text.Encoding.UTF8.GetString(
                        EvaluationResultCodec.Encode(result, SqliteEvaluationResultLookup.Json, _database.Settings.MaximumRecordBytes));
                }
                catch (InvalidDataException)
                {
                    return new EvaluationStoreRejected(new EvaluationStoreFailure(
                        EvaluationStoreFailureKind.LimitExceeded, "The encoded result exceeds the configured record bound."));
                }

                try
                {
                    using var connection = _database.Open();
                    using var transaction = connection.BeginTransaction(deferred: false);
                    var lookup = new SqliteEvaluationResultLookup(connection, transaction);
                    var plan = EvaluationResultPlanner.PlanAppend(lookup, result);
                    if (plan.Kind == EvaluationAppendPlanKind.Applied)
                    {
                        Insert(connection, transaction, result, document);
                        transaction.Commit();
                    }

                    return plan.ToResult();
                }
                catch (SqliteException)
                {
                    return new EvaluationStoreRejected(new EvaluationStoreFailure(
                        EvaluationStoreFailureKind.Unavailable, "The evaluation database could not take the write lock or write the result."));
                }
            },
            static answer => (answer as EvaluationStoreRejected)?.Failure);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The database cannot be opened safely.</exception>
    public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EvaluationResultStoreObservation.ObserveAsync<EvaluationReadResult>(
            _logger, _time, _adapter, EvaluationResultStoreOperationKind.Read, query.EvaluationRunId,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _database.EnsureInitialized(cancellationToken);
                try
                {
                    using var connection = _database.Open();
                    using var transaction = connection.BeginTransaction(deferred: true);
                    return EvaluationResultPlanner.Read(new SqliteEvaluationResultLookup(connection, transaction), query);
                }
                catch (SqliteException)
                {
                    return new EvaluationReadRejected(new EvaluationStoreFailure(
                        EvaluationStoreFailureKind.Unavailable, "The evaluation database could not be read."));
                }
            },
            static answer => (answer as EvaluationReadRejected)?.Failure);
    }

    private static void Insert(SqliteConnection connection, SqliteTransaction transaction, EvaluationCaseResult result, string document)
    {
        using var run = connection.CreateCommand();
        run.Transaction = transaction;
        run.CommandText = "INSERT OR IGNORE INTO evaluation_runs(run_id, plan_id, plan_version) VALUES ($run, $plan, $version)";
        _ = run.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(result.EvaluationRunId.Value));
        _ = run.Parameters.AddWithValue("$plan", result.PlanId.Value);
        _ = run.Parameters.AddWithValue("$version", result.PlanVersion.Value);
        _ = run.ExecuteNonQuery();

        using var row = connection.CreateCommand();
        row.Transaction = transaction;
        row.CommandText = "INSERT INTO evaluation_results(run_id, case_ordinal, repetition, case_id, document) VALUES ($run, $ordinal, $repetition, $case, $document)";
        _ = row.Parameters.AddWithValue("$run", SqliteEvaluationStoreDatabase.Encode(result.EvaluationRunId.Value));
        _ = row.Parameters.AddWithValue("$ordinal", result.CaseOrdinal);
        _ = row.Parameters.AddWithValue("$repetition", result.Repetition);
        _ = row.Parameters.AddWithValue("$case", result.CaseId.Value);
        _ = row.Parameters.AddWithValue("$document", document);
        _ = row.ExecuteNonQuery();
    }
}
