// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Emits failure-isolated diagnostics for one pinned-agent admission.</summary>
internal static class AgentAdmissionObservability
{
    private static readonly Lock _counterLock = new();
    private static Counter<long>? _admissions;

    /// <summary>Starts a safe admission activity with the non-content agent correlation identity.</summary>
    /// <param name="agentId">The validated agent identity being admitted.</param>
    /// <returns>A sampled activity, or <see langword="null"/> when diagnostics cannot be started safely.</returns>
    internal static Activity? Start(AgentId agentId)
    {
        try
        {
            return AgentKitDiagnostics.Activities.StartActivity(
                AgentKitActivityNames.AgentAdmission,
                ActivityKind.Internal,
                Activity.Current?.Context ?? default,
                tags: new ActivityTagsCollection { { AgentKitTagNames.AgentId, agentId.ToString() } });
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Completes diagnostics without allowing listeners, meters, or logs to affect admission.</summary>
    /// <param name="activity">The activity started for this admission, if sampling permitted one.</param>
    /// <param name="logger">The logger receiving the content-free terminal outcome.</param>
    /// <param name="outcome">The bounded terminal admission outcome.</param>
    /// <param name="errorType">The optional normalized error type for a non-success outcome.</param>
    internal static void Complete(Activity? activity, ILogger logger, string outcome, string? errorType = null)
    {
        try
        {
            if (errorType is null)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, errorType);
            }

            GetCounter()?.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            LogCompleted(logger, outcome);
        }
        catch (Exception)
        {
        }
        finally
        {
            try
            {
                activity?.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Logs cancellation without allowing a logger failure to alter control flow.</summary>
    /// <param name="logger">The logger receiving the content-free cancellation event.</param>
    internal static void LogCancelled(ILogger logger)
    {
        try { AgentAdmissionLog.Cancelled(logger); } catch (Exception) { }
    }

    /// <summary>Logs a failure without allowing a logger failure to alter the original exception.</summary>
    /// <param name="logger">The logger receiving the content-free failure event.</param>
    /// <param name="errorType">The normalized exception type.</param>
    internal static void LogFailed(ILogger logger, string errorType)
    {
        try { AgentAdmissionLog.Failed(logger, errorType); } catch (Exception) { }
    }

    private static Counter<long>? GetCounter()
    {
        lock (_counterLock)
        {
            try { return _admissions ??= AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.AgentAdmissionCount); } catch (Exception) { return null; }
        }
    }

    private static void LogCompleted(ILogger logger, string outcome)
    {
        try { AgentAdmissionLog.Completed(logger, outcome); } catch (Exception) { }
    }
}
