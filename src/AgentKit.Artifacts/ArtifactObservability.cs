// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Isolates tracing, logging, and metrics from authoritative artifact outcomes.</summary>
internal static class ArtifactObservability
{
    /// <summary>Runs one prepare operation under correlated diagnostics.</summary>
    internal static async Task<ArtifactPrepareResult> ObservePrepareAsync(
        ILogger logger,
        TenantId? tenantId,
        ArtifactId? artifactId,
        ArtifactPreparationId? preparationId,
        Func<Task<ArtifactPrepareResult>> action)
    {
        return await ObserveAsync(
            logger,
            AgentKitActivityNames.ArtifactPrepare,
            "prepare",
            tenantId,
            artifactId,
            preparationId,
            action,
            static result => result is ArtifactPrepared ? "prepared" : "rejected",
            static result => result is ArtifactPrepared).ConfigureAwait(false);
    }

    /// <summary>Runs one finalize operation under correlated diagnostics.</summary>
    internal static async ValueTask<ArtifactFinalizeResult> ObserveFinalizeAsync(
        ILogger logger,
        ArtifactPreparationId preparationId,
        Func<ValueTask<ArtifactFinalizeResult>> action)
    {
        return await ObserveAsync(
            logger,
            AgentKitActivityNames.ArtifactFinalize,
            "finalize",
            tenantId: null,
            artifactId: null,
            preparationId,
            () => action().AsTask(),
            static result => result is ArtifactFinalized ? "finalized" : "rejected",
            static result => result is ArtifactFinalized).ConfigureAwait(false);
    }

    /// <summary>Runs one abort operation under correlated diagnostics.</summary>
    internal static async ValueTask<ArtifactAbortResult> ObserveAbortAsync(
        ILogger logger,
        ArtifactPreparationId preparationId,
        Func<ValueTask<ArtifactAbortResult>> action)
    {
        return await ObserveAsync(
            logger,
            AgentKitActivityNames.ArtifactAbort,
            "abort",
            tenantId: null,
            artifactId: null,
            preparationId,
            () => action().AsTask(),
            static result => result is ArtifactAborted ? "aborted" : "rejected",
            static result => result is ArtifactAborted).ConfigureAwait(false);
    }

    /// <summary>Runs one read operation under correlated diagnostics.</summary>
    internal static async ValueTask<ArtifactReadResult> ObserveReadAsync(
        ILogger logger,
        TenantId tenantId,
        ArtifactId artifactId,
        Func<ValueTask<ArtifactReadResult>> action)
    {
        return await ObserveAsync(
            logger,
            AgentKitActivityNames.ArtifactRead,
            "read",
            tenantId,
            artifactId,
            preparationId: null,
            () => action().AsTask(),
            static result => result is ArtifactReadOpened ? "read" : "rejected",
            static result => result is ArtifactReadOpened).ConfigureAwait(false);
    }

    /// <summary>Runs one delete operation under correlated diagnostics.</summary>
    internal static async ValueTask<ArtifactDeleteResult> ObserveDeleteAsync(
        ILogger logger,
        TenantId tenantId,
        ArtifactId artifactId,
        Func<ValueTask<ArtifactDeleteResult>> action)
    {
        return await ObserveAsync(
            logger,
            AgentKitActivityNames.ArtifactDelete,
            "delete",
            tenantId,
            artifactId,
            preparationId: null,
            () => action().AsTask(),
            static result => result is ArtifactDeleted ? "deleted" : "rejected",
            static result => result is ArtifactDeleted).ConfigureAwait(false);
    }

    private static async Task<TResult> ObserveAsync<TResult>(
        ILogger logger,
        string activityName,
        string operation,
        TenantId? tenantId,
        ArtifactId? artifactId,
        ArtifactPreparationId? preparationId,
        Func<Task<TResult>> action,
        Func<TResult, string> outcomeSelector,
        Func<TResult, bool> isSuccess)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(outcomeSelector);
        ArgumentNullException.ThrowIfNull(isSuccess);
        Activity? activity = null;
        TryObserve(() =>
        {
            activity = AgentKitDiagnostics.Activities.StartActivity(activityName);
            _ = activity?.SetTag(AgentKitTagNames.TenantId, tenantId?.ToString());
            _ = activity?.SetTag(AgentKitTagNames.ArtifactId, artifactId?.ToString());
            _ = activity?.SetTag(AgentKitTagNames.ArtifactPreparationId, preparationId?.ToString());
        });
        try
        {
            var result = await action().ConfigureAwait(false);
            var outcome = outcomeSelector(result);
            TryObserve(() =>
            {
                _ = activity?.SetTag(AgentKitTagNames.Outcome, outcome);
                _ = activity?.SetStatus(isSuccess(result) ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            });
            TryObserve(() => ArtifactLog.Completed(
                logger,
                operation,
                tenantId?.ToString(),
                artifactId?.ToString(),
                preparationId?.ToString(),
                outcome));
            TryObserve(() => ArtifactMetrics.Record(operation, outcome));
            return result;
        }
        catch (OperationCanceledException)
        {
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, "cancellation"));
            TryObserve(() => ArtifactLog.Completed(
                logger,
                operation,
                tenantId?.ToString(),
                artifactId?.ToString(),
                preparationId?.ToString(),
                "cancelled"));
            TryObserve(() => ArtifactMetrics.Record(operation, "cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, errorType));
            TryObserve(() => ArtifactLog.Failed(
                logger,
                operation,
                tenantId?.ToString(),
                artifactId?.ToString(),
                preparationId?.ToString(),
                errorType));
            TryObserve(() => ArtifactMetrics.Record(operation, "faulted"));
            throw;
        }
        finally
        {
            TryObserve(() => activity?.Dispose());
        }
    }

    private static void TryObserve(Action observation)
    {
        try
        {
            observation();
        }
        catch
        {
        }
    }
}
