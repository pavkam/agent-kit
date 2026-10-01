// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Stores evaluation results as a flushed newline-delimited JSON log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged append writes one line holding the complete result and is flushed before the call returns, so an
/// acknowledged result survives process loss. Live state is projected during initialization by replaying the log through the
/// same shared planner state the in-memory adapter runs; a torn trailing append is recovered or refused according to the target
/// recovery mode, and a log whose records conflict with each other is refused.
/// </para>
/// <para>
/// The adapter holds an advisory exclusive lock on its root and rejects a second writer, so it claims no multi-process
/// coordination, distributed fencing, or atomicity with report exporters. The instance is thread-safe.
/// </para>
/// </remarks>
public sealed class JsonEvaluationResultStore: IEvaluationResultStore, IDisposable
{
    private const string _adapter = "json";
    private const string _storeKind = "agentkit.evaluation.results";

    private readonly JsonEvaluationStoreFile _file;
    private readonly EvaluationResultState _state = new();
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly ILogger<JsonEvaluationResultStore> _logger;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a store bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public JsonEvaluationResultStore(
        JsonEvaluationStoreTarget target,
        JsonEvaluationStoreSettings settings,
        TimeProvider time,
        ILogger<JsonEvaluationResultStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _logger = logger ?? NullLogger<JsonEvaluationResultStore>.Instance;
        _file = new JsonEvaluationStoreFile(target, settings, _storeKind, _logger);
    }

    /// <summary>Validates or creates the root, binds its encoding contract, and replays recorded results into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <remarks>Calling this is optional: the first operation initializes the store. Calling it during trusted host startup surfaces a misconfigured root at boot. Repeating it after success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureInitialized(cancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <exception cref="InvalidOperationException">The root cannot be opened safely.</exception>
    public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return EvaluationResultStoreObservation.ObserveAsync(
            _logger, _time, _adapter, EvaluationResultStoreOperationKind.Append, result.EvaluationRunId,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    var plan = EvaluationResultPlanner.PlanAppend(_state, result);
                    if (plan.Kind != EvaluationAppendPlanKind.Applied)
                    {
                        return plan.ToResult();
                    }

                    byte[] record;
                    try
                    {
                        record = _file.Encode(result);
                    }
                    catch (InvalidDataException)
                    {
                        return new EvaluationStoreRejected(new EvaluationStoreFailure(
                            EvaluationStoreFailureKind.LimitExceeded, "The encoded result exceeds the configured record bound."));
                    }

                    try
                    {
                        _file.Append(record, cancellationToken);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        return new EvaluationStoreRejected(new EvaluationStoreFailure(
                            EvaluationStoreFailureKind.Unavailable, "The result log could not be written."));
                    }

                    _state.Add(result);
                    return plan.ToResult();
                }
            },
            static answer => (answer as EvaluationStoreRejected)?.Failure);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <exception cref="InvalidOperationException">The root cannot be opened safely.</exception>
    public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return EvaluationResultStoreObservation.ObserveAsync<EvaluationReadResult>(
            _logger, _time, _adapter, EvaluationResultStoreOperationKind.Read, query.EvaluationRunId,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    EnsureInitialized(cancellationToken);
                    return EvaluationResultPlanner.Read(_state, query);
                }
            },
            static answer => (answer as EvaluationReadRejected)?.Failure);
    }

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _file.Dispose();
        }
    }

    private void EnsureInitialized(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        foreach (var record in _file.Open(cancellationToken))
        {
            EvaluationCaseResult result;
            try
            {
                result = _file.Decode(record.Span);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
            {
                _file.Dispose();
                throw new InvalidOperationException("The JSON evaluation-store log contains a record that is not valid evidence.", exception);
            }

            if (EvaluationResultPlanner.PlanAppend(_state, result).Kind != EvaluationAppendPlanKind.Applied)
            {
                _file.Dispose();
                throw new InvalidOperationException("The JSON evaluation-store log contains conflicting or duplicate records.");
            }

            _state.Add(result);
        }

        _initialized = true;
    }
}
