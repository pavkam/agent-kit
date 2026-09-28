// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Isolates diagnostics around one Simple ask without logging user text.</summary>
internal static class SimpleAskObservability
{
    internal static async Task<string> RunAsync(
        ILogger logger,
        AgentId? agentId,
        SessionId? sessionId,
        Func<Task<string>> action)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(action);
        Activity? activity = null;
        TryObserve(() => activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.SimpleAsk));
        TryObserve(() =>
        {
            _ = activity?.SetTag(AgentKitTagNames.AgentId, agentId?.ToString());
            _ = activity?.SetTag(AgentKitTagNames.SessionId, sessionId?.ToString());
        });
        try
        {
            var result = await action().ConfigureAwait(false);
            TryObserve(() =>
            {
                _ = activity?.SetTag(AgentKitTagNames.Outcome, "succeeded");
                _ = activity?.SetStatus(ActivityStatusCode.Ok);
            });
            TryObserve(() => SimpleLog.Completed(logger, agentId?.ToString(), sessionId?.ToString(), "succeeded"));
            return result;
        }
        catch (OperationCanceledException)
        {
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, "cancellation"));
            TryObserve(() => SimpleLog.Completed(logger, agentId?.ToString(), sessionId?.ToString(), "cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, errorType));
            TryObserve(() => SimpleLog.Failed(logger, agentId?.ToString(), sessionId?.ToString(), errorType));
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
