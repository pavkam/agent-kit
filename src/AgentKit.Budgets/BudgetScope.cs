// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Provides an immutable runtime handle over one exact persisted budget scope.</summary>
internal sealed class BudgetScope: IBudgetScope
{
    private readonly IBudgetLedger _ledger;
    private readonly BudgetLedgerScopeReference _reference;
    private readonly ILogger<BudgetScope> _logger;
    private readonly ILogger<BudgetReservation> _reservationLogger;

    /// <summary>Initializes a scope handle.</summary>
    /// <param name="ledger">The ledger owning all authoritative state.</param>
    /// <param name="reference">The exact persisted scope locator.</param>
    /// <param name="logger">The optional structured logger; null disables log publication.</param>
    /// <param name="reservationLogger">The optional reservation-category logger; null disables its log publication.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public BudgetScope(
        IBudgetLedger ledger,
        BudgetLedgerScopeReference reference,
        ILogger<BudgetScope>? logger = null,
        ILogger<BudgetReservation>? reservationLogger = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(reference);
        _ledger = ledger;
        _reference = reference;
        _logger = logger ?? NullLogger<BudgetScope>.Instance;
        _reservationLogger = reservationLogger ?? NullLogger<BudgetReservation>.Instance;
    }

    /// <inheritdoc/>
    public BudgetScopeId Id => _reference.Id;

    /// <inheritdoc/>
    public BudgetScopeAddress Address => _reference.Address;

    /// <inheritdoc/>
    public async ValueTask<BudgetReservationResult> ReserveAsync(
        BudgetReservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await ReserveBatchAsync([request], cancellationToken).ConfigureAwait(false);
        return result switch
        {
            BudgetBatchReserved reserved => new BudgetReserved(reserved.Reservations[0]),
            BudgetBatchRejected rejected => new BudgetRejected(rejected.Failure),
            BudgetBatchHeld held => new BudgetHeld(held.Holds),
            _ => throw new UnreachableException(),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
        ImmutableArray<BudgetReservationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var request = new BudgetLedgerBatchReserveRequest(_reference, requests);
        using var observation = BudgetRuntimeObservation.Start(AgentKitActivityNames.BudgetReserve);
        observation.Tag(AgentKitTagNames.BudgetScopeId, Id.ToString());
        observation.Tag(AgentKitTagNames.BudgetDimension, requests[0].Dimension.ToString());
        observation.Tag(AgentKitTagNames.AgentId, Address.AgentId.ToString());
        observation.Tag(AgentKitTagNames.SessionId, Address.SessionId?.ToString());
        observation.Tag(AgentKitTagNames.RunId, Address.RunId?.ToString());
        observation.Tag(AgentKitTagNames.OperationId, requests[0].OperationId.ToString());
        try
        {
            var ledgerResult = await _ledger.ReserveBatchAsync(request, cancellationToken).ConfigureAwait(false);
            BudgetBatchReservationResult mapped = ledgerResult switch
            {
                BudgetLedgerBatchReserved reserved => new BudgetBatchReserved(
                    [.. reserved.Receipts.Select(receipt => (IBudgetReservation) new BudgetReservation(_ledger, receipt, _reservationLogger))]),
                BudgetLedgerBatchReserveRejected rejected => new BudgetBatchRejected(rejected.Failure),
                BudgetLedgerBatchReserveHeld held => new BudgetBatchHeld(held.Holds),
                _ => throw new UnreachableException(),
            };
            var outcome = mapped switch
            {
                BudgetBatchReserved => "reserved",
                BudgetBatchRejected => "rejected",
                BudgetBatchHeld => "held",
                _ => throw new UnreachableException(),
            };
            if (mapped is BudgetBatchReserved)
            {
                observation.Success(outcome);
            }
            else
            {
                observation.Rejected(outcome);
            }

            TryMetric(outcome);
            TryLog(() => BudgetLog.ReservationCompleted(_logger, Id, requests[0].Dimension, outcome));
            return mapped;
        }
        catch (Exception exception)
        {
            var outcome = exception is OperationCanceledException && cancellationToken.IsCancellationRequested ? "cancelled" : "failed";
            observation.Failure(outcome, exception.GetType().FullName ?? exception.GetType().Name);
            TryMetric(outcome);
            if (outcome == "cancelled")
            {
                TryLog(() => BudgetLog.ReservationCancelled(_logger, Id, requests[0].Dimension));
            }
            else
            {
                TryLog(() => BudgetLog.ReservationFailed(
                    _logger, Id, requests[0].Dimension, exception.GetType().FullName ?? exception.GetType().Name));
            }
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var observation = BudgetRuntimeObservation.Start(AgentKitActivityNames.BudgetSnapshot);
        observation.Tag(AgentKitTagNames.BudgetScopeId, Id.ToString());
        observation.Tag(AgentKitTagNames.AgentId, Address.AgentId.ToString());
        observation.Tag(AgentKitTagNames.SessionId, Address.SessionId?.ToString());
        observation.Tag(AgentKitTagNames.RunId, Address.RunId?.ToString());
        observation.Tag(AgentKitTagNames.OperationId, Address.OperationId?.ToString());
        try
        {
            var snapshot = await _ledger.GetSnapshotAsync(_reference, cancellationToken).ConfigureAwait(false);
            observation.Success("read");
            TrySnapshotMetric("read");
            TryLog(() => BudgetLog.SnapshotCompleted(_logger, Id, "read"));
            return snapshot;
        }
        catch (Exception exception)
        {
            var outcome = exception is OperationCanceledException && cancellationToken.IsCancellationRequested
                ? "cancelled"
                : "failed";
            observation.Failure(outcome, exception.GetType().FullName ?? exception.GetType().Name);
            TrySnapshotMetric(outcome);
            if (outcome == "cancelled")
            {
                TryLog(() => BudgetLog.SnapshotCancelled(_logger, Id));
            }
            else
            {
                TryLog(() => BudgetLog.SnapshotFailed(
                    _logger, Id, exception.GetType().FullName ?? exception.GetType().Name));
            }
            throw;
        }
    }

    private static void TryMetric(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "Callers provide a bounded outcome.");
        try { BudgetMetrics.RecordReservation(outcome); } catch (Exception) { }
    }

    private static void TrySnapshotMetric(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "Callers provide a bounded outcome.");
        try { BudgetMetrics.RecordSnapshot(outcome); } catch (Exception) { }
    }

    private static void TryLog(Action publish)
    {
        Debug.Assert(publish is not null, "Callers provide a log publication action.");
        try { publish(); } catch (Exception) { }
    }
}
