// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Wraps one result-store operation in a trace span, a structured log, and a bounded metric.</summary>
/// <remarks>Instrumentation is observational only: every listener, logger, or clock failure is swallowed and never changes the operation's semantic result. The span carries the run identity and the bounded outcome, never result content.</remarks>
internal static class EvaluationResultStoreObservation
{
    /// <summary>Runs one operation under observation.</summary>
    /// <typeparam name="TResult">The typed operation result.</typeparam>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="kind">The bounded operation.</param>
    /// <param name="runId">The evaluation run concerned.</param>
    /// <param name="operation">The operation to run.</param>
    /// <param name="failureOf">Extracts a typed failure from a result, or <see langword="null"/> for success.</param>
    /// <returns>The operation's own result, unchanged.</returns>
    internal static ValueTask<TResult> ObserveAsync<TResult>(
        ILogger logger,
        TimeProvider time,
        string adapter,
        EvaluationResultStoreOperationKind kind,
        EvaluationRunId runId,
        Func<TResult> operation,
        Func<TResult, EvaluationStoreFailure?> failureOf)
    {
        Debug.Assert(logger is not null, "Adapters supply a logger.");
        Debug.Assert(time is not null, "Adapters supply a clock.");
        Debug.Assert(operation is not null, "The operation to run is supplied.");
        var operationName = Name(kind);
        var started = TryTimestamp(time);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.EvaluationStoreOperation,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.EvaluationStoreAdapter, adapter),
                new(AgentKitTagNames.EvaluationStoreOperation, operationName),
                new(AgentKitTagNames.EvaluationRunId, runId.ToString()),
            ]);
        try
        {
            var result = operation();
            var failure = failureOf(result);
            var outcome = failure is null ? "completed" : Name(failure.Kind);
            Safe(() =>
            {
                if (failure is null)
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    scope.Activity.SetFailed(outcome, outcome);
                }
            });
            Safe(() => EvaluationResultStoreLog.Completed(logger, failure is null ? LogLevel.Information : LogLevel.Warning, adapter, operationName, outcome, runId));
            Safe(() => EvaluationResultStoreMetrics.Record(adapter, operationName, outcome, TryElapsed(time, started)));
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            Safe(() => EvaluationResultStoreLog.Cancelled(logger, adapter, operationName, runId));
            Safe(() => EvaluationResultStoreMetrics.Record(adapter, operationName, "cancelled", TryElapsed(time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Safe(() => scope.Activity.SetFailed("faulted", errorType));
            Safe(() => EvaluationResultStoreLog.Faulted(logger, adapter, operationName, runId, errorType));
            Safe(() => EvaluationResultStoreMetrics.Record(adapter, operationName, "faulted", TryElapsed(time, started)));
            throw;
        }
    }

    /// <summary>Gets the stable trace and metric name of an operation.</summary>
    /// <param name="kind">The defined operation.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(EvaluationResultStoreOperationKind kind) => kind switch
    {
        EvaluationResultStoreOperationKind.Append => "append",
        EvaluationResultStoreOperationKind.Read => "read",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The evaluation result-store operation is undefined."),
    };

    /// <summary>Gets the stable outcome name of a failure class.</summary>
    /// <param name="kind">The defined failure class.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(EvaluationStoreFailureKind kind) => kind switch
    {
        EvaluationStoreFailureKind.IdentityConflict => "identity_conflict",
        EvaluationStoreFailureKind.LimitExceeded => "limit_exceeded",
        EvaluationStoreFailureKind.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The evaluation store failure class is undefined."),
    };

    /// <summary>Runs one observation and swallows any failure so instrumentation never changes a semantic result.</summary>
    /// <param name="observation">The observation to run.</param>
    internal static void Safe(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Observation never changes the semantic result.
        }
    }

    private static long? TryTimestamp(TimeProvider time)
    {
        try
        {
            return time.GetTimestamp();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static TimeSpan? TryElapsed(TimeProvider time, long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try
        {
            return time.GetElapsedTime(timestamp);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
