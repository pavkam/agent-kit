// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Provides an immutable owned handle over one persisted reservation receipt.</summary>
internal sealed class BudgetReservation: IBudgetReservation
{
    private readonly IBudgetLedger _ledger;
    private readonly BudgetLedgerReservationReceipt _receipt;
    private readonly ILogger<BudgetReservation> _logger;

    /// <summary>Initializes a reservation handle.</summary>
    /// <param name="ledger">The ledger owning reservation lifecycle and accounting.</param>
    /// <param name="receipt">The exact immutable admission receipt.</param>
    /// <param name="logger">The optional structured logger; null disables log publication.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public BudgetReservation(
        IBudgetLedger ledger, BudgetLedgerReservationReceipt receipt, ILogger<BudgetReservation>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(receipt);
        _ledger = ledger;
        _receipt = receipt;
        _logger = logger ?? NullLogger<BudgetReservation>.Instance;
    }

    /// <inheritdoc/>
    public BudgetReservationId Id => _receipt.Reservation.Id;

    /// <inheritdoc/>
    public BudgetScopeId ScopeId => _receipt.Reservation.Scope.Id;

    /// <inheritdoc/>
    public BudgetDimension Dimension => _receipt.OriginalRequest.Dimension;

    /// <inheritdoc/>
    public decimal Reserved => _receipt.OriginalRequest.Amount;

    /// <inheritdoc/>
    public async ValueTask<BudgetStartResult> MarkStartedAsync(CancellationToken cancellationToken = default)
    {
        using var observation = Observe(AgentKitActivityNames.BudgetStart);
        try
        {
            var result = await _ledger.MarkStartedAsync(_receipt.Reservation, cancellationToken).ConfigureAwait(false);
            var outcome = result is BudgetStarted ? "started" : result is BudgetStartExpired ? "expired" : "rejected";
            if (result is BudgetStarted)
            {
                observation.Success(outcome);
            }
            else
            {
                observation.Rejected(outcome);
            }

            TryMetric(() => BudgetMetrics.RecordStart(outcome));
            TryLog(() => BudgetLog.StartCompleted(_logger, ScopeId, Dimension, outcome));
            return result;
        }
        catch (Exception exception)
        {
            var outcome = Fail(observation, exception, cancellationToken);
            TryMetric(() => BudgetMetrics.RecordStart(outcome));
            if (outcome == "cancelled")
            {
                TryLog(() => BudgetLog.StartCompleted(_logger, ScopeId, Dimension, outcome));
            }
            else
            {
                TryLog(() => BudgetLog.StartFailed(
                    _logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name));
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actual);
        return CommitCoreAsync(new BudgetLedgerSettlementRequest(_receipt.Reservation, actual), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(
        decimal correctedActual,
        long revision,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(correctedActual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        return CorrectCoreAsync(
            new BudgetLedgerCorrectionRequest(_receipt.Reservation, correctedActual, revision), cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        using var observation = Observe(AgentKitActivityNames.BudgetRelease);
        try
        {
            var result = await _ledger.ReleaseUnstartedAsync(
                _receipt.Reservation, CancellationToken.None).ConfigureAwait(false);
            var outcome = result switch
            {
                BudgetLedgerReleased => "released",
                BudgetLedgerRetainedStarted => "retained_started",
                BudgetLedgerAlreadySettled => "already_settled",
                _ => throw new UnreachableException(),
            };
            observation.Success(outcome);
            TryMetric(() => BudgetMetrics.RecordSettlement(outcome));
            TryLog(() => BudgetLog.SettlementCompleted(_logger, ScopeId, Dimension, outcome));
        }
        catch (Exception exception)
        {
            var outcome = Fail(observation, exception, CancellationToken.None);
            TryMetric(() => BudgetMetrics.RecordSettlement("failed"));
            TryLog(() => BudgetLog.SettlementFailed(
                _logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name));
            throw;
        }
    }

    private async ValueTask<BudgetCommitResult> CommitCoreAsync(
        BudgetLedgerSettlementRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public method constructs a validated settlement request.");
        using var observation = Observe(AgentKitActivityNames.BudgetCommit);
        try
        {
            var result = await _ledger.SettleAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result.Overrun > 0 ? "committed_overrun" : "committed";
            observation.Success(outcome);
            TryMetric(() => BudgetMetrics.RecordSettlement(outcome));
            TryLog(() => BudgetLog.SettlementCompleted(_logger, ScopeId, Dimension, outcome));
            return result;
        }
        catch (Exception exception)
        {
            var outcome = Fail(observation, exception, cancellationToken);
            TryMetric(() => BudgetMetrics.RecordSettlement(outcome));
            if (outcome == "cancelled")
            {
                TryLog(() => BudgetLog.SettlementCompleted(_logger, ScopeId, Dimension, outcome));
            }
            else
            {
                TryLog(() => BudgetLog.SettlementFailed(
                    _logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name));
            }

            throw;
        }
    }

    private async ValueTask<BudgetCorrectionResult> CorrectCoreAsync(
        BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public method constructs a validated correction request.");
        using var observation = Observe(AgentKitActivityNames.BudgetCorrection);
        try
        {
            var result = await _ledger.CorrectAsync(request, cancellationToken).ConfigureAwait(false);
            observation.Success("corrected");
            TryMetric(() => BudgetMetrics.RecordCorrection("corrected"));
            TryLog(() => BudgetLog.CorrectionCompleted(_logger, ScopeId, Dimension, "corrected"));
            return result;
        }
        catch (Exception exception)
        {
            var outcome = Fail(observation, exception, cancellationToken);
            TryMetric(() => BudgetMetrics.RecordCorrection(outcome));
            if (outcome == "cancelled")
            {
                TryLog(() => BudgetLog.CorrectionCompleted(_logger, ScopeId, Dimension, outcome));
            }
            else
            {
                TryLog(() => BudgetLog.CorrectionFailed(
                    _logger, ScopeId, Dimension, exception.GetType().FullName ?? exception.GetType().Name));
            }

            throw;
        }
    }

    private BudgetRuntimeObservation Observe(string activityName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(activityName), "Callers provide a shared activity name.");
        var observation = BudgetRuntimeObservation.Start(activityName);
        observation.Tag(AgentKitTagNames.BudgetScopeId, ScopeId.ToString());
        observation.Tag(AgentKitTagNames.BudgetDimension, Dimension.ToString());
        observation.Tag(AgentKitTagNames.AgentId, _receipt.Reservation.Scope.Address.AgentId.ToString());
        observation.Tag(AgentKitTagNames.SessionId, _receipt.Reservation.Scope.Address.SessionId?.ToString());
        observation.Tag(AgentKitTagNames.RunId, _receipt.Reservation.Scope.Address.RunId?.ToString());
        observation.Tag(AgentKitTagNames.OperationId, _receipt.OriginalRequest.OperationId.ToString());
        return observation;
    }

    private static string Fail(BudgetRuntimeObservation observation, Exception exception, CancellationToken cancellationToken)
    {
        Debug.Assert(observation is not null, "The operation created an observation.");
        Debug.Assert(exception is not null, "The operation caught an exception.");
        var outcome = exception is OperationCanceledException && cancellationToken.IsCancellationRequested ? "cancelled" : "failed";
        observation.Failure(outcome, exception.GetType().FullName ?? exception.GetType().Name);
        return outcome;
    }

    private static void TryMetric(Action record)
    {
        Debug.Assert(record is not null, "Callers provide a metric action.");
        try { record(); } catch (Exception) { }
    }

    private static void TryLog(Action publish)
    {
        Debug.Assert(publish is not null, "Callers provide a log publication action.");
        try { publish(); } catch (Exception) { }
    }
}
