// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <content>Contains the content-free tracing, metric, and logging wrappers shared by every grant-store operation.</content>
public sealed partial class JsonSecurityGrantStore
{
    private ValueTask<T> ExecuteAsync<T>(
        string operation,
        Func<T> action,
        SecurityGrant? grant = null,
        SecurityEnforcementIntent? intent = null,
        GrantId? grantId = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous store action is required.");
        using var activity = StartActivity(operation, grant, intent, grantId);
        var measured = TryGetTimestamp(out var startedAt);
        try
        {
            var result = action();
            var outcome = result is GrantConsumptionResult consumption
                ? consumption.Status.ToString().ToLowerInvariant()
                : "success";
            var successful = result is not GrantConsumptionResult terminal
                || terminal.Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled;
            ObserveTerminal(activity.Activity, operation, outcome, successful, null,
                grant, intent, grantId, measured, startedAt);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            ObserveTerminal(activity.Activity, operation, "cancelled", false, null,
                grant, intent, grantId, measured, startedAt);
            throw;
        }
        catch (SecurityGrantStoreUnavailableException exception)
        {
            ObserveTerminal(activity.Activity, operation, "unavailable", false, exception,
                grant, intent, grantId, measured, startedAt);
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException
            or FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            var mapped = Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "Persisted JSON security evidence is corrupt or unsupported.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                grant, intent, grantId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            var mapped = Unavailable(SecurityGrantStoreFailureKind.PersistenceFailed,
                "The JSON grant-store root could not be written or read.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                grant, intent, grantId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception)
        {
            ObserveTerminal(activity.Activity, operation, "faulted", false, exception,
                grant, intent, grantId, measured, startedAt);
            throw;
        }
    }

    private ValueTask ExecuteVoidAsync(string operation, Action action, SecurityGrant? grant = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous store action is required.");
        return new ValueTask(ExecuteAsync(operation, () =>
        {
            action();
            return true;
        }, grant).AsTask());
    }

    private static AgentKitActivityScope StartActivity(
        string operation,
        SecurityGrant? grant,
        SecurityEnforcementIntent? intent,
        GrantId? grantId)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        return AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityGrantStoreOperation,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, operation },
                { AgentKitTagNames.SecurityRequestId, grant?.RequestId.ToString() },
                { AgentKitTagNames.SecurityGrantId, (grant?.Id ?? grantId)?.ToString() },
                { AgentKitTagNames.SecurityEnforcementIntentId, intent?.Id.ToString() },
                { AgentKitTagNames.AgentId, grant?.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, grant?.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, grant?.Scope.Correlation.OperationId.ToString() },
            });
    }

    private void ObserveTerminal(
        Activity? activity,
        string operation,
        string outcome,
        bool successful,
        Exception? exception,
        SecurityGrant? grant,
        SecurityEnforcementIntent? intent,
        GrantId? grantId,
        bool measured,
        long startedAt)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        // Activity.SetSuccessful/SetFailed never throws for the always-bounded, nonblank outcome and failure-code
        // values produced here, so the catch has no reachable trigger; it guards only against a future regression.
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
            // Trace listeners are observational and never alter the semantic outcome.
        }

        try
        {
            JsonSecurityGrantStoreMetrics.Operations.Add(1,
                new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            if (measured)
            {
                JsonSecurityGrantStoreMetrics.Duration.Record(
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
            var requestText = grant?.RequestId.ToString();
            var grantText = (grant?.Id ?? grantId)?.ToString();
            var intentText = intent?.Id.ToString();
            if (successful)
            {
                JsonSecurityGrantStoreLog.OperationCompleted(
                    _logger, operation, outcome, requestText, grantText, intentText);
            }
            else
            {
                JsonSecurityGrantStoreLog.OperationFailed(
                    _logger, operation, outcome, GetBoundedFailureCode(outcome, exception),
                    requestText, grantText, intentText);
            }
        }
        catch
        {
            // Logging failures are observational and never alter the semantic outcome.
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
            // A replaced clock may refuse to supply a timestamp; measurement is observational and never blocks the store.
            timestamp = 0;
            return false;
        }
    }

    private static string GetBoundedFailureCode(string outcome, Exception? exception) => exception switch
    {
        SecurityGrantStoreUnavailableException unavailable => unavailable.Kind.ToString().ToLowerInvariant(),
        null => outcome,
        _ => "faulted",
    };
}
