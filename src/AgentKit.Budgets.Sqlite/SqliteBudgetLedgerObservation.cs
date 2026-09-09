// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Isolates tracing, logging, timing, and metrics from authoritative SQLite outcomes.</summary>
internal sealed class SqliteBudgetLedgerObservation
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>Creates an observational boundary from replaceable collaborators.</summary>
    /// <param name="timeProvider">The non-null timing provider.</param><param name="logger">The non-null logger.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal SqliteBudgetLedgerObservation(TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Runs one semantic operation while isolating every observer callback.</summary>
    /// <typeparam name="T">The immutable semantic result.</typeparam><param name="operation">The bounded operation name.</param>
    /// <param name="action">The non-null authoritative operation.</param><param name="outcomeSelector">The non-null bounded result classifier.</param>
    /// <param name="address">Applicable owner correlation.</param><param name="scopeId">Applicable scope identity.</param>
    /// <param name="reservationId">Applicable reservation identity.</param><param name="operationId">Applicable domain operation identity.</param><param name="establishedScopeSelector">Optional selector for a scope established by success.</param><returns>The unchanged semantic result.</returns>
    /// <exception cref="ArgumentException"><paramref name="operation"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="outcomeSelector"/> is null.</exception>
    internal T Run<T>(string operation, Func<T> action, Func<T, string> outcomeSelector, BudgetScopeAddress? address = null, BudgetScopeId? scopeId = null, BudgetReservationId? reservationId = null, OperationId? operationId = null, Func<T, BudgetScopeId?>? establishedScopeSelector = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(outcomeSelector);
        long? started = null;
        AgentKitActivityScope? activity = null;
        Try(() => started = _timeProvider.GetTimestamp());
        Try(() => activity = AgentKitActivityScope.Start(AgentKitActivityNames.BudgetLedgerOperation, ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.BudgetOperation, operation }, { AgentKitTagNames.TenantId, address?.TenantId.ToString() },
                { AgentKitTagNames.PrincipalId, address?.PrincipalId.ToString() }, { AgentKitTagNames.AgentId, address?.AgentId.ToString() },
                { AgentKitTagNames.SessionId, address?.SessionId?.ToString() }, { AgentKitTagNames.RunId, address?.RunId?.ToString() },
                { AgentKitTagNames.OperationId, (operationId ?? address?.OperationId)?.ToString() },
                { AgentKitTagNames.BudgetScopeId, scopeId?.ToString() }, { AgentKitTagNames.BudgetReservationId, reservationId?.ToString() },
            }));
        try
        {
            var result = action();
            var outcome = outcomeSelector(result);
            var effectiveScopeId = scopeId;
            if (establishedScopeSelector is not null)
            {
                Try(() => effectiveScopeId = establishedScopeSelector(result) ?? scopeId);
            }
            Try(() => activity?.Activity?.SetTag(AgentKitTagNames.BudgetScopeId, effectiveScopeId?.ToString()));
            Try(() => activity?.Activity?.SetTag(AgentKitTagNames.Outcome, outcome));
            Try(() => activity?.Activity?.SetStatus(IsErrorOutcome(outcome) ? ActivityStatusCode.Error : ActivityStatusCode.Ok));
            Try(() => SqliteBudgetLedgerLog.Completed(_logger, operation, outcome, address?.TenantId.ToString(), address?.PrincipalId.ToString(), address?.AgentId.ToString(), address?.SessionId?.ToString(), address?.RunId?.ToString(), effectiveScopeId?.ToString(), reservationId?.ToString(), (operationId ?? address?.OperationId)?.ToString()));
            Record(operation, outcome, started);
            return result;
        }
        catch (Exception exception)
        {
            var outcome = exception is OperationCanceledException ? "cancelled" : "faulted";
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Try(() => activity?.Activity?.SetTag(AgentKitTagNames.Outcome, outcome));
            Try(() => activity?.Activity?.SetTag(AgentKitTagNames.ErrorType, errorType));
            Try(() => activity?.Activity?.SetStatus(ActivityStatusCode.Error, errorType));
            if (exception is OperationCanceledException)
            {
                Try(() => SqliteBudgetLedgerLog.Completed(_logger, operation, outcome, address?.TenantId.ToString(), address?.PrincipalId.ToString(), address?.AgentId.ToString(), address?.SessionId?.ToString(), address?.RunId?.ToString(), scopeId?.ToString(), reservationId?.ToString(), (operationId ?? address?.OperationId)?.ToString()));
            }
            else
            {
                Try(() => SqliteBudgetLedgerLog.Failed(_logger, operation, outcome, errorType, address?.TenantId.ToString(), address?.PrincipalId.ToString(), address?.AgentId.ToString(), address?.SessionId?.ToString(), address?.RunId?.ToString(), scopeId?.ToString(), reservationId?.ToString(), (operationId ?? address?.OperationId)?.ToString()));
            }
            Record(operation, outcome, started);
            throw;
        }
        finally
        {
            Try(() => activity?.Dispose());
        }
    }

    private void Record(string operation, string outcome, long? started)
    {
        Try(() => SqliteBudgetLedgerMetrics.RecordCount(operation, outcome));
        if (started is { } timestamp)
        {
            Try(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(timestamp);
                if (elapsed >= TimeSpan.Zero)
                {
                    SqliteBudgetLedgerMetrics.RecordDuration(operation, outcome, elapsed);
                }
            });
        }
    }

    private static bool IsErrorOutcome(string outcome) => outcome is "rejected" or "held" or "expired" or "cancelled" or "faulted";
    private static void Try(Action action) { try { action(); } catch (Exception) { } }
}
