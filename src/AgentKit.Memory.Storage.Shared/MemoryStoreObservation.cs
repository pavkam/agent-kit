// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Wraps one store operation in a trace span, a structured log, and a bounded metric.</summary>
/// <remarks>Instrumentation is observational only: every listener, logger, or clock failure is swallowed and never changes the operation's semantic result. The span carries the tenant and the bounded outcome, never content.</remarks>
internal static class MemoryStoreObservation
{
    /// <summary>Runs one operation under observation.</summary>
    /// <typeparam name="TResult">The typed operation result.</typeparam>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="family">The state family.</param>
    /// <param name="kind">The bounded operation.</param>
    /// <param name="tenantId">The tenant concerned, when known.</param>
    /// <param name="operation">The operation to run.</param>
    /// <param name="failureOf">Extracts a typed failure from a result, or <see langword="null"/> for success.</param>
    /// <returns>The operation's own result, unchanged.</returns>
    internal static async ValueTask<TResult> ObserveAsync<TResult>(
        ILogger logger,
        TimeProvider time,
        string adapter,
        MemoryStoreFamily family,
        MemoryStoreOperationKind kind,
        TenantId? tenantId,
        Func<ValueTask<TResult>> operation,
        Func<TResult, MemoryStoreFailure?> failureOf)
    {
        Debug.Assert(logger is not null, "Adapters supply a logger.");
        Debug.Assert(time is not null, "Adapters supply a clock.");
        Debug.Assert(operation is not null, "The operation to run is supplied.");
        var familyName = Name(family);
        var operationName = Name(kind);
        var started = TryTimestamp(time);
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(AgentKitTagNames.MemoryStoreAdapter, adapter),
            new(AgentKitTagNames.MemoryStoreFamily, familyName),
            new(AgentKitTagNames.MemoryStoreOperation, operationName),
        };
        if (tenantId is { } tenant && !string.IsNullOrWhiteSpace(tenant.Value))
        {
            tags.Add(new(AgentKitTagNames.TenantId, tenant.Value));
        }

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.MemoryStoreOperation, ActivityKind.Internal, tags);
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
            Safe(() => MemoryStoreLog.Completed(logger, failure is null ? LogLevel.Information : LogLevel.Warning, adapter, familyName, operationName, outcome));
            Safe(() => MemoryStoreMetrics.Record(adapter, familyName, operationName, outcome, TryElapsed(time, started)));
            return result;
        }
        catch (OperationCanceledException)
        {
            Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            Safe(() => MemoryStoreLog.Cancelled(logger, adapter, familyName, operationName));
            Safe(() => MemoryStoreMetrics.Record(adapter, familyName, operationName, "cancelled", TryElapsed(time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Safe(() => scope.Activity.SetFailed("faulted", errorType));
            Safe(() => MemoryStoreLog.Faulted(logger, adapter, familyName, operationName, errorType));
            Safe(() => MemoryStoreMetrics.Record(adapter, familyName, operationName, "faulted", TryElapsed(time, started)));
            throw;
        }
    }

    /// <summary>Gets the stable name of a state family.</summary>
    /// <param name="family">The defined family.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="family"/> is undefined.</exception>
    internal static string Name(MemoryStoreFamily family) => family switch
    {
        MemoryStoreFamily.Memory => "memory",
        MemoryStoreFamily.Document => "document",
        MemoryStoreFamily.Vector => "vector",
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "The memory-store family is undefined."),
    };

    /// <summary>Gets the stable trace and metric name of an operation.</summary>
    /// <param name="kind">The defined operation.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(MemoryStoreOperationKind kind) => kind switch
    {
        MemoryStoreOperationKind.Write => "write",
        MemoryStoreOperationKind.Read => "read",
        MemoryStoreOperationKind.List => "list",
        MemoryStoreOperationKind.Transition => "transition",
        MemoryStoreOperationKind.Delete => "delete",
        MemoryStoreOperationKind.Activate => "activate",
        MemoryStoreOperationKind.Upsert => "upsert",
        MemoryStoreOperationKind.Search => "search",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The memory-store operation is undefined."),
    };

    /// <summary>Gets the stable outcome name of a failure class.</summary>
    /// <param name="kind">The defined failure class.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(MemoryStoreFailureKind kind) => kind switch
    {
        MemoryStoreFailureKind.Denied => "denied",
        MemoryStoreFailureKind.NotFound => "not_found",
        MemoryStoreFailureKind.VersionConflict => "version_conflict",
        MemoryStoreFailureKind.IdempotencyConflict => "idempotency_conflict",
        MemoryStoreFailureKind.InvalidTransition => "invalid_transition",
        MemoryStoreFailureKind.ScopeMismatch => "scope_mismatch",
        MemoryStoreFailureKind.IncompatibleVectorSpace => "incompatible_vector_space",
        MemoryStoreFailureKind.LimitExceeded => "limit_exceeded",
        MemoryStoreFailureKind.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The memory-store failure class is undefined."),
    };

    /// <summary>Runs one observation and swallows any failure so instrumentation never changes a semantic result.</summary>
    /// <param name="observation">The observation to run.</param>
    internal static void Safe(Action observation)
    {
        try { observation(); } catch (Exception) { /* Observation never changes the semantic result. */ }
    }

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
}
