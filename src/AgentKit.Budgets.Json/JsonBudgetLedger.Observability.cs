// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <content>Contains the content-free tracing, metric, and logging wrappers shared by every ledger operation.</content>
public sealed partial class JsonBudgetLedger
{
    private TResult Run<TResult>(
        string operation,
        Func<CancellationToken, TResult> action,
        Func<TResult, string> outcomeSelector,
        ActivityTagsCollection tags,
        CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A synchronous ledger transition is required.");
        Debug.Assert(outcomeSelector is not null, "A bounded result classifier is required.");
        Debug.Assert(tags is not null, "Applicable correlation tags are required.");
        tags[AgentKitTagNames.BudgetOperation] = operation;
        var correlation = LedgerCorrelation.From(tags);
        AgentKitActivityScope? activityScope = null;
        TryObserve(() => activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.BudgetLedgerOperation, ActivityKind.Internal, tags));
        var measured = TryGetTimestamp(out var startedAt);
        try
        {
            var result = action(cancellationToken);
            var outcome = outcomeSelector(result);
            if (result is BudgetLedgerScopeCreated created)
            {
                correlation = correlation with { ScopeId = created.Scope.Id.ToString() };
                var scopeText = correlation.ScopeId;
                TryObserve(() => activityScope?.Activity?.SetTag(AgentKitTagNames.BudgetScopeId, scopeText));
            }

            ObserveTerminal(
                activityScope?.Activity, operation, outcome, !IsStartForbidden(outcome), null, correlation, measured, startedAt);
            return result;
        }
        catch (OperationCanceledException)
        {
            ObserveTerminal(
                activityScope?.Activity, operation, "cancelled", false, null, correlation, measured, startedAt);
            throw;
        }
        catch (Exception exception)
        {
            ObserveTerminal(
                activityScope?.Activity, operation, "faulted", false, exception, correlation, measured, startedAt);
            throw;
        }
        finally
        {
            TryObserve(() => activityScope?.Dispose());
        }
    }

    private void ObserveTerminal(
        Activity? activity,
        string operation,
        string outcome,
        bool successful,
        Exception? exception,
        LedgerCorrelation correlation,
        bool measured,
        long startedAt)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        // Activity.SetSuccessful/SetFailed never throws for the always-bounded, nonblank outcome and failure-code values
        // produced here, so the catch has no reachable trigger; it guards only against a future regression.
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
            JsonBudgetLedgerMetrics.Operations.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.BudgetOperation, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
            if (measured)
            {
                var elapsed = _timeProvider.GetElapsedTime(startedAt);
                if (elapsed >= TimeSpan.Zero)
                {
                    JsonBudgetLedgerMetrics.Duration.Record(
                        elapsed.TotalSeconds,
                        new KeyValuePair<string, object?>(AgentKitTagNames.BudgetOperation, operation),
                        new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
                }
            }
        }
        catch
        {
            // Meter listeners are observational and never alter the semantic outcome.
        }

        try
        {
            if (successful)
            {
                JsonBudgetLedgerLog.OperationCompleted(
                    _logger, operation, outcome, correlation.TenantId, correlation.PrincipalId, correlation.AgentId,
                    correlation.SessionId, correlation.RunId, correlation.ScopeId, correlation.ReservationId,
                    correlation.OperationId);
            }
            else
            {
                JsonBudgetLedgerLog.OperationFailed(
                    _logger, operation, outcome, GetBoundedFailureCode(outcome, exception), correlation.TenantId,
                    correlation.PrincipalId, correlation.AgentId, correlation.SessionId, correlation.RunId,
                    correlation.ScopeId, correlation.ReservationId, correlation.OperationId);
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
            // A replaced clock may refuse to supply a timestamp; measurement is observational and never blocks the ledger.
            timestamp = 0;
            return false;
        }
    }

    private static ActivityTagsCollection DiagnosticTags(
        BudgetScopeAddress address,
        BudgetScopeId? scopeId = null,
        BudgetReservationId? reservationId = null,
        OperationId? operationId = null)
    {
        Debug.Assert(address is not null, "The public wrapper supplies its validated scope address.");
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.TenantId, address.TenantId.ToString() },
            { AgentKitTagNames.PrincipalId, address.PrincipalId.ToString() },
            { AgentKitTagNames.AgentId, address.AgentId.ToString() },
        };
        if (address.SessionId is { } session)
        {
            tags.Add(AgentKitTagNames.SessionId, session.ToString());
        }
        if (address.RunId is { } run)
        {
            tags.Add(AgentKitTagNames.RunId, run.ToString());
        }
        if (scopeId is { } scope)
        {
            tags.Add(AgentKitTagNames.BudgetScopeId, scope.ToString());
        }
        if (reservationId is { } reservation)
        {
            tags.Add(AgentKitTagNames.BudgetReservationId, reservation.ToString());
        }
        if ((operationId ?? address.OperationId) is { } operation)
        {
            tags.Add(AgentKitTagNames.OperationId, operation.ToString());
        }

        return tags;
    }

    private static string ResultOutcome(object result)
    {
        Debug.Assert(result is not null, "A successful ledger operation supplies a non-null result.");
        return result switch
        {
            BudgetLedgerScopeCreated => "created",
            BudgetLedgerScopeCreateRejected => "rejected",
            BudgetLedgerBatchReserved => "reserved",
            BudgetLedgerBatchReserveRejected => "rejected",
            BudgetLedgerBatchReserveHeld => "held",
            BudgetStarted started when started.WasAlreadyStarted => "already_started",
            BudgetStarted => "started",
            BudgetStartRejected => "rejected",
            BudgetStartExpired => "expired",
            BudgetLedgerReleased => "released",
            BudgetLedgerRetainedStarted => "retained_started",
            BudgetLedgerAlreadySettled => "already_settled",
            BudgetLedgerReconciliationReleased => "released",
            BudgetLedgerReconciliationRetainedUnknown => "retained_unknown",
            BudgetLedgerReconciliationSettled => "settled",
            BudgetOverrunHoldResolved => "resolved",
            BudgetOverrunHoldResolutionBlocked => "held",
            _ => "completed",
        };
    }

    private static bool IsStartForbidden(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "The result classifier supplies a bounded outcome.");
        return outcome is "rejected" or "expired" or "held";
    }

    private static string GetBoundedFailureCode(string outcome, Exception? exception)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        return exception switch
        {
            BudgetLedgerPersistenceUnavailableException unavailable =>
                unavailable.AcknowledgementUnknown ? "persistence_unconfirmed" : "persistence_unavailable",
            BudgetLedgerReferenceUnavailableException => "reference_unavailable",
            BudgetLedgerMutationConflictException => "mutation_conflict",
            BudgetLedgerStateException => "invalid_state",
            null => outcome,
            _ => "faulted",
        };
    }

    private static void TryObserve(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try
        {
            observation();
        }
        catch
        {
            // Diagnostics construction is observational and never alters the semantic outcome.
        }
    }

    /// <summary>Carries the bounded correlation identities one ledger operation may attach to logs and traces.</summary>
    /// <remarks>
    /// The values are extracted once from the operation's activity tags so a failing or disabled listener cannot make the
    /// log and the span disagree. Every member is an identity rather than accounting content, so none of them discloses a
    /// reserved amount, a limit, a replay key, or the configured store root.
    /// </remarks>
    /// <param name="TenantId">The applicable tenant identity text, when established.</param>
    /// <param name="PrincipalId">The applicable principal identity text, when established.</param>
    /// <param name="AgentId">The applicable agent identity text, when established.</param>
    /// <param name="SessionId">The applicable session identity text, when the address carries one.</param>
    /// <param name="RunId">The applicable run identity text, when the address carries one.</param>
    /// <param name="ScopeId">The applicable scope identity text, when established before or by the operation.</param>
    /// <param name="ReservationId">The applicable reservation identity text, when established.</param>
    /// <param name="OperationId">The applicable logical operation identity text, when established.</param>
    private readonly record struct LedgerCorrelation(
        string? TenantId,
        string? PrincipalId,
        string? AgentId,
        string? SessionId,
        string? RunId,
        string? ScopeId,
        string? ReservationId,
        string? OperationId)
    {
        /// <summary>Extracts the bounded correlation identities from one operation's activity tags.</summary>
        /// <param name="tags">The non-null tag collection built by the calling public wrapper.</param>
        /// <returns>The identities present on the collection, with every absent identity left null.</returns>
        internal static LedgerCorrelation From(ActivityTagsCollection tags)
        {
            Debug.Assert(tags is not null, "The wrapper creates a diagnostic tag collection before extraction.");
            return new LedgerCorrelation(
                Tag(tags, AgentKitTagNames.TenantId),
                Tag(tags, AgentKitTagNames.PrincipalId),
                Tag(tags, AgentKitTagNames.AgentId),
                Tag(tags, AgentKitTagNames.SessionId),
                Tag(tags, AgentKitTagNames.RunId),
                Tag(tags, AgentKitTagNames.BudgetScopeId),
                Tag(tags, AgentKitTagNames.BudgetReservationId),
                Tag(tags, AgentKitTagNames.OperationId));
        }

        private static string? Tag(ActivityTagsCollection tags, string name)
        {
            Debug.Assert(tags is not null, "A diagnostic tag collection is required.");
            Debug.Assert(!string.IsNullOrWhiteSpace(name), "A shared nonblank tag name is required.");
            return tags.TryGetValue(name, out var value) ? value?.ToString() : null;
        }
    }
}
