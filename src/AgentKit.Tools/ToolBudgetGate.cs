// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Text;

/// <summary>Reserves the tool-runtime budget dimensions one batch consumes through its borrowed budget scope.</summary>
/// <remarks>
/// <para>
/// The gate covers four first-party dimensions. Concurrent calls are a gauge reserved and started immediately before an entry's
/// first attempt and committed when it settles, so the authority sees exactly the calls running now. Each retry attempt is counted
/// on the retries dimension before it is scheduled, so a refused count declines the retry instead of consuming capacity the run
/// does not have. Successful calls and result bytes are known only after the fact, so they are reserved and committed in one step
/// after a call succeeds and an overrun is recorded rather than erasing a delivered result. Attempted calls are counted by the
/// caller that admits the call (the loop counts one per call before it reaches the executor), so the gate never counts them twice.
/// </para>
/// <para>
/// Every reservation carries a stable idempotency key derived from the run, dimension, call, and attempt, so a retried or
/// replayed settlement never double-counts. A refusal or an authority that cannot answer is reported to the caller as a typed
/// outcome and observed content-free; it never throws except for cancellation.
/// </para>
/// </remarks>
internal sealed class ToolBudgetGate
{
    private static readonly BudgetUnit _count = new("count");
    private static readonly BudgetUnit _bytes = new("bytes");

    private readonly BudgetExecutionCapability _budget;
    private readonly RunId _runId;
    private readonly ILogger _logger;

    /// <summary>Initializes a gate over one borrowed budget capability.</summary>
    /// <param name="budget">The borrowed capability; its scope is never disposed or retained beyond the batch.</param>
    /// <param name="runId">The run whose identity keys every reservation.</param>
    /// <param name="logger">The structured logger.</param>
    /// <exception cref="ArgumentNullException">A reference is null.</exception>
    internal ToolBudgetGate(BudgetExecutionCapability budget, RunId runId, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(logger);
        _budget = budget;
        _runId = runId;
        _logger = logger;
    }

    /// <summary>Reserves and starts one concurrent-call slot for an entry.</summary>
    /// <param name="entry">The accepted entry about to be invoked.</param>
    /// <param name="expiresAt">The instant after which an unstarted reservation is released.</param>
    /// <param name="cancellationToken">Cancels the reservation; cancellation propagates.</param>
    /// <returns>The held slot, or <see langword="null"/> when the budget refused or could not answer, in which case the call must not start.</returns>
    internal async ValueTask<ConcurrentSlot?> TryEnterAsync(ToolBatchEntry entry, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var reservation = await ReserveAsync(
            entry, BudgetDimensions.ConcurrentToolCalls, 1m, _count, "concurrent", expiresAt, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return null;
        }

        try
        {
            _ = await reservation.MarkStartedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is BudgetLedgerStateException or BudgetLedgerPersistenceUnavailableException)
        {
            Declined(entry, BudgetDimensions.ConcurrentToolCalls, "unavailable");
            await reservation.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        return new ConcurrentSlot(reservation, this, entry);
    }

    /// <summary>Counts one retry attempt before it is scheduled.</summary>
    /// <param name="entry">The entry whose attempt failed.</param>
    /// <param name="failedAttempt">The positive attempt that just failed.</param>
    /// <param name="expiresAt">The instant after which an unstarted reservation is released.</param>
    /// <param name="cancellationToken">Cancels the reservation; cancellation propagates.</param>
    /// <returns><see langword="true"/> when the retry is within budget; otherwise the retry is declined.</returns>
    internal async ValueTask<bool> TryCountRetryAsync(ToolBatchEntry entry, int failedAttempt, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        await CommitAsync(
            entry, BudgetDimensions.ToolRetries, 1m, _count, $"retry:{failedAttempt}", expiresAt, cancellationToken).ConfigureAwait(false);

    /// <summary>Accounts the successful call and its retained result bytes after the call settled.</summary>
    /// <param name="entry">The settled entry.</param>
    /// <param name="result">The terminal result whose status and content are measured.</param>
    /// <param name="expiresAt">The instant after which an unstarted reservation is released.</param>
    /// <param name="cancellationToken">Cancels the accounting; cancellation propagates.</param>
    /// <returns>A task that completes once both dimensions were accounted or declined.</returns>
    internal async ValueTask AccountSettledAsync(ToolBatchEntry entry, ToolCallResult result, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status is not ToolTerminalStatus.Succeeded)
        {
            return;
        }

        _ = await CommitAsync(entry, BudgetDimensions.SuccessfulToolCalls, 1m, _count, "successful", expiresAt, cancellationToken).ConfigureAwait(false);
        var bytes = MeasureBytes(result.Content);
        if (bytes > 0)
        {
            _ = await CommitAsync(entry, BudgetDimensions.ToolResultBytes, bytes, _bytes, "result-bytes", expiresAt, cancellationToken).ConfigureAwait(false);
        }
    }

    private static decimal MeasureBytes(ImmutableArray<ToolResultContent> content)
    {
        decimal total = 0;
        if (content.IsDefaultOrEmpty)
        {
            return total;
        }

        foreach (var item in content)
        {
            total += item switch
            {
                ToolResultTextContent text => Encoding.UTF8.GetByteCount(text.Text),
                ToolResultOpaqueContent opaque => opaque.CanonicalPayload.CanonicalJson.Length,
                _ => 0,
            };
        }

        return total;
    }

    private async ValueTask<bool> CommitAsync(
        ToolBatchEntry entry,
        BudgetDimension dimension,
        decimal amount,
        BudgetUnit unit,
        string suffix,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var reservation = await ReserveAsync(entry, dimension, amount, unit, suffix, expiresAt, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return false;
        }

        try
        {
            _ = await reservation.MarkStartedAsync(cancellationToken).ConfigureAwait(false);
            _ = await reservation.CommitAsync(amount, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is BudgetLedgerStateException or BudgetLedgerPersistenceUnavailableException or BudgetLedgerMutationConflictException)
        {
            Declined(entry, dimension, "unavailable");
            return false;
        }
    }

    private async ValueTask<IBudgetReservation?> ReserveAsync(
        ToolBatchEntry entry,
        BudgetDimension dimension,
        decimal amount,
        BudgetUnit unit,
        string suffix,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var invocation = entry.Invocation;
        BudgetReservationResult result;
        try
        {
            result = await _budget.Scope.ReserveAsync(
                new BudgetReservationRequest(
                    _budget.Scope.Id,
                    dimension,
                    amount,
                    unit,
                    invocation.OperationId,
                    expiresAt,
                    new IdempotencyKey($"tool:{_runId}:{dimension.Value}:{invocation.CallId}:{suffix}")),
                cancellationToken).ConfigureAwait(false);
        }
        catch (BudgetLedgerPersistenceUnavailableException)
        {
            Declined(entry, dimension, "unavailable");
            return null;
        }

        switch (result)
        {
            case BudgetReserved reserved:
                Observe(dimension, "reserved");
                return reserved.Reservation;
            case BudgetRejected:
            case BudgetHeld:
                Declined(entry, dimension, "exhausted");
                return null;
            default:
                Declined(entry, dimension, "unavailable");
                return null;
        }
    }

    private void Declined(ToolBatchEntry entry, BudgetDimension dimension, string outcome)
    {
        Observe(dimension, outcome);
        try
        {
            ToolLog.BudgetReservationDeclined(_logger, dimension.Value, entry.Invocation.CallId, entry.Invocation.Tool.Id, outcome);
        }
        catch
        {
            // Instrumentation is observational only and cannot change the budget decision.
        }
    }

    private static void Observe(BudgetDimension dimension, string outcome)
    {
        try
        {
            ToolRecordingMetrics.BudgetReservationCount.Add(
                1,
                new TagList { { AgentKitTagNames.BudgetDimension, dimension.Value }, { AgentKitTagNames.Outcome, outcome } });
        }
        catch
        {
            // Instrumentation is observational only and cannot change the budget decision.
        }
    }

    /// <summary>Holds one started concurrent-call reservation until the call settles.</summary>
    internal sealed class ConcurrentSlot
    {
        private readonly IBudgetReservation _reservation;
        private readonly ToolBudgetGate _gate;
        private readonly ToolBatchEntry _entry;

        internal ConcurrentSlot(IBudgetReservation reservation, ToolBudgetGate gate, ToolBatchEntry entry)
        {
            _reservation = reservation;
            _gate = gate;
            _entry = entry;
        }

        /// <summary>Releases the slot once the call settled.</summary>
        /// <param name="abandoned">Whether an attempt ignored cancellation and may still be running; the started reservation is then retained so the gauge keeps counting it until reconciliation.</param>
        /// <returns>A task completed once the slot was committed or deliberately retained.</returns>
        internal async ValueTask ReleaseAsync(bool abandoned)
        {
            if (abandoned)
            {
                return;
            }

            try
            {
                _ = await _reservation.CommitAsync(1m, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is BudgetLedgerStateException or BudgetLedgerPersistenceUnavailableException or BudgetLedgerMutationConflictException)
            {
                _gate.Declined(_entry, BudgetDimensions.ConcurrentToolCalls, "unavailable");
            }
        }
    }
}
