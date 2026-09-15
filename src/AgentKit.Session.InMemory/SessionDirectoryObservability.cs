// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Coordinates failure-isolated content-free diagnostics around session-directory access.</summary>
internal static class SessionDirectoryObservability
{
    /// <summary>Runs one directory operation under a correlated activity and bounded terminal counter.</summary>
    /// <typeparam name="TResult">The typed terminal directory result.</typeparam>
    /// <param name="logger">The configured logger.</param><param name="operation">The bounded operation name.</param>
    /// <param name="agentId">The owning agent.</param><param name="sessionId">The session when established.</param>
    /// <param name="action">The directory operation.</param><param name="isSuccessful">The typed success classifier.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param><returns>The exact semantic result.</returns>
    internal static async ValueTask<TResult> ObserveAsync<TResult>(ILogger logger, string operation,
        AgentId agentId, SessionId? sessionId, Func<CancellationToken, ValueTask<TResult>> action,
        Func<TResult, bool> isSuccessful, CancellationToken cancellationToken)
        where TResult : class
    {
        Debug.Assert(logger is not null, "The directory owns a non-null logger.");
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded directory operation is required.");
        Debug.Assert(action is not null, "A directory operation delegate is required.");
        Debug.Assert(isSuccessful is not null, "A result classifier is required.");
        Activity? activity = null;
        TryObserve(() =>
        {
            activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.SessionDirectoryOperation);
            _ = activity?.SetTag(AgentKitTagNames.SessionOperation, operation);
            _ = activity?.SetTag(AgentKitTagNames.AgentId, agentId.ToString());
            _ = activity?.SetTag(AgentKitTagNames.SessionId, sessionId?.ToString());
        });
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action(cancellationToken).ConfigureAwait(false);
            var outcome = Outcome(result);
            TryObserve(() =>
            {
                _ = activity?.SetTag(AgentKitTagNames.Outcome, outcome);
                _ = activity?.SetStatus(isSuccessful(result) ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            });
            TryObserve(() => SessionDirectoryLog.Completed(logger, operation, agentId, sessionId, outcome));
            TryObserve(() => SessionDirectoryMetrics.Record(operation, outcome));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, "cancellation"));
            TryObserve(() => SessionDirectoryLog.Completed(logger, operation, agentId, sessionId, "cancelled"));
            TryObserve(() => SessionDirectoryMetrics.Record(operation, "cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, errorType));
            TryObserve(() => SessionDirectoryLog.Failed(logger, operation, agentId, sessionId, errorType));
            TryObserve(() => SessionDirectoryMetrics.Record(operation, "faulted"));
            throw;
        }
        finally
        {
            TryObserve(() => activity?.Dispose());
        }
    }

    private static string Outcome<TResult>(TResult result) where TResult : class => result switch
    {
        SessionLocated or SessionLocationNotFound or SessionCreationLocationLocated or
            SessionCreationLocationNotFound or SessionLocationRecorded or SessionDirectoryPage => "success",
        SessionDirectoryLookupDenied or SessionDirectoryLookupUnavailable or
            SessionCreationLocationConflict or SessionDirectoryCreationLookupDenied or
            SessionDirectoryCreationLookupUnavailable or SessionLocationConflict or
            SessionDirectoryWriteDenied or SessionDirectoryWriteUnavailable or
            SessionDirectoryListUnavailable => "failed",
        _ => "unknown",
    };

    private static void TryObserve(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try { observation(); } catch { }
    }
}
