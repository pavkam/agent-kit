// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using System.Text.Json;

/// <content>Contains the content-free tracing, metric, and logging wrappers shared by every approval-store operation.</content>
public sealed partial class SqliteApprovalStore
{
    private ValueTask<T> ExecuteAsync<T>(
        string operation,
        Func<T> action,
        ApprovalRequestId? requestId = null,
        ApprovalResponseId? responseId = null,
        ApprovalScopeBinding? binding = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous store action is required.");
        using var activity = StartActivity(operation, binding);
        var measured = TryGetTimestamp(out var startedAt);
        try
        {
            var result = action();
            ObserveTerminal(activity.Activity, operation, GetOutcome(result), IsSuccessful(result), null,
                requestId, responseId, measured, startedAt);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            ObserveTerminal(activity.Activity, operation, "cancelled", false, null,
                requestId, responseId, measured, startedAt);
            throw;
        }
        catch (SecurityGrantStoreUnavailableException exception)
        {
            ObserveTerminal(activity.Activity, operation, "unavailable", false, exception,
                requestId, responseId, measured, startedAt);
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException
            or FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            var mapped = Unavailable(SecurityGrantStoreFailureKind.CorruptEvidence,
                "Persisted SQLite approval evidence is corrupt or unsupported.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                requestId, responseId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            var mapped = Unavailable(SecurityGrantStoreFailureKind.PersistenceFailed,
                "The SQLite approval-store target could not be written or read.", exception);
            ObserveTerminal(activity.Activity, operation, "unavailable", false, mapped,
                requestId, responseId, measured, startedAt);
            throw mapped;
        }
        catch (Exception exception)
        {
            ObserveTerminal(activity.Activity, operation, "faulted", false, exception,
                requestId, responseId, measured, startedAt);
            throw;
        }
    }

    private ValueTask ExecuteVoidAsync(string operation, Action action) =>
        new(ExecuteAsync(operation, () =>
        {
            action();
            return true;
        }).AsTask());

    private static AgentKitActivityScope StartActivity(string operation, ApprovalScopeBinding? binding)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        return AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityApprovalStoreOperation,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, operation },
                { AgentKitTagNames.SecurityRequestId, binding?.Request.Id.ToString() },
                { AgentKitTagNames.AgentId, binding?.Request.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, binding?.Request.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, binding?.Request.Scope.Correlation.OperationId.ToString() },
            });
    }

    private void ObserveTerminal(
        Activity? activity,
        string operation,
        string outcome,
        bool successful,
        Exception? exception,
        ApprovalRequestId? requestId,
        ApprovalResponseId? responseId,
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
            // Observational only.
        }

        try
        {
            SqliteApprovalStoreMetrics.Operations.Add(1,
                new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            if (measured)
            {
                SqliteApprovalStoreMetrics.Duration.Record(
                    _timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    new KeyValuePair<string, object?>(AgentKitTagNames.GenAiOperationName, operation),
                    new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            }
        }
        catch
        {
            // Observational only.
        }

        try
        {
            var requestText = requestId?.ToString();
            var responseText = responseId?.ToString();
            if (successful)
            {
                SqliteApprovalStoreLog.OperationCompleted(_logger, operation, outcome, requestText, responseText);
            }
            else
            {
                SqliteApprovalStoreLog.OperationFailed(_logger, operation, outcome,
                    GetBoundedFailureCode(outcome, exception), requestText, responseText);
            }
        }
        catch
        {
            // Observational only.
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

    private static string GetOutcome<T>(T result) => result switch
    {
        ApprovalStoreCreateResult created => created.ToString().ToLowerInvariant(),
        ApprovalStoreResolveResult resolved => resolved.ToString().ToLowerInvariant(),
        _ => "success",
    };

    private static bool IsSuccessful<T>(T result) => result switch
    {
        ApprovalStoreCreateResult created =>
            created is ApprovalStoreCreateResult.Created or ApprovalStoreCreateResult.AlreadyExists,
        ApprovalStoreResolveResult resolved =>
            resolved is ApprovalStoreResolveResult.Resolved or ApprovalStoreResolveResult.AlreadyResolved,
        _ => true,
    };

    private static string GetBoundedFailureCode(string outcome, Exception? exception) => exception switch
    {
        SecurityGrantStoreUnavailableException unavailable => unavailable.Kind.ToString().ToLowerInvariant(),
        OperationCanceledException => "cancelled",
        null => outcome,
        _ => "faulted",
    };
}
