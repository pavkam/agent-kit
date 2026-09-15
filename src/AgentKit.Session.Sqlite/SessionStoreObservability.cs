// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Coordinates content-free, failure-isolated diagnostics around the deterministic session-store boundary.</summary>
/// <remarks>All result classifications are selected from a fixed internal vocabulary, and activity, logger, or meter failures cannot replace the operation's result or exception.</remarks>
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
    /// <remarks>Successful results use <c>succeeded</c>, typed rejections use <c>rejected</c>, caller cancellation uses <c>cancelled</c>, and unexpected exceptions use <c>failed</c>.</remarks>
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
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.SessionStoreOperation,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.SessionOperation, operation },
                { AgentKitTagNames.AgentId, agentId.ToString() },
                { AgentKitTagNames.SessionId, sessionId?.ToString() },
            });
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            var succeeded = isSuccessful(result);
            var outcome = succeeded ? "succeeded" : "rejected";
            SafeSetActivity(() =>
            {
                if (succeeded)
                {
                    activityScope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    activityScope.Activity.SetFailed(outcome, outcome);
                }
            });
            SafeLog(() => SessionStoreLog.Completed(logger, operation, agentId, sessionId, outcome));
            SafeMetric(operation, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() =>
                activityScope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            SafeLog(() => SessionStoreLog.Completed(logger, operation, agentId, sessionId, "cancelled"));
            SafeMetric(operation, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activityScope.Activity.SetFailed("failed", errorType));
            SafeLog(() => SessionStoreLog.Failed(logger, operation, agentId, sessionId, errorType));
            SafeMetric(operation, "failed");
            throw;
        }
    }

    private static void SafeSetActivity(Action action)
    {
        Debug.Assert(action is not null, "A diagnostic activity mutation is required.");
        try
        {
            action();
        }
        catch (Exception)
        {
            // Observation failures never change session-store behavior.
        }
    }

    private static void SafeLog(Action action)
    {
        Debug.Assert(action is not null, "A diagnostic log write is required.");
        try
        {
            action();
        }
        catch (Exception)
        {
            // Observation failures never change session-store behavior.
        }
    }

    private static void SafeMetric(string operation, string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        try
        {
            SessionStoreMetrics.Record(operation, outcome);
        }
        catch (Exception)
        {
            // Observation failures never change session-store behavior.
        }
    }
}
