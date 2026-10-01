// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Isolates tracing, logging, and metrics from authoritative artifact store outcomes.</summary>
/// <remarks>Only bounded adapter, operation, and outcome labels and the tenant identity are observed; artifact content, media types, locators, and idempotency keys never appear in any signal.</remarks>
internal static class ArtifactStoreObservation
{
    /// <summary>Runs one store operation under a correlated activity, structured log, and bounded metrics.</summary>
    /// <typeparam name="TResult">The store result type.</typeparam>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="time">The clock used only for observational duration.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="kind">The store operation.</param>
    /// <param name="tenantId">The tenant partition, which may be high-cardinality on traces and logs only.</param>
    /// <param name="operation">The operation to run.</param>
    /// <param name="failureOf">Extracts the typed failure from a result, or <see langword="null"/> for success.</param>
    /// <returns>The operation's result, unchanged.</returns>
    internal static async Task<TResult> ObserveAsync<TResult>(
        ILogger logger,
        TimeProvider time,
        string adapter,
        ArtifactStoreOperationKind kind,
        TenantId tenantId,
        Func<Task<TResult>> operation,
        Func<TResult, ArtifactFailure?> failureOf)
    {
        Debug.Assert(logger is not null, "Adapters supply a logger.");
        Debug.Assert(time is not null, "Adapters supply a clock.");
        Debug.Assert(operation is not null, "The operation to run is supplied.");
        var operationName = Name(kind);
        var started = TryTimestamp(time);
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(AgentKitTagNames.ArtifactStoreAdapter, adapter),
            new(AgentKitTagNames.ArtifactStoreOperation, operationName),
        };
        if (!string.IsNullOrWhiteSpace(tenantId.Value))
        {
            tags.Add(new(AgentKitTagNames.TenantId, tenantId.Value));
        }

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.ArtifactStoreOperation, ActivityKind.Internal, tags);
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
            Safe(() => ArtifactStoreLog.Completed(logger, failure is null ? LogLevel.Information : LogLevel.Warning, adapter, operationName, outcome));
            Safe(() => ArtifactStoreMetrics.Record(adapter, operationName, outcome, TryElapsed(time, started)));
            return result;
        }
        catch (OperationCanceledException)
        {
            Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            Safe(() => ArtifactStoreLog.Cancelled(logger, adapter, operationName));
            Safe(() => ArtifactStoreMetrics.Record(adapter, operationName, "cancelled", TryElapsed(time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Safe(() => scope.Activity.SetFailed("faulted", errorType));
            Safe(() => ArtifactStoreLog.Faulted(logger, adapter, operationName, errorType));
            Safe(() => ArtifactStoreMetrics.Record(adapter, operationName, "faulted", TryElapsed(time, started)));
            throw;
        }
    }

    /// <summary>Runs an observation step and swallows its failure so observation never changes a semantic result.</summary>
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

    /// <summary>Names a failure class as a bounded outcome label.</summary>
    /// <param name="kind">The failure class.</param>
    /// <returns>The stable snake-case label.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    internal static string Name(ArtifactFailureKind kind) => kind switch
    {
        ArtifactFailureKind.Denied => "denied",
        ArtifactFailureKind.LimitExceeded => "limit_exceeded",
        ArtifactFailureKind.IntegrityMismatch => "integrity_mismatch",
        ArtifactFailureKind.NotFound => "not_found",
        ArtifactFailureKind.Conflict => "conflict",
        ArtifactFailureKind.RetentionConflict => "retention_conflict",
        ArtifactFailureKind.Unavailable => "unavailable",
        ArtifactFailureKind.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The artifact failure class is undefined."),
    };

    private static string Name(ArtifactStoreOperationKind kind) => kind switch
    {
        ArtifactStoreOperationKind.Prepare => "prepare",
        ArtifactStoreOperationKind.Finalize => "finalize",
        ArtifactStoreOperationKind.Abort => "abort",
        ArtifactStoreOperationKind.Read => "read",
        ArtifactStoreOperationKind.Delete => "delete",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The artifact store operation is undefined."),
    };

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
