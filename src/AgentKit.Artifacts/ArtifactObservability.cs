// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Isolates tracing, logging, and metrics from authoritative artifact outcomes.</summary>
/// <remarks>Only identities and bounded labels are observed. Content, media types, locators, metadata values, and idempotency keys never appear in any signal, and a failing listener or logger never changes a result.</remarks>
internal static class ArtifactObservability
{
    /// <summary>Runs one operation under a correlated activity, structured log, and bounded metrics.</summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="target">The operation identity.</param>
    /// <param name="action">The operation to run.</param>
    /// <param name="classify">Maps a result to its bounded outcome label and whether it succeeded.</param>
    /// <returns>The operation result, unchanged.</returns>
    internal static async Task<TResult> ObserveAsync<TResult>(
        ArtifactObservationTarget target,
        Func<Task<TResult>> action,
        Func<TResult, (string Outcome, bool Succeeded)> classify)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(classify);
        var started = Timestamp(target.Time);
        var tenantText = target.TenantId?.Value;
        var artifactText = target.ArtifactId?.ToString();
        var preparationText = target.PreparationId?.ToString();
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(AgentKitTagNames.ArtifactCoordinatorKey, target.CoordinatorKey.Value),
            new(AgentKitTagNames.ArtifactOperation, target.Operation),
        };
        if (target.TenantId is { } tenant)
        {
            tags.Add(new(AgentKitTagNames.TenantId, tenant.Value));
        }

        if (target.ArtifactId is { } artifact)
        {
            tags.Add(new(AgentKitTagNames.ArtifactId, artifact.ToString()));
        }

        if (target.PreparationId is { } preparation)
        {
            tags.Add(new(AgentKitTagNames.ArtifactPreparationId, preparation.ToString()));
        }

        using var scope = AgentKitActivityScope.Start(target.ActivityName, ActivityKind.Internal, tags);
        try
        {
            var result = await action().ConfigureAwait(false);
            var (outcome, succeeded) = classify(result);
            Safe(() =>
            {
                if (succeeded)
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    scope.Activity.SetFailed(outcome, outcome);
                }
            });
            Safe(() => ArtifactLog.Completed(
                target.Logger, succeeded ? LogLevel.Debug : LogLevel.Information, target.CoordinatorKey.Value, target.Operation,
                tenantText, artifactText, preparationText, outcome));
            Safe(() => ArtifactMetrics.Record(target.Operation, outcome, Elapsed(target.Time, started)));
            return result;
        }
        catch (OperationCanceledException)
        {
            Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            Safe(() => ArtifactLog.Cancelled(target.Logger, target.CoordinatorKey.Value, target.Operation));
            Safe(() => ArtifactMetrics.Record(target.Operation, "cancelled", Elapsed(target.Time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Safe(() => scope.Activity.SetFailed("faulted", errorType));
            Safe(() => ArtifactLog.Failed(
                target.Logger, target.CoordinatorKey.Value, target.Operation, tenantText,
                artifactText, preparationText, errorType));
            Safe(() => ArtifactMetrics.Record(target.Operation, "faulted", Elapsed(target.Time, started)));
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

    private static long? Timestamp(TimeProvider time)
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

    private static TimeSpan? Elapsed(TimeProvider time, long? started)
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
