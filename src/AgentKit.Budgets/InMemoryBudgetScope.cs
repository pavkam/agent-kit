// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>
/// The in-memory <see cref="IBudgetScope"/>/<see cref="IRunBudget"/>
/// implementation created by <see cref="InMemoryBudgetAuthority"/>.
/// </summary>
/// <remarks>
/// A scope enforces its own configured limits and, when it has a parent,
/// atomically cascades the same reservation to that parent before
/// succeeding, so effective capacity is always the tightest applicable
/// constraint across the whole hierarchy. Every mutation to this scope's
/// per-dimension bookkeeping happens under one scope-wide lock; this is a
/// simple, correct, non-lock-free implementation appropriate for a
/// first-party in-memory process authority.
/// </remarks>
internal sealed class InMemoryBudgetScope: IBudgetScope, IRunBudget
{
    private readonly Lock _gate = new();
    private readonly Dictionary<BudgetDimension, DimensionState> _states = [];
    private readonly Dictionary<(BudgetDimension Dimension, IdempotencyKey Key), InMemoryBudgetReservation> _byIdempotencyKey = [];
    private readonly IIdentifierGenerator<BudgetReservationId> _reservationIds;
    private readonly TimeProvider _timeProvider;
    private readonly AgentBudgetOptionsSnapshot _options;

    /// <summary>Initializes a new instance of the <see cref="InMemoryBudgetScope"/> class.</summary>
    /// <param name="id">The identity of this scope.</param>
    /// <param name="address">The hierarchical address this scope occupies.</param>
    /// <param name="parent">The parent scope, when this scope is not a root scope.</param>
    /// <param name="limits">The limits configured directly on this scope.</param>
    /// <param name="reservationIds">Generates identities for reservations created against this scope.</param>
    /// <param name="timeProvider">The clock used to timestamp and expire reservations.</param>
    /// <param name="options">The validated authority options.</param>
    public InMemoryBudgetScope(
        BudgetScopeId id,
        BudgetScopeAddress address,
        InMemoryBudgetScope? parent,
        ImmutableArray<BudgetLimit> limits,
        IIdentifierGenerator<BudgetReservationId> reservationIds,
        TimeProvider timeProvider,
        AgentBudgetOptionsSnapshot options)
    {
        Id = id;
        Address = address;
        Parent = parent;
        Depth = (parent?.Depth ?? -1) + 1;
        Limits = limits.ToImmutableDictionary(static limit => limit.Dimension);
        _reservationIds = reservationIds;
        _timeProvider = timeProvider;
        _options = options;

        foreach (var limit in limits)
        {
            _states[limit.Dimension] = new DimensionState(limit.Unit);
        }
    }

    /// <inheritdoc/>
    public BudgetScopeId Id { get; }

    /// <inheritdoc/>
    public BudgetScopeAddress Address { get; }

    /// <summary>Gets the parent scope, when this scope is not a root scope.</summary>
    public InMemoryBudgetScope? Parent { get; }

    /// <summary>Gets this scope's depth in the hierarchy; a root scope has depth zero.</summary>
    public int Depth { get; }

    /// <summary>Gets the limits configured directly on this scope, keyed by dimension.</summary>
    public ImmutableDictionary<BudgetDimension, BudgetLimit> Limits { get; }

    /// <inheritdoc/>
    public async ValueTask<BudgetReservationResult> ReserveAsync(
        BudgetReservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ScopeId != Id)
        {
            throw new ArgumentException("The request's ScopeId does not match this scope's Id.", nameof(request));
        }

        if (Limits.TryGetValue(request.Dimension, out var configuredLimit) && configuredLimit.Unit != request.Unit)
        {
            throw new ArgumentException(
                $"Dimension '{request.Dimension}' is configured with unit '{configuredLimit.Unit}', not '{request.Unit}'.",
                nameof(request));
        }

        await SweepExpiredAsync(request.Dimension).ConfigureAwait(false);

        var (reservation, failure) = TryReserveLocal(request, configuredLimit);
        if (failure is not null)
        {
            return new BudgetRejected(failure);
        }

        var ownReservation = reservation!;

        if (Parent is null)
        {
            return new BudgetReserved(ownReservation);
        }

        var parentRequest = new BudgetReservationRequest(
            Parent.Id, request.Dimension, request.Amount, request.Unit, request.OperationId,
            request.ExpiresAt, request.IdempotencyKey);

        var parentResult = await Parent.ReserveAsync(parentRequest, cancellationToken).ConfigureAwait(false);

        if (parentResult is BudgetRejected parentRejected)
        {
            await ownReservation.DisposeAsync().ConfigureAwait(false);
            return new BudgetRejected(parentRejected.Failure);
        }

        ownReservation.AttachParent(((BudgetReserved) parentResult).Reservation);
        return new BudgetReserved(ownReservation);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var usages = _states
                .Select(pair =>
                {
                    _ = Limits.TryGetValue(pair.Key, out var limit);
                    return new BudgetDimensionUsage(pair.Key, pair.Value.Unit, pair.Value.Reserved, pair.Value.Committed, limit);
                })
                .ToImmutableArray();

            return ValueTask.FromResult(new BudgetSnapshot(Id, now, usages));
        }
    }

    /// <summary>Settles a committed reservation's bookkeeping and returns its settlement outcome.</summary>
    /// <param name="reservation">The reservation being committed.</param>
    /// <param name="actual">The actual amount consumed.</param>
    /// <returns>The settlement outcome for this scope's level.</returns>
    internal BudgetCommitResult SettleCommit(InMemoryBudgetReservation reservation, decimal actual)
    {
        lock (_gate)
        {
            var state = _states[reservation.Dimension];
            state.Reserved -= reservation.Reserved;
            state.Committed += actual;
            _ = state.Open.Remove(reservation);
            RemoveIdempotencyEntryLocked(reservation);

            var released = Math.Max(0m, reservation.Reserved - actual);
            var overrun = Math.Max(0m, actual - reservation.Reserved);

            if (overrun > 0m && Limits.TryGetValue(reservation.Dimension, out var limit) && limit.Kind == BudgetLimitKind.Hard)
            {
                state.Blocked = true;
            }

            return new BudgetCommitResult(reservation.Id, reservation.Reserved, actual, released, overrun);
        }
    }

    /// <summary>Releases an uncommitted reservation's bookkeeping.</summary>
    /// <param name="reservation">The reservation being released.</param>
    internal void SettleRelease(InMemoryBudgetReservation reservation)
    {
        lock (_gate)
        {
            if (_states.TryGetValue(reservation.Dimension, out var state))
            {
                state.Reserved -= reservation.Reserved;
                _ = state.Open.Remove(reservation);
            }

            RemoveIdempotencyEntryLocked(reservation);
        }
    }

    private (InMemoryBudgetReservation? Reservation, BudgetLimitFailure? Failure) TryReserveLocal(
        BudgetReservationRequest request, BudgetLimit? configuredLimit)
    {
        lock (_gate)
        {
            var key = (request.Dimension, request.IdempotencyKey);
            if (_byIdempotencyKey.TryGetValue(key, out var existing) && existing.IsOpen)
            {
                return existing.Reserved != request.Amount
                    ? throw new InvalidOperationException(
                        $"Idempotency key '{request.IdempotencyKey}' was already used for a reservation of a different amount.")
                    : (existing, null);
            }

            if (!_states.TryGetValue(request.Dimension, out var state))
            {
                state = new DimensionState(request.Unit);
                _states[request.Dimension] = state;
            }

            var openCount = _states.Values.Sum(static s => s.Open.Count);
            var observed = state.Reserved + state.Committed;

            var failure = state switch
            {
                { Blocked: true } => new BudgetLimitFailure(
                    Id, request.Dimension, BudgetLimitKind.Hard, configuredLimit?.Value ?? 0m, observed,
                    request.Amount, request.Unit,
                    $"Dimension '{request.Dimension}' is blocked from further reservations after a prior overrun."),

                _ when openCount >= _options.MaximumOpenReservationsPerScope => new BudgetLimitFailure(
                    Id, request.Dimension, BudgetLimitKind.Hard, _options.MaximumOpenReservationsPerScope,
                    openCount, 1, new BudgetUnit("reservations"),
                    "The scope already holds the maximum number of open reservations."),

                _ when configuredLimit is { Kind: BudgetLimitKind.Hard } && observed + request.Amount > configuredLimit.Value =>
                    new BudgetLimitFailure(
                        Id, request.Dimension, BudgetLimitKind.Hard, configuredLimit.Value, observed,
                        request.Amount, request.Unit,
                        $"Reserving {request.Amount} {request.Unit} for '{request.Dimension}' would exceed the " +
                            $"hard limit of {configuredLimit.Value}."),

                _ => null,
            };

            if (failure is not null)
            {
                return (null, failure);
            }

            var expiresAt = request.ExpiresAt ?? (_timeProvider.GetUtcNow() + _options.DefaultReservationLifetime);
            var reservation = new InMemoryBudgetReservation(
                _reservationIds.Create(), this, request.Dimension, request.Amount, expiresAt, request.IdempotencyKey);

            state.Reserved += request.Amount;
            state.Open.Add(reservation);
            _byIdempotencyKey[key] = reservation;

            return (reservation, null);
        }
    }

    private void RemoveIdempotencyEntryLocked(InMemoryBudgetReservation reservation)
    {
        var key = (reservation.Dimension, reservation.IdempotencyKey);
        if (_byIdempotencyKey.TryGetValue(key, out var current) && ReferenceEquals(current, reservation))
        {
            _ = _byIdempotencyKey.Remove(key);
        }
    }

    private async ValueTask SweepExpiredAsync(BudgetDimension dimension)
    {
        List<InMemoryBudgetReservation>? expired = null;

        lock (_gate)
        {
            if (_states.TryGetValue(dimension, out var state))
            {
                var now = _timeProvider.GetUtcNow();
                foreach (var candidate in state.Open)
                {
                    if (candidate.IsOpen && candidate.ExpiresAt <= now)
                    {
                        expired ??= [];
                        expired.Add(candidate);
                    }
                }
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var candidate in expired)
        {
            await candidate.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class DimensionState(BudgetUnit unit)
    {
        public BudgetUnit Unit { get; } = unit;

        public decimal Reserved { get; set; }

        public decimal Committed { get; set; }

        public bool Blocked { get; set; }

        public List<InMemoryBudgetReservation> Open { get; } = [];
    }
}
