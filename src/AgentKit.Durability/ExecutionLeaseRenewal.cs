// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Keeps one execution lease alive for the duration of a single coordinator attempt.</summary>
/// <remarks>
/// <para>
/// Renewal runs on the injected <see cref="TimeProvider"/>, never on a wall-clock delay, so tests advance it
/// deterministically. The worker cannot extend ownership from its own clock: it only asks the lease manager, which
/// remains authoritative for expiry.
/// </para>
/// <para>
/// Renewal is observational with respect to the attempt's control flow. A failed or refused renewal is recorded and
/// surfaced through <see cref="Lost"/>; it does not interrupt in-flight work, because cancelling local awaiting cannot
/// undo an effect that may already be running. The durable write itself fails closed on a stale generation, which is
/// what actually prevents a lost owner from appending.
/// </para>
/// <para>
/// Instances are single-owner and must be disposed by the attempt that created them. Disposal stops renewal and never
/// releases or revokes the lease.
/// </para>
/// </remarks>
internal sealed class ExecutionLeaseRenewal: IDisposable
{
    private readonly IExecutionLease _lease;
    private readonly ILogger _logger;
    private readonly ITimer _timer;
    private int _lost;
    private bool _disposed;

    /// <summary>Starts periodic renewal for one held execution lease.</summary>
    /// <param name="lease">The non-null, borrowed lease to keep alive.</param>
    /// <param name="timeProvider">The non-null injected clock that schedules renewal.</param>
    /// <param name="interval">The positive renewal period, which must be shorter than the lease duration.</param>
    /// <param name="logger">The non-null logger for content-free renewal diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/>, <paramref name="timeProvider"/>, or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive.</exception>
    public ExecutionLeaseRenewal(IExecutionLease lease, TimeProvider timeProvider, TimeSpan interval, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero, nameof(interval));
        _lease = lease;
        _logger = logger;
        _timer = timeProvider.CreateTimer(static state => RenewAsync(state), this, interval, interval);
    }

    /// <summary>Gets whether a renewal attempt observed that ownership has passed to another worker.</summary>
    /// <value>
    /// <see langword="true"/> once the lease manager reported the lease lost or a renewal attempt failed. The value is
    /// advisory: the authoritative refusal is the journal rejecting a stale generation.
    /// </value>
    public bool Lost => Volatile.Read(ref _lost) != 0;

    /// <summary>Stops renewal without releasing or revoking the lease.</summary>
    /// <remarks>Repeated disposal is harmless. Disposal never throws a renewal failure.</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
    }

    private static async void RenewAsync(object? state)
    {
        if (state is not ExecutionLeaseRenewal renewal)
        {
            return;
        }

        try
        {
            var result = await renewal._lease.RenewAsync(CancellationToken.None).ConfigureAwait(false);
            if (result is LeaseRenewed)
            {
                renewal.Observe(DurableCoordinatorOutcome.Completed, errorType: null);
                return;
            }

            _ = Interlocked.Exchange(ref renewal._lost, 1);
            renewal.Observe(DurableCoordinatorOutcome.LeaseLost, errorType: null);
        }
        catch (Exception exception)
        {
            // Renewal is a background timer callback. An escaping exception would terminate the process, and a
            // renewal failure must never change the semantic outcome of the attempt that is still running.
            _ = Interlocked.Exchange(ref renewal._lost, 1);
            renewal.Observe(DurableCoordinatorOutcome.Failed, exception.GetType().Name);
        }
    }

    private void Observe(DurableCoordinatorOutcome outcome, string? errorType)
    {
        const DurableCoordinatorStage stage = DurableCoordinatorStage.RenewLease;
        try
        {
            var stageValue = stage.ToStableValue();
            switch (outcome)
            {
                case DurableCoordinatorOutcome.Completed:
                    DurabilityLog.LeaseRenewed(_logger, stageValue);
                    break;
                case DurableCoordinatorOutcome.LeaseLost:
                    DurabilityLog.LeaseLost(_logger, stageValue);
                    break;
                case DurableCoordinatorOutcome.Started:
                case DurableCoordinatorOutcome.Reconciled:
                case DurableCoordinatorOutcome.Committed:
                case DurableCoordinatorOutcome.Cancelled:
                case DurableCoordinatorOutcome.Failed:
                case DurableCoordinatorOutcome.Unavailable:
                case DurableCoordinatorOutcome.OperatorRequired:
                case DurableCoordinatorOutcome.Incompatible:
                case DurableCoordinatorOutcome.NotPossible:
                case DurableCoordinatorOutcome.Denied:
                default:
                    DurabilityLog.StageFailed(_logger, stageValue, errorType ?? nameof(Exception));
                    break;
            }

            DurabilityMetrics.RecordStage(stage, outcome);
        }
        catch
        {
            // Instrumentation is observational. A failing provider never changes whether ownership was renewed.
        }
    }
}
