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
        var tags = new ActivityTagsCollection();
        if (agentId is { } agent)
        {
            tags.Add(AgentKitTagNames.AgentId, agent.ToString());
        }

        if (sessionId is { } session)
        {
            tags.Add(AgentKitTagNames.SessionId, session.ToString());
        }

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.SimpleAsk, ActivityKind.Internal, tags);
        try
        {
            var result = await action().ConfigureAwait(false);
            TryObserve(() =>
            {
                scope.Activity.SetSuccessful("succeeded");
                SimpleLog.Completed(logger, agentId?.ToString(), sessionId?.ToString(), "succeeded");
            });
            return result;
        }
        catch (OperationCanceledException)
        {
            TryObserve(() =>
            {
                scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException));
                SimpleLog.Completed(logger, agentId?.ToString(), sessionId?.ToString(), "cancelled");
            });
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            TryObserve(() =>
            {
                scope.Activity.SetFailed("faulted", errorType);
                SimpleLog.Failed(logger, agentId?.ToString(), sessionId?.ToString(), errorType);
            });
            throw;
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
