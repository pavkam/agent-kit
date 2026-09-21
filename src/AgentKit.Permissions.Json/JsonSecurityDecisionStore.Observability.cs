// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <content>Contains the content-free tracing, metric, and logging wrappers shared by every decision-store operation.</content>
public sealed partial class JsonSecurityDecisionStore
{
    private ValueTask ExecuteVoidAsync(string operation, Action action, SecurityRequestId? requestId = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous store action is required.");
        return new ValueTask(ExecuteAsync(operation, () =>
        {
            action();
            return true;
        }, requestId).AsTask());
    }

    private ValueTask<T> ExecuteAsync<T>(string operation, Func<T> action, SecurityRequestId? requestId = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous store action is required.");
        using var activity = StartActivity(operation, requestId);
        var measured = TryGetTimestamp(out var startedAt);
        try
        {
            var result = action();
            ObserveTerminal(activity.Activity, operation, "success", true, null, requestId, measured, startedAt);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            ObserveTerminal(activity.Activity, operation, "cancelled", false, null, requestId, measured, startedAt);
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            ObserveTerminal(activity.Activity, operation, "faulted", false, exception, requestId, measured, startedAt);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            ObserveTerminal(activity.Activity, operation, "unavailable", false, exception, requestId, measured, startedAt);
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException
            or FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            var mapped = Unavailable(_corruptEvidence,
                "Persisted JSON decision evidence is corrupt or unsupported.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped, requestId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            var mapped = Unavailable(_persistenceFailed,
                "The JSON decision-store root could not be written or read.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped, requestId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception)
        {
            ObserveTerminal(activity.Activity, operation, "faulted", false, exception, requestId, measured, startedAt);
            throw;
        }
    }

    private static AgentKitActivityScope StartActivity(string operation, SecurityRequestId? requestId)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        return AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityDecisionStoreOperation,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, operation },
                { AgentKitTagNames.SecurityRequestId, requestId?.ToString() },
            });
    }

    private void ObserveTerminal(
        Activity? activity,
        string operation,
        string outcome,
        bool successful,
        Exception? exception,
        SecurityRequestId? requestId,
        bool measured,
        long startedAt)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        try
        {
            if (successful)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, GetBoundedFailureCode(outcome, exception));
            }
        }
        catch
        {
            // Trace listeners are observational.
        }

        try
        {
            JsonSecurityDecisionStoreMetrics.Operations.Add(1,
                new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            if (measured)
            {
                JsonSecurityDecisionStoreMetrics.Duration.Record(
                    _timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                    new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            }
        }
        catch
        {
            // Meter listeners are observational.
        }

        try
        {
            var requestText = requestId?.ToString();
            if (successful)
            {
                JsonSecurityDecisionStoreLog.OperationCompleted(_logger, operation, outcome, requestText);
            }
            else
            {
                JsonSecurityDecisionStoreLog.OperationFailed(_logger, operation, outcome,
                    GetBoundedFailureCode(outcome, exception), requestText);
            }
        }
        catch
        {
            // Logging failures are observational.
        }
    }

    private bool TryGetTimestamp(out long timestamp)
    {
        try
        {
            timestamp = _timeProvider.GetTimestamp();
            return true;
        }
        catch
        {
            timestamp = 0;
            return false;
        }
    }

    private static string GetBoundedFailureCode(string outcome, Exception? exception) => exception switch
    {
        null => outcome,
        _ when exception.Data[_failureKindKey] is string kind => kind,
        _ => "faulted",
    };
}
