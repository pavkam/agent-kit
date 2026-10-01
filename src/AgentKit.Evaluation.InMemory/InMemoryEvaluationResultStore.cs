// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores evaluation results in process memory, ephemerally, under one lock.</summary>
/// <remarks>
/// The adapter runs the shared planner over an in-memory state, so identity, idempotency, run pinning, and ordering match the
/// durable adapters exactly. Results do not survive disposal or process exit. The instance is thread-safe; every operation is
/// short and synchronous under the lock.
/// </remarks>
public sealed class InMemoryEvaluationResultStore: IEvaluationResultStore
{
    private const string _adapter = "in_memory";

    private readonly EvaluationResultState _state = new();
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<InMemoryEvaluationResultStore> _logger;

    /// <summary>Initializes an empty store.</summary>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    public InMemoryEvaluationResultStore(TimeProvider time, ILogger<InMemoryEvaluationResultStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _logger = logger ?? NullLogger<InMemoryEvaluationResultStore>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return EvaluationResultStoreObservation.ObserveAsync(
            _logger, _time, _adapter, EvaluationResultStoreOperationKind.Append, result.EvaluationRunId,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    var plan = EvaluationResultPlanner.PlanAppend(_state, result);
                    if (plan.Kind == EvaluationAppendPlanKind.Applied)
                    {
                        _state.Add(result);
                    }

                    return plan.ToResult();
                }
            },
            static answer => (answer as EvaluationStoreRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EvaluationResultStoreObservation.ObserveAsync<EvaluationReadResult>(
            _logger, _time, _adapter, EvaluationResultStoreOperationKind.Read, query.EvaluationRunId,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    return EvaluationResultPlanner.Read(_state, query);
                }
            },
            static answer => (answer as EvaluationReadRejected)?.Failure);
    }
}
