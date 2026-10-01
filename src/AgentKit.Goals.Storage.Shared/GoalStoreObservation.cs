// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Wraps one goal-store operation in a trace span, a structured log, and a bounded metric.</summary>
/// <remarks>Instrumentation is observational only: every listener, logger, or clock failure is swallowed and never changes the operation's semantic result. The span carries identities and the bounded outcome, never goal content.</remarks>
internal static class GoalStoreObservation
{
    /// <summary>Runs one operation under observation.</summary>
    /// <typeparam name="TResult">The typed operation result.</typeparam>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="kind">The bounded operation.</param>
    /// <param name="goalId">The goal concerned, when known.</param>
    /// <param name="tenantId">The tenant concerned, when known.</param>
    /// <param name="operation">The operation to run.</param>
    /// <param name="failureOf">Extracts a typed failure from a result, or <see langword="null"/> for success.</param>
    /// <returns>The operation's own result, unchanged.</returns>
    internal static async ValueTask<TResult> ObserveAsync<TResult>(
        ILogger logger,
        TimeProvider time,
        string adapter,
        GoalStoreOperationKind kind,
        GoalId? goalId,
        TenantId? tenantId,
        Func<ValueTask<TResult>> operation,
        Func<TResult, GoalStoreFailure?> failureOf)
    {
        Debug.Assert(logger is not null, "Adapters supply a logger.");
        Debug.Assert(time is not null, "Adapters supply a clock.");
        Debug.Assert(operation is not null, "The operation to run is supplied.");
        var operationName = Name(kind);
        var started = TryTimestamp(time);
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(AgentKitTagNames.GoalStoreAdapter, adapter),
            new(AgentKitTagNames.GoalStoreOperation, operationName),
        };
        if (goalId is { } goal)
        {
            tags.Add(new(AgentKitTagNames.GoalId, goal.ToString()));
        }

        if (tenantId is { } tenant && !string.IsNullOrWhiteSpace(tenant.Value))
        {
            tags.Add(new(AgentKitTagNames.TenantId, tenant.Value));
        }

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.GoalStoreOperation, ActivityKind.Internal, tags);
        try
        {
            var result = await operation().ConfigureAwait(false);
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
            Safe(() => GoalStoreLog.Completed(logger, failure is null ? LogLevel.Information : LogLevel.Warning, adapter, operationName, outcome, goalId));
            Safe(() => GoalStoreMetrics.Record(adapter, operationName, outcome, TryElapsed(time, started)));
            return result;
        }
        catch (OperationCanceledException)
        {
            Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            Safe(() => GoalStoreLog.Cancelled(logger, adapter, operationName, goalId));
            Safe(() => GoalStoreMetrics.Record(adapter, operationName, "cancelled", TryElapsed(time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Safe(() => scope.Activity.SetFailed("faulted", errorType));
            Safe(() => GoalStoreLog.Faulted(logger, adapter, operationName, goalId, errorType));
            Safe(() => GoalStoreMetrics.Record(adapter, operationName, "faulted", TryElapsed(time, started)));
            throw;
        }
    }

    /// <summary>Gets the stable trace and metric name of an operation.</summary>
    /// <param name="kind">The defined operation.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(GoalStoreOperationKind kind) => kind switch
    {
        GoalStoreOperationKind.Create => "create",
        GoalStoreOperationKind.Load => "load",
        GoalStoreOperationKind.Transition => "transition",
        GoalStoreOperationKind.ReadChildren => "read_children",
        GoalStoreOperationKind.ReadIntents => "read_intents",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The goal-store operation is undefined."),
    };

    /// <summary>Gets the stable outcome name of a failure class.</summary>
    /// <param name="kind">The defined failure class.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(GoalStoreFailureKind kind) => kind switch
    {
        GoalStoreFailureKind.Denied => "denied",
        GoalStoreFailureKind.NotFound => "not_found",
        GoalStoreFailureKind.VersionConflict => "version_conflict",
        GoalStoreFailureKind.IdempotencyConflict => "idempotency_conflict",
        GoalStoreFailureKind.InvalidTransition => "invalid_transition",
        GoalStoreFailureKind.ScopeMismatch => "scope_mismatch",
        GoalStoreFailureKind.LimitExceeded => "limit_exceeded",
        GoalStoreFailureKind.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The goal-store failure class is undefined."),
    };

    private static long? TryTimestamp(TimeProvider time)
    {
        try { return time.GetTimestamp(); } catch (Exception) { return null; }
    }

    private static TimeSpan? TryElapsed(TimeProvider time, long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try { return time.GetElapsedTime(timestamp); } catch (Exception) { return null; }
    }

    /// <summary>Runs one observation and swallows any failure so instrumentation never changes a semantic result.</summary>
    /// <param name="observation">The observation to run.</param>
    internal static void Safe(Action observation)
    {
        try { observation(); } catch (Exception) { /* Observation never changes the semantic result. */ }
    }
}
