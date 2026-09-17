// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Coordinates exclusive process-local ownership of durable operations for one process.</summary>
/// <remarks>
/// This manager is itself the single authoritative in-process lease store: it allocates every fencing token under its own
/// serialization gate, so ordering among every caller in this process is exact. It cannot coordinate across process boundaries,
/// because its state exists only in this process's memory. Composing more than one engine process, or more than one
/// instance of this manager, against durable operations that must exclude each other does not produce mutual
/// exclusion; that requires a distributed backend leaf instead.
/// </remarks>
public sealed class InMemoryDurableLeaseManager: IDurableLeaseManager
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DurableOperationAddress, LeaseRecord> _records = [];
    private readonly IIdentifierGenerator<ExecutionLeaseId> _leaseIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryDurableLeaseManager> _logger;
    private long _nextFencingToken;

    /// <summary>Initializes an empty manager with replaceable identity and clock collaborators.</summary>
    /// <param name="leaseIds">The non-null allocator for new lease identities.</param>
    /// <param name="timeProvider">The non-null injected clock used for expiry and elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public InMemoryDurableLeaseManager(
        IIdentifierGenerator<ExecutionLeaseId> leaseIds,
        TimeProvider timeProvider,
        ILogger<InMemoryDurableLeaseManager>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(leaseIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _leaseIds = leaseIds;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryDurableLeaseManager>.Instance;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The selected lease-identity generator produced a default identity.</exception>
    public ValueTask<ExecutionLeaseResult> AcquireAsync(
        ExecutionLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableLeaseAcquire,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, request.Address.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.Address.SessionId.ToString() },
                { AgentKitTagNames.RunId, request.Address.RunId.ToString() },
                { AgentKitTagNames.OperationId, request.Address.OperationId.ToString() },
                { AgentKitTagNames.WorkerId, request.WorkerId.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = _timeProvider.GetUtcNow();
            ExecutionLeaseResult result;
            DurableLeaseAcquisitionOutcome outcome;
            lock (_gate)
            {
                var hadPriorRecord = _records.TryGetValue(request.Address, out var existing);
                if (hadPriorRecord && existing!.ExpiresAt > now)
                {
                    result = new ExecutionLeaseHeldByAnotherWorker(existing.OwnerWorkerId, existing.FencingToken, existing.ExpiresAt);
                    outcome = DurableLeaseAcquisitionOutcome.HeldByAnotherWorker;
                }
                else
                {
                    var leaseId = _leaseIds.Create();
                    if (leaseId == default)
                    {
                        throw new InvalidOperationException(
                            "The selected lease-identity generator produced a default identity.");
                    }

                    // This manager is the single in-process lease store; allocating the next generation
                    // under its own gate is what makes the token an atomic store allocation rather than
                    // an independent per-caller counter.
                    _nextFencingToken++;
                    var token = new FencingToken(_nextFencingToken);
                    var expiresAt = now + request.Duration;
                    _records[request.Address] = new LeaseRecord(request.WorkerId, token, request.Duration, expiresAt);
                    var lease = new InMemoryExecutionLease(
                        this, leaseId, request.WorkerId, request.Address, token, request.Duration, expiresAt);
                    result = new ExecutionLeaseAcquired(lease);
                    outcome = hadPriorRecord
                        ? DurableLeaseAcquisitionOutcome.GrantedByTakeover
                        : DurableLeaseAcquisitionOutcome.GrantedFirstOwnership;
                }
            }

            FinishAcquisition(activity, outcome, started, null, request.WorkerId);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishAcquisition(activity, DurableLeaseAcquisitionOutcome.Cancelled, started, nameof(OperationCanceledException), request.WorkerId);
            throw;
        }
        catch (Exception exception)
        {
            FinishAcquisition(activity, DurableLeaseAcquisitionOutcome.Failed, started, ErrorType(exception), request.WorkerId);
            throw;
        }
    }

    /// <summary>Attempts to extend expiry for a lease this manager granted, without changing its generation.</summary>
    /// <param name="lease">The non-null lease requesting renewal.</param>
    /// <param name="cancellationToken">Cancels the renewal attempt; it does not release the lease.</param>
    /// <returns>The terminal renewal outcome.</returns>
    internal ValueTask<LeaseRenewalResult> RenewAsync(InMemoryExecutionLease lease, CancellationToken cancellationToken)
    {
        Debug.Assert(lease is not null, "Only this manager's own leases call back into it.");
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableLeaseRenew,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, lease.Address.AgentId.ToString() },
                { AgentKitTagNames.SessionId, lease.Address.SessionId.ToString() },
                { AgentKitTagNames.RunId, lease.Address.RunId.ToString() },
                { AgentKitTagNames.OperationId, lease.Address.OperationId.ToString() },
                { AgentKitTagNames.WorkerId, lease.OwnerWorkerId.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LeaseRenewalResult result;
            DurableLeaseRenewalOutcome outcome;
            lock (_gate)
            {
                var now = _timeProvider.GetUtcNow();
                if (_records.TryGetValue(lease.Address, out var current)
                    && current.FencingToken == lease.FencingToken
                    && current.ExpiresAt > now)
                {
                    var expiresAt = now + current.Duration;
                    current.ExpiresAt = expiresAt;
                    lease.ExpiresAt = expiresAt;
                    result = new LeaseRenewed(expiresAt);
                    outcome = DurableLeaseRenewalOutcome.Renewed;
                }
                else
                {
                    // Ownership is lost either because another worker's takeover installed a new record, or
                    // because this lease's own expiry passed before renewal: the lease service is authoritative
                    // for expiry, so a worker that missed its window cannot resurrect ownership by renewing late.
                    // The null case remains for the interface's general documented shape.
                    result = new LeaseLost(current?.FencingToken);
                    outcome = DurableLeaseRenewalOutcome.Lost;
                }
            }

            FinishRenewal(activity, outcome, started, null, lease.OwnerWorkerId);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishRenewal(activity, DurableLeaseRenewalOutcome.Cancelled, started, nameof(OperationCanceledException), lease.OwnerWorkerId);
            throw;
        }
        catch (Exception exception)
        {
            FinishRenewal(activity, DurableLeaseRenewalOutcome.Failed, started, ErrorType(exception), lease.OwnerWorkerId);
            throw;
        }
    }

    /// <summary>Releases ownership for a lease this manager granted, if it is still the current generation.</summary>
    /// <param name="lease">The non-null lease being disposed.</param>
    /// <remarks>Removing the record lets another worker acquire immediately instead of waiting for expiry. A lease that already lost ownership to takeover releases nothing, because a later generation already owns the record.</remarks>
    internal void Release(InMemoryExecutionLease lease)
    {
        Debug.Assert(lease is not null, "Only this manager's own leases call back into it.");
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableLeaseRelease,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, lease.Address.AgentId.ToString() },
                { AgentKitTagNames.SessionId, lease.Address.SessionId.ToString() },
                { AgentKitTagNames.RunId, lease.Address.RunId.ToString() },
                { AgentKitTagNames.OperationId, lease.Address.OperationId.ToString() },
                { AgentKitTagNames.WorkerId, lease.OwnerWorkerId.ToString() },
            });
        var activity = activityScope.Activity;
        lock (_gate)
        {
            if (_records.TryGetValue(lease.Address, out var current) && current.FencingToken == lease.FencingToken)
            {
                _ = _records.Remove(lease.Address);
            }
        }

        SafeSetActivity(activity, static current => current.SetSuccessful("released"));
        SafeLogReleased(lease.OwnerWorkerId);
        SafeRecordRelease();
    }

    private static string ErrorType(Exception exception)
    {
        Debug.Assert(exception is not null, "Only observed exceptions are classified.");
        return exception.GetType().FullName ?? exception.GetType().Name;
    }

    private void FinishAcquisition(Activity? activity, DurableLeaseAcquisitionOutcome outcome, long? started, string? errorType, WorkerId workerId)
    {
        var outcomeValue = outcome.ToStableValue();
        var granted = outcome is DurableLeaseAcquisitionOutcome.GrantedFirstOwnership or DurableLeaseAcquisitionOutcome.GrantedByTakeover;
        SafeSetActivity(activity, current =>
        {
            if (granted)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        try
        {
            if (errorType is not null && outcome == DurableLeaseAcquisitionOutcome.Failed)
            {
                DurableLeaseLog.AcquisitionFailed(_logger, workerId, errorType);
            }
            else if (outcome == DurableLeaseAcquisitionOutcome.Cancelled)
            {
                DurableLeaseLog.AcquisitionCancelled(_logger, workerId);
            }
            else
            {
                DurableLeaseLog.AcquisitionCompleted(_logger, workerId, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the committed acquisition outcome.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            DurableLeaseMetrics.RecordAcquisition(outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the committed acquisition outcome.
        }
    }

    private void FinishRenewal(Activity? activity, DurableLeaseRenewalOutcome outcome, long? started, string? errorType, WorkerId workerId)
    {
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(activity, current =>
        {
            if (outcome == DurableLeaseRenewalOutcome.Renewed)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        try
        {
            if (errorType is not null && outcome == DurableLeaseRenewalOutcome.Failed)
            {
                DurableLeaseLog.RenewalFailed(_logger, workerId, errorType);
            }
            else if (outcome == DurableLeaseRenewalOutcome.Cancelled)
            {
                DurableLeaseLog.RenewalCancelled(_logger, workerId);
            }
            else
            {
                DurableLeaseLog.RenewalCompleted(_logger, workerId, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the committed renewal outcome.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            DurableLeaseMetrics.RecordRenewal(outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the committed renewal outcome.
        }
    }

    private void SafeLogReleased(WorkerId workerId)
    {
        try
        {
            DurableLeaseLog.Released(_logger, workerId);
        }
        catch
        {
            // Logging is observational and cannot alter a completed release.
        }
    }

    private static void SafeRecordRelease()
    {
        try
        {
            DurableLeaseMetrics.RecordRelease();
        }
        catch
        {
            // Metrics are observational and cannot alter a completed release.
        }
    }

    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    private TimeSpan? TryGetElapsedTime(long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try
        {
            return _timeProvider.GetElapsedTime(timestamp);
        }
        catch
        {
            return null;
        }
    }

    private static void SafeSetActivity(Activity? activity, Action<Activity?> action)
    {
        Debug.Assert(action is not null, "Only package-owned activity updates are applied.");
        try
        {
            action(activity);
        }
        catch
        {
            // Activity listeners cannot alter a committed lease outcome.
        }
    }
}
