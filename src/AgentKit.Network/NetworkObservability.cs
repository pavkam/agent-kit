// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Coordinates content-free Microsoft diagnostics around protected network stages.</summary>
internal static class NetworkObservability
{
    /// <summary>Runs one network stage under a causal client activity and records its unchanged result.</summary>
    /// <typeparam name="TResult">The typed terminal network result.</typeparam>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="activityName">The stable shared activity name.</param>
    /// <param name="stage">The bounded network stage name.</param>
    /// <param name="operationId">The causal network operation identity.</param>
    /// <param name="action">The protected stage implementation.</param>
    /// <param name="isSuccessful">The typed success classifier.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>The exact result returned by <paramref name="action"/>.</returns>
    /// <exception cref="ArgumentNullException">A delegate or logger is null.</exception>
    /// <exception cref="ArgumentException">An activity or stage name is empty.</exception>
    internal static async ValueTask<TResult> ObserveAsync<TResult>(
        ILogger logger,
        string activityName,
        string stage,
        NetworkOperationId operationId,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<TResult, bool> isSuccessful,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(isSuccessful);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(activityName, ActivityKind.Client);
        _ = activity?.SetTag(AgentKitTagNames.NetworkOperationId, operationId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.NetworkStage, stage);
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            var outcome = result?.GetType().Name.ToLowerInvariant() ?? "unknown";
            if (isSuccessful(result))
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
            }

            NetworkLog.Completed(logger, stage, operationId, outcome);
            NetworkMetrics.Record(stage, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            NetworkLog.Completed(logger, stage, operationId, "cancelled");
            NetworkMetrics.Record(stage, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            NetworkLog.Failed(logger, stage, operationId, errorType);
            NetworkMetrics.Record(stage, "failed");
            throw;
        }
    }
}
