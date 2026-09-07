// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Coordinates content-free diagnostics around the deterministic session-store boundary.</summary>
internal static class SessionStoreObservability
{
    /// <summary>Runs one concrete store operation under a correlated activity and bounded metric.</summary>
    /// <typeparam name="TResult">The typed terminal store result.</typeparam>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="operation">The bounded store operation name.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="sessionId">The session identity when already established.</param>
    /// <param name="action">The concrete store implementation.</param>
    /// <param name="isSuccessful">The typed success classifier.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>The exact result returned by <paramref name="action"/>.</returns>
    /// <exception cref="ArgumentNullException">A delegate or logger is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="operation"/> is empty.</exception>
    internal static async ValueTask<TResult> ObserveAsync<TResult>(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<TResult, bool> isSuccessful,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(isSuccessful);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.SessionStoreOperation);
        _ = activity?.SetTag(AgentKitTagNames.SessionOperation, operation);
        _ = activity?.SetTag(AgentKitTagNames.AgentId, agentId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.SessionId, sessionId?.ToString());
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

            SessionStoreLog.Completed(logger, operation, agentId, sessionId, outcome);
            SessionStoreMetrics.Record(operation, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            SessionStoreLog.Completed(logger, operation, agentId, sessionId, "cancelled");
            SessionStoreMetrics.Record(operation, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            SessionStoreLog.Failed(logger, operation, agentId, sessionId, errorType);
            SessionStoreMetrics.Record(operation, "failed");
            throw;
        }
    }
}
