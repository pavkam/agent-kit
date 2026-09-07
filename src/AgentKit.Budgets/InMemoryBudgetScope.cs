// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>The in-memory hierarchical budget scope created by <see cref="InMemoryBudgetAuthority"/>.</summary>
/// <remarks>All related scopes share one mutation gate, making hierarchy and batch admission atomic.</remarks>
internal sealed class InMemoryBudgetScope: IBudgetScope, IRunBudget
{
    private readonly Lock _hierarchyGate;
    private readonly Dictionary<BudgetDimension, InMemoryBudgetDimensionState> _states = [];
    private readonly Dictionary<IdempotencyKey, InMemoryBudgetBatchRecord> _batchesByItemKey = [];
    private readonly IIdentifierGenerator<BudgetReservationId> _reservationIds;
    private readonly TimeProvider _timeProvider;
    private readonly AgentBudgetOptionsSnapshot _options;
    private readonly ILogger<InMemoryBudgetScope> _logger;

    /// <summary>Initializes a scope participating in its authority's atomic hierarchy.</summary>
    /// <param name="id">The scope identity.</param><param name="address">The hierarchical address.</param>
    /// <param name="parent">The parent scope, when present.</param><param name="limits">The local limits.</param>
    /// <param name="hierarchyGate">The authority-owned hierarchy gate.</param>
    /// <param name="reservationIds">The reservation identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param><param name="options">The validated options.</param>
    /// <param name="logger">The optional structured logger.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public InMemoryBudgetScope(
        BudgetScopeId id, BudgetScopeAddress address, InMemoryBudgetScope? parent,
        ImmutableArray<BudgetLimit> limits, Lock hierarchyGate,
        IIdentifierGenerator<BudgetReservationId> reservationIds, TimeProvider timeProvider,
        AgentBudgetOptionsSnapshot options, ILogger<InMemoryBudgetScope>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNullLock(hierarchyGate);
        ArgumentNullException.ThrowIfNull(reservationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        Id = id;
        Address = address;
        Parent = parent;
        Depth = (parent?.Depth ?? -1) + 1;
        Limits = limits.ToImmutableDictionary(static limit => limit.Dimension);
        _hierarchyGate = hierarchyGate;
        _reservationIds = reservationIds;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger ?? NullLogger<InMemoryBudgetScope>.Instance;
        foreach (var limit in limits)
        {
            _states[limit.Dimension] = new InMemoryBudgetDimensionState(limit.Unit);
        }
    }

    /// <inheritdoc/>
    public BudgetScopeId Id { get; }
    /// <inheritdoc/>
    public BudgetScopeAddress Address { get; }
    /// <summary>Gets the parent scope, when present.</summary>
    public InMemoryBudgetScope? Parent { get; }
    /// <summary>Gets the zero-based hierarchy depth.</summary>
    public int Depth { get; }
    /// <summary>Gets the local limits by dimension.</summary>
    public ImmutableDictionary<BudgetDimension, BudgetLimit> Limits { get; }

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
            _ => throw new UnreachableException(),
        };
    }

    /// <inheritdoc/>
    public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
        ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, Id);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.BudgetReserve);
        _ = activity?.SetTag(AgentKitTagNames.BudgetScopeId, Id.ToString());
        _ = activity?.SetTag(AgentKitTagNames.BudgetDimension, requests[0].Dimension.ToString());
        _ = activity?.SetTag(AgentKitTagNames.OperationId, requests[0].OperationId.ToString());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            BudgetBatchReservationResult result;
            lock (_hierarchyGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateUnitsLocked(requests);
                SweepExpiredLocked();
                if (TryGetReplayLocked(requests, out var replay))
                {
                    result = new BudgetBatchReserved(replay);
                }
                else
                {
                    var scopes = GetHierarchy();
                    var failure = ValidateAdmissionLocked(scopes, requests);
                    result = failure is null
                        ? new BudgetBatchReserved(CreateReservationsLocked(scopes, requests))
                        : new BudgetBatchRejected(failure);
                }
            }

            var outcome = result is BudgetBatchReserved ? "reserved" : "rejected";
            if (result is BudgetBatchReserved)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, nameof(BudgetLimitFailure));
            }

            foreach (var dimension in requests.Select(static item => item.Dimension).Distinct())
            {
                BudgetLog.ReservationCompleted(_logger, Id, dimension, outcome);
                BudgetMetrics.RecordReservation(outcome, dimension);
            }

            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_hierarchyGate)
        {
            SweepExpiredLocked();
            var usages = _states.Select(pair =>
            {
                _ = Limits.TryGetValue(pair.Key, out var limit);
                return new BudgetDimensionUsage(pair.Key, pair.Value.Unit, pair.Value.Reserved, pair.Value.Committed, limit);
            }).ToImmutableArray();
            return ValueTask.FromResult(new BudgetSnapshot(Id, _timeProvider.GetUtcNow(), usages));
        }
    }

    /// <summary>Marks the reservation hierarchy started atomically.</summary>
    /// <param name="reservation">The caller-visible reservation.</param><returns>The start result.</returns>
    /// <param name="cancellationToken">Cancels the transition before any accounting changes.</param>
    internal BudgetStartResult MarkStarted(
        InMemoryBudgetReservation reservation,
        CancellationToken cancellationToken)
    {
        lock (_hierarchyGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SweepExpiredLocked();
            return reservation.MarkHierarchyStartedLocked();
        }
    }

    /// <summary>Commits the reservation hierarchy atomically.</summary>
    /// <param name="reservation">The caller-visible reservation.</param><param name="actual">The actual usage.</param>
    /// <param name="cancellationToken">Cancels settlement before any accounting changes.</param>
    /// <returns>The caller-visible settlement.</returns>
    internal BudgetCommitResult Commit(
        InMemoryBudgetReservation reservation,
        decimal actual,
        CancellationToken cancellationToken)
    {
        lock (_hierarchyGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return reservation.CommitHierarchyLocked(actual);
        }
    }

    /// <summary>Corrects the reservation hierarchy atomically.</summary>
    /// <param name="reservation">The original reservation.</param><param name="correctedActual">The replacement actual.</param>
    /// <param name="revision">The monotonic revision.</param>
    /// <param name="cancellationToken">Cancels correction before any accounting changes.</param>
    /// <returns>The correction result.</returns>
    internal BudgetCorrectionResult Correct(
        InMemoryBudgetReservation reservation,
        decimal correctedActual,
        long revision,
        CancellationToken cancellationToken)
    {
        lock (_hierarchyGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return reservation.CorrectHierarchyLocked(correctedActual, revision);
        }
    }

    /// <summary>Releases the reservation hierarchy only if it is unstarted.</summary>
    /// <param name="reservation">The caller-visible reservation.</param>
    internal void Release(InMemoryBudgetReservation reservation)
    {
        lock (_hierarchyGate)
        {
            reservation.ReleaseHierarchyLocked();
        }
    }

    /// <summary>Applies one local commit while the hierarchy gate is held.</summary>
    /// <param name="reservation">The local reservation.</param><param name="actual">The actual usage.</param>
    /// <returns>The local settlement.</returns>
    internal BudgetCommitResult CommitLocalLocked(InMemoryBudgetReservation reservation, decimal actual)
    {
        var state = _states[reservation.Dimension];
        state.Reserved -= reservation.Reserved;
        state.Committed += actual;
        _ = state.Open.Remove(reservation);
        state.CommittedReservations.Add(reservation);
        RecomputeBlocked(reservation.Dimension, state);
        return new BudgetCommitResult(reservation.Id, reservation.Reserved, actual,
            Math.Max(0m, reservation.Reserved - actual), Math.Max(0m, actual - reservation.Reserved));
    }

    /// <summary>Applies one local correction while the hierarchy gate is held.</summary>
    /// <param name="reservation">The local reservation.</param><param name="correctedActual">The replacement actual.</param>
    internal void CorrectLocalLocked(InMemoryBudgetReservation reservation, decimal correctedActual)
    {
        var state = _states[reservation.Dimension];
        state.Committed += correctedActual - reservation.Actual;
        RecomputeBlocked(reservation.Dimension, state, reservation, correctedActual);
    }

    /// <summary>Releases one local unstarted reservation while the hierarchy gate is held.</summary>
    /// <param name="reservation">The local reservation.</param>
    internal void ReleaseLocalLocked(InMemoryBudgetReservation reservation)
    {
        var state = _states[reservation.Dimension];
        state.Reserved -= reservation.Reserved;
        _ = state.Open.Remove(reservation);
    }

    private bool TryGetReplayLocked(ImmutableArray<BudgetReservationRequest> requests, out ImmutableArray<IBudgetReservation> reservations)
    {
        InMemoryBudgetBatchRecord? found = null;
        var missing = false;
        foreach (var request in requests)
        {
            if (!_batchesByItemKey.TryGetValue(request.IdempotencyKey, out var batch))
            {
                missing = true;
                continue;
            }
            if (found is not null && !ReferenceEquals(found, batch))
            {
                throw new InvalidOperationException("The item keys belong to different prior batches.");
            }

            found = batch;
        }
        if (found is null)
        {
            reservations = default;
            return false;
        }
        if (missing || found.TargetScopeId != Id || !found.Requests.SequenceEqual(requests))
        {
            throw new InvalidOperationException("An idempotency key was reused with different batch content or membership.");
        }

        reservations = [.. found.TargetReservations.Cast<IBudgetReservation>()];
        return true;
    }

    private ImmutableArray<InMemoryBudgetScope> GetHierarchy()
    {
        var builder = ImmutableArray.CreateBuilder<InMemoryBudgetScope>();
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            builder.Add(scope);
        }

        return builder.ToImmutable();
    }

    private static BudgetLimitFailure? ValidateAdmissionLocked(
        ImmutableArray<InMemoryBudgetScope> scopes, ImmutableArray<BudgetReservationRequest> requests)
    {
        foreach (var scope in scopes)
        {
            foreach (var request in requests)
            {
                if (scope._batchesByItemKey.ContainsKey(request.IdempotencyKey))
                {
                    throw new InvalidOperationException("An idempotency key is already bound to another reservation or batch.");
                }

            }
            var openCount = scope._states.Values.Sum(static state => state.Open.Count);
            if (openCount + requests.Length > scope._options.MaximumOpenReservationsPerScope)
            {
                var first = requests[0];
                return new BudgetLimitFailure(scope.Id, first.Dimension, BudgetLimitKind.Hard,
                    scope._options.MaximumOpenReservationsPerScope, openCount, requests.Length,
                    new BudgetUnit("reservations"), "The batch would exceed the maximum number of open reservations.");
            }
            foreach (var group in requests.GroupBy(static item => (item.Dimension, item.Unit)))
            {
                var first = group.First();
                var amount = group.Sum(static item => item.Amount);
                _ = scope._states.TryGetValue(first.Dimension, out var state);
                var observed = (state?.Reserved ?? 0m) + (state?.Committed ?? 0m);
                _ = scope.Limits.TryGetValue(first.Dimension, out var limit);
                if (state?.Blocked == true)
                {
                    return new BudgetLimitFailure(scope.Id, first.Dimension, BudgetLimitKind.Hard, limit?.Value ?? 0m,
                        observed, amount, first.Unit, $"Dimension '{first.Dimension}' is blocked after a recorded overrun.");
                }

                if (limit is { Kind: BudgetLimitKind.Hard } && observed + amount > limit.Value)
                {
                    return new BudgetLimitFailure(scope.Id, first.Dimension, BudgetLimitKind.Hard, limit.Value,
                        observed, amount, first.Unit, $"The batch would exceed the hard limit of {limit.Value} for '{first.Dimension}'.");
                }
            }
        }
        return null;
    }

    private void ValidateUnitsLocked(ImmutableArray<BudgetReservationRequest> requests)
    {
        foreach (var scope in GetHierarchy())
        {
            foreach (var request in requests)
            {
                if (scope.Limits.TryGetValue(request.Dimension, out var limit))
                {
                    ArgumentOutOfRangeException.ThrowIfNotEqual(request.Unit, limit.Unit, nameof(requests));
                }

                if (scope._states.TryGetValue(request.Dimension, out var state))
                {
                    ArgumentOutOfRangeException.ThrowIfNotEqual(request.Unit, state.Unit, nameof(requests));
                }
            }
        }
    }

    private ImmutableArray<IBudgetReservation> CreateReservationsLocked(
        ImmutableArray<InMemoryBudgetScope> scopes, ImmutableArray<BudgetReservationRequest> requests)
    {
        var batch = new InMemoryBudgetBatchRecord(Id, requests);
        var plans = new List<(InMemoryBudgetReservation Reservation, BudgetUnit Unit)>(scopes.Length * requests.Length);
        var targets = ImmutableArray.CreateBuilder<InMemoryBudgetReservation>(requests.Length);
        foreach (var request in requests)
        {
            InMemoryBudgetReservation? target = null;
            InMemoryBudgetReservation? child = null;
            foreach (var scope in scopes)
            {
                var expiresAt = request.ExpiresAt
                    ?? (scope._timeProvider.GetUtcNow() + scope._options.DefaultReservationLifetime);
                var reservation = new InMemoryBudgetReservation(scope._reservationIds.Create(), scope,
                    request.Dimension, request.Amount, request.Unit,
                    expiresAt,
                    request.IdempotencyKey, batch, scope._logger);
                child?.AttachParent(reservation);
                target ??= reservation;
                child = reservation;
                plans.Add((reservation, request.Unit));
            }
            targets.Add(target!);
        }
        batch.TargetReservations = targets.MoveToImmutable();
        foreach (var (reservation, unit) in plans)
        {
            reservation.Scope.AddLocalLocked(reservation, unit);
        }

        return [.. batch.TargetReservations.Cast<IBudgetReservation>()];
    }

    private void AddLocalLocked(InMemoryBudgetReservation reservation, BudgetUnit unit)
    {
        var state = GetOrCreateState(reservation.Dimension, unit);
        state.Reserved += reservation.Reserved;
        state.Open.Add(reservation);
        _batchesByItemKey[reservation.IdempotencyKey] = reservation.Batch;
    }

    private InMemoryBudgetDimensionState GetOrCreateState(BudgetDimension dimension, BudgetUnit unit)
    {
        if (_states.TryGetValue(dimension, out var state))
        {
            Debug.Assert(state.Unit == unit, "Caller validation must preserve one unit for each budget dimension.");
            return state;
        }
        state = new InMemoryBudgetDimensionState(unit);
        _states.Add(dimension, state);
        return state;
    }

    private void SweepExpiredLocked()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var reservation in _states.Values.SelectMany(static state => state.Open).ToArray())
        {
            if (reservation.IsOpen && reservation.ExpiresAt <= now)
            {
                reservation.ReleaseHierarchyLocked();
            }
        }
    }

    private void RecomputeBlocked(BudgetDimension dimension, InMemoryBudgetDimensionState state,
        InMemoryBudgetReservation? correcting = null, decimal correctedActual = 0m)
    {
        var overrun = state.CommittedReservations.Any(item =>
            (ReferenceEquals(item, correcting) ? correctedActual : item.Actual) > item.Reserved);
        state.Blocked = overrun || (Limits.TryGetValue(dimension, out var limit)
            && limit.Kind == BudgetLimitKind.Hard && state.Committed > limit.Value);
    }

}
