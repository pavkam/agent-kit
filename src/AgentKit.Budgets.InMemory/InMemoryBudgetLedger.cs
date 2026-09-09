// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Stores one atomic budget hierarchy in process memory.</summary>
/// <remarks>State is ephemeral. One lock linearizes topology, accounting, replay, expiry, and recovery scans.</remarks>
public sealed class InMemoryBudgetLedger: IBudgetLedger
{
    private readonly Lock _gate = new();
    private readonly Dictionary<BudgetScopeId, ScopeState> _scopes = [];
    private readonly Dictionary<IdempotencyKey, ScopeState> _scopeKeys = [];
    private readonly Dictionary<BudgetReservationId, ReservationState> _reservations = [];
    private readonly Dictionary<IdempotencyKey, BatchState> _batchKeys = [];
    private readonly Dictionary<IdempotencyKey, OverrunResolutionState> _overrunResolutionKeys = [];
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<BudgetScopeId> _scopeIds;
    private readonly IIdentifierGenerator<BudgetReservationId> _reservationIds;
    private readonly IBudgetDimensionCatalog _dimensions;
    private readonly ILogger<InMemoryBudgetLedger> _logger;
    private long _revision;

    /// <summary>Creates an ephemeral ledger using the supplied deterministic clock.</summary>
    /// <param name="timeProvider">The clock used for expiry, snapshots, and start evidence.</param>
    /// <param name="scopeIds">The replaceable source of scope identities.</param>
    /// <param name="reservationIds">The replaceable source of reservation identities.</param>
    /// <param name="dimensions">The captured dimension semantics used to validate and aggregate accounting.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public InMemoryBudgetLedger(
        TimeProvider timeProvider,
        IIdentifierGenerator<BudgetScopeId> scopeIds,
        IIdentifierGenerator<BudgetReservationId> reservationIds,
        IBudgetDimensionCatalog dimensions)
        : this(timeProvider, scopeIds, reservationIds, dimensions, null)
    {
    }

    /// <summary>Creates an ephemeral ledger with failure-isolated structured diagnostics.</summary>
    /// <param name="timeProvider">The clock used for expiry, snapshots, and start evidence.</param>
    /// <param name="scopeIds">The replaceable source of scope identities.</param>
    /// <param name="reservationIds">The replaceable source of reservation identities.</param>
    /// <param name="dimensions">The captured dimension semantics used to validate and aggregate accounting.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public InMemoryBudgetLedger(
        TimeProvider timeProvider,
        IIdentifierGenerator<BudgetScopeId> scopeIds,
        IIdentifierGenerator<BudgetReservationId> reservationIds,
        IBudgetDimensionCatalog dimensions,
        ILogger<InMemoryBudgetLedger>? logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(scopeIds);
        ArgumentNullException.ThrowIfNull(reservationIds);
        ArgumentNullException.ThrowIfNull(dimensions);
        _timeProvider = timeProvider;
        _scopeIds = scopeIds;
        _reservationIds = reservationIds;
        _dimensions = dimensions;
        _logger = logger ?? NullLogger<InMemoryBudgetLedger>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync("create_scope", token => CreateScopeCoreAsync(request, token), ResultOutcome, DiagnosticTags(request.OriginalRequest.Address), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync("reserve_batch", token => ReserveBatchCoreAsync(request, token), ResultOutcome, DiagnosticTags(request.Scope.Address, request.Scope.Id, operationId: request.OriginalRequests[0].OperationId), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return ObserveAsync("mark_started", token => MarkStartedCoreAsync(reservation, token), ResultOutcome, DiagnosticTags(reservation.Scope.Address, reservation.Scope.Id, reservation.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync("settle", token => SettleCoreAsync(request, token), static _ => "settled", DiagnosticTags(request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return ObserveAsync("release_unstarted", token => ReleaseUnstartedCoreAsync(reservation, token), ResultOutcome, DiagnosticTags(reservation.Scope.Address, reservation.Scope.Id, reservation.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync("correct", token => CorrectCoreAsync(request, token), static _ => "corrected", DiagnosticTags(request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ObserveAsync("get_snapshot", token => GetSnapshotCoreAsync(scope, token), static _ => "read", DiagnosticTags(scope.Address, scope.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ObserveAsync("read_unresolved", token => ReadUnresolvedStartedCoreAsync(query, token), static _ => "read", DiagnosticTags(query.Scope.Address, query.Scope.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync("reconcile", token => ReconcileCoreAsync(request, token), ResultOutcome, DiagnosticTags(request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync("resolve_overrun_hold", token => ResolveOverrunHoldCoreAsync(request, token), ResultOutcome, DiagnosticTags(request.Hold.Boundary.Address, request.Hold.Boundary.Id, request.Hold.Reservation.Id), cancellationToken);
    }

    private ValueTask<BudgetLedgerScopeCreateResult> CreateScopeCoreAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_scopeKeys.TryGetValue(request.OriginalRequest.IdempotencyKey, out var replay))
            {
                return replay.Request != request
                    ? throw Conflict("The scope key is bound to different evidence.")
                    : ValueTask.FromResult<BudgetLedgerScopeCreateResult>(new BudgetLedgerScopeCreated(replay.Reference));
            }
            ScopeState? parent = null;
            if (request.OriginalRequest.ParentScopeId is { } parentId)
            {
                if (!_scopes.TryGetValue(parentId, out parent) || !IsParentAddress(parent.Reference.Address, request.OriginalRequest.Address))
                {
                    throw Missing("The parent scope is unavailable.");
                }
            }
            var depth = (parent?.Depth ?? 0) + 1;
            if (depth > request.Admission.MaximumScopeDepth)
            {
                return ValueTask.FromResult<BudgetLedgerScopeCreateResult>(Rejected(BudgetScopeCreationFailureKind.MaximumDepthExceeded, "The scope would exceed its captured maximum depth."));
            }

            foreach (var limit in request.OriginalRequest.Limits)
            {
                if (!_dimensions.TryGet(limit.Dimension, out var descriptor) || !descriptor.AllowedUnits.Contains(limit.Unit))
                {
                    return ValueTask.FromResult<BudgetLedgerScopeCreateResult>(Rejected(BudgetScopeCreationFailureKind.InvalidLimit, "The scope limit has no compatible dimension descriptor."));
                }
                for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
                {
                    var inherited = ancestor.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == limit.Dimension && item.Unit == limit.Unit);
                    var incompatible = ancestor.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == limit.Dimension && item.Unit != limit.Unit);
                    if (incompatible is not null)
                    {
                        return ValueTask.FromResult<BudgetLedgerScopeCreateResult>(Rejected(BudgetScopeCreationFailureKind.InvalidLimit, "The scope limit unit conflicts with an ancestor limit."));
                    }
                    if (inherited is { Kind: BudgetLimitKind.Hard } && limit.Value > inherited.Value)
                    {
                        return ValueTask.FromResult<BudgetLedgerScopeCreateResult>(Rejected(BudgetScopeCreationFailureKind.LimitWiderThanAncestor, "The scope limit widens an ancestor hard limit."));
                    }
                }
            }
            var id = _scopeIds.Create();
            ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
            if (_scopes.ContainsKey(id))
            {
                throw new BudgetLedgerStateException("The scope identity source produced a duplicate value.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var state = new ScopeState(new BudgetLedgerScopeReference(id, request.OriginalRequest.Address), request, parent, depth);
            EnsureRevisionCapacity(1);
            _scopes.Add(id, state);
            _scopeKeys.Add(request.OriginalRequest.IdempotencyKey, state);
            _ = AdvanceRevision();
            return ValueTask.FromResult<BudgetLedgerScopeCreateResult>(new BudgetLedgerScopeCreated(state.Reference));
        }
    }

    private ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchCoreAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = GetScope(request.Scope);
            BatchState? replay = null;
            foreach (var item in request.OriginalRequests)
            {
                if (_batchKeys.TryGetValue(item.IdempotencyKey, out var found))
                {
                    if (replay is not null && !ReferenceEquals(replay, found))
                    {
                        throw Conflict("Batch item keys belong to different batches.");
                    }

                    replay = found;
                }
            }
            if (replay is not null)
            {
                return replay.Request != request || request.OriginalRequests.Any(item => !_batchKeys.ContainsKey(item.IdempotencyKey))
                    ? throw Conflict("A batch item key is bound to different ordered evidence.")
                    : ValueTask.FromResult<BudgetLedgerBatchReserveResult>(replay.Result);
            }
            var descriptors = ImmutableArray.CreateBuilder<BudgetDimensionDescriptor>(request.OriginalRequests.Length);
            foreach (var item in request.OriginalRequests)
            {
                if (!_dimensions.TryGet(item.Dimension, out var descriptor) || !descriptor.AllowedUnits.Contains(item.Unit))
                {
                    throw new BudgetLedgerStateException("The reservation has no compatible dimension descriptor.");
                }
                if (!Enum.IsDefined(descriptor.Aggregation))
                {
                    throw new BudgetLedgerStateException("The reservation dimension has undefined aggregation semantics.");
                }
                if (request.OriginalRequests.Any(candidate => candidate.Dimension == item.Dimension && candidate.Unit != item.Unit))
                {
                    throw new BudgetLedgerStateException("One atomic batch cannot mix units for the same dimension.");
                }
                descriptors.Add(descriptor);
            }
            var lineage = Lineage(scope);
            foreach (var boundary in lineage)
            {
                foreach (var item in request.OriginalRequests)
                {
                    if (boundary.Reservations.Any(existing =>
                            existing.Receipt.OriginalRequest.Dimension == item.Dimension
                            && existing.Receipt.OriginalRequest.Unit != item.Unit))
                    {
                        throw new BudgetLedgerStateException("The reservation unit conflicts with existing accounting at a charged boundary.");
                    }
                }
            }
            var requestedDimensions = request.OriginalRequests.Select(item => item.Dimension).ToHashSet();
            var activeHolds = lineage
                .SelectMany(boundary => ActiveHoldEvidence(boundary))
                .Where(hold => requestedDimensions.Contains(hold.Dimension))
                .ToImmutableArray();
            if (!activeHolds.IsEmpty)
            {
                return ValueTask.FromResult<BudgetLedgerBatchReserveResult>(new BudgetLedgerBatchReserveHeld(activeHolds));
            }
            var now = _timeProvider.GetUtcNow();
            var expired = FindExpired(now);
            foreach (var boundary in lineage)
            {
                var open = boundary.Reservations.Count(item => item.IsCapacityRetaining && !expired.Contains(item));
                if (open + request.OriginalRequests.Length > boundary.Request.Admission.MaximumOpenReservationsPerScope)
                {
                    throw new BudgetLedgerStateException("The atomic batch would exceed the scope's captured open-reservation capacity.");
                }

                foreach (var group in request.OriginalRequests.GroupBy(item => (item.Dimension, item.Unit)))
                {
                    var limit = boundary.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == group.Key.Dimension);
                    if (limit is not null && limit.Unit != group.Key.Unit)
                    {
                        throw new BudgetLedgerStateException("The reservation unit conflicts with a captured scope limit.");
                    }

                    var descriptor = descriptors.First(item => item.Dimension == group.Key.Dimension);
                    var (Reserved, Committed) = Usage(boundary, group.Key.Dimension, expired);
                    var observed = descriptor.Aggregation == BudgetAggregationKind.Maximum
                        ? Max(Reserved, Committed)
                        : Reserved.Add(Committed);
                    var amount = descriptor.Aggregation switch
                    {
                        BudgetAggregationKind.Maximum => BudgetQuantity.FromDecimal(group.Max(item => item.Amount)),
                        BudgetAggregationKind.ConcurrentGauge or BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => Sum(group.Select(item => item.Amount)),
                        _ => throw new BudgetLedgerStateException("The dimension descriptor has an unsupported aggregation kind."),
                    };
                    var projected = descriptor.Aggregation == BudgetAggregationKind.Maximum
                        ? Max(observed, amount)
                        : observed.Add(amount);
                    if (limit is { Kind: BudgetLimitKind.Hard } && projected.CompareTo(BudgetQuantity.FromDecimal(limit.Value)) > 0)
                    {
                        return ValueTask.FromResult<BudgetLedgerBatchReserveResult>(Reject(boundary, group.First(), limit.Value, observed, amount, limit.Unit.Value));
                    }
                }
            }
            var receiptsBuilder = ImmutableArray.CreateBuilder<BudgetLedgerReservationReceipt>(request.OriginalRequests.Length);
            var newIds = new HashSet<BudgetReservationId>();
            foreach (var item in request.OriginalRequests)
            {
                var id = _reservationIds.Create();
                ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
                if (_reservations.ContainsKey(id) || !newIds.Add(id))
                {
                    throw new BudgetLedgerStateException("The reservation identity source produced a duplicate value.");
                }
                var reference = new BudgetLedgerReservationReference(scope.Reference, id);
                var receipt = new BudgetLedgerReservationReceipt(reference, item, new BudgetEffectiveReservation(item.ExpiresAt ?? (now + scope.Request.Admission.DefaultReservationLifetime)));
                receiptsBuilder.Add(receipt);
            }
            var receipts = receiptsBuilder.ToImmutable();
            var result = new BudgetLedgerBatchReserved(receipts);
            var batch = new BatchState(request, result);
            EnsureRevisionCapacity(expired.Count + 1);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var expiredReservation in expired)
            {
                _ = Expire(expiredReservation);
            }
            for (var index = 0; index < receipts.Length; index++)
            {
                var receipt = receipts[index];
                var reservation = new ReservationState(receipt, lineage, descriptors[index].Aggregation);
                _reservations.Add(receipt.Reservation.Id, reservation);
                foreach (var boundary in lineage)
                {
                    boundary.Reservations.Add(reservation);
                }
            }
            foreach (var item in request.OriginalRequests)
            {
                _batchKeys.Add(item.IdempotencyKey, batch);
            }

            _ = AdvanceRevision();
            return ValueTask.FromResult<BudgetLedgerBatchReserveResult>(result);
        }
    }

    private ValueTask<BudgetStartResult> MarkStartedCoreAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken)
    {
        Debug.Assert(reservation is not null, "The public wrapper validates the reservation reference before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(reservation);
            if (state.Commit is not null || state.Released)
            {
                return state.StartExpiration is not null
                    ? ValueTask.FromResult<BudgetStartResult>(state.StartExpiration)
                    : throw new BudgetLedgerStateException("The reservation is terminal and cannot start.");
            }

            if (state.StartedAt is not null)
            {
                return ValueTask.FromResult<BudgetStartResult>(new BudgetStarted(reservation.Id, true));
            }

            var now = _timeProvider.GetUtcNow();
            cancellationToken.ThrowIfCancellationRequested();
            if (now >= state.Receipt.EffectiveReservation.ExpiresAt)
            {
                EnsureRevisionCapacity(1);
                return ValueTask.FromResult<BudgetStartResult>(Expire(state));
            }
            EnsureRevisionCapacity(1);
            state.StartedAt = now;
            state.StartRevision = AdvanceRevision();
            return ValueTask.FromResult<BudgetStartResult>(new BudgetStarted(reservation.Id, false));
        }
    }

    private ValueTask<BudgetCommitResult> SettleCoreAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(request.Reservation);
            return ValueTask.FromResult(Settle(state, request.Actual, cancellationToken));
        }
    }

    private ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedCoreAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken)
    {
        Debug.Assert(reservation is not null, "The public wrapper validates the reservation reference before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(reservation);
            if (state.Commit is not null)
            {
                return ValueTask.FromResult<BudgetLedgerReleaseResult>(new BudgetLedgerAlreadySettled(state.Commit));
            }

            if (state.StartedAt is not null)
            {
                return ValueTask.FromResult<BudgetLedgerReleaseResult>(new BudgetLedgerRetainedStarted(reservation));
            }

            EnsureRevisionCapacity(1);
            Release(state);
            return ValueTask.FromResult<BudgetLedgerReleaseResult>(new BudgetLedgerReleased(reservation));
        }
    }

    private ValueTask<BudgetCorrectionResult> CorrectCoreAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(request.Reservation);
            if (state.Commit is null)
            {
                throw new BudgetLedgerStateException("Only settled accounting can be corrected.");
            }

            if (state.Corrections.TryGetValue(request.Revision, out var replay))
            {
                return replay.CorrectedActual != request.CorrectedActual
                ? throw Conflict("The correction revision is bound to different usage.")
                : ValueTask.FromResult(replay);
            }
            if (request.Revision <= state.LatestCorrectionRevision)
            {
                throw new BudgetLedgerStateException("Correction revisions must increase monotonically.");
            }

            EnsureRevisionCapacity(1);
            var accountingRevision = new BudgetAccountingRevision(checked(_revision + 1));
            var previousActual = state.Commit.Actual;
            var reserved = state.Receipt.OriginalRequest.Amount;
            var createdStates = ImmutableArray.CreateBuilder<(ScopeState Boundary, OverrunHoldState Hold)>();
            if (previousActual <= reserved && request.CorrectedActual > reserved)
            {
                foreach (var boundary in state.Lineage.Where(boundary => !boundary.OverrunHolds.Any(hold => hold.IsActive && hold.Evidence.Reference.Reservation == state.Receipt.Reservation)))
                {
                    var evidence = HoldEvidence(boundary, state, accountingRevision, request.CorrectedActual);
                    createdStates.Add((boundary, new OverrunHoldState(evidence)));
                }
            }
            var clearing = state.Lineage
                .SelectMany(boundary => EligibleForClear(boundary, state, request.CorrectedActual)
                    ? boundary.OverrunHolds.Where(hold => hold.IsActive && hold.Evidence.Policy == BudgetOverrunHoldPolicy.ClearWhenReconciled && hold.Evidence.Dimension == state.Receipt.OriginalRequest.Dimension)
                    : [])
                .ToImmutableArray();
            var result = new BudgetCorrectionResult(
                request.Reservation.Id,
                previousActual,
                request.CorrectedActual,
                request.Revision,
                accountingRevision,
                [.. createdStates.Select(item => item.Hold.Evidence)],
                [.. clearing.Select(item => item.Evidence.Reference)]);
            var commit = Commit(request.Reservation.Id, reserved, request.CorrectedActual, accountingRevision, [.. createdStates.Select(item => item.Hold.Evidence)]);
            cancellationToken.ThrowIfCancellationRequested();
            state.Commit = commit;
            state.AccountingRevision = accountingRevision;
            state.LatestCorrectionRevision = request.Revision;
            state.Corrections.Add(request.Revision, result);
            foreach (var (Boundary, Hold) in createdStates)
            {
                Boundary.OverrunHolds.Add(Hold);
            }
            foreach (var hold in clearing)
            {
                hold.AutomaticallyCleared = true;
            }
            _ = AdvanceRevision();
            return ValueTask.FromResult(result);
        }
    }

    private ValueTask<BudgetSnapshot> GetSnapshotCoreAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken)
    {
        Debug.Assert(scope is not null, "The public wrapper validates the scope reference before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetScope(scope);
            var now = _timeProvider.GetUtcNow();
            var expired = FindExpired(now);
            var dimensions = state.Request.OriginalRequest.Limits
                .Select(item => item.Dimension)
                .Concat(state.Reservations.Select(item => item.Receipt.OriginalRequest.Dimension))
                .Distinct();
            var usages = dimensions.Select(dimension =>
            {
                var (reserved, committed) = Usage(state, dimension, expired);
                var limit = state.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == dimension);
                var unit = limit?.Unit ?? state.Reservations.First(item => item.Receipt.OriginalRequest.Dimension == dimension).Receipt.OriginalRequest.Unit;
                return new BudgetDimensionUsage(dimension, unit, reserved, committed, limit);
            }).ToImmutableArray();
            var snapshot = new BudgetSnapshot(scope.Id, now, usages, ActiveHoldEvidence(state));
            EnsureRevisionCapacity(expired.Count);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var expiredReservation in expired)
            {
                _ = Expire(expiredReservation);
            }
            return ValueTask.FromResult(snapshot);
        }
    }

    private ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedCoreAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken)
    {
        Debug.Assert(query is not null, "The public wrapper validates the query before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = GetScope(query.Scope);
            if (query.PageSize > scope.Request.Admission.MaximumOpenReservationsPerScope)
            {
                throw new BudgetLedgerStateException("The page size exceeds the captured finite ledger bound.");
            }

            var watermark = query.After?.Watermark ?? new BudgetLedgerWatermark(Math.Max(1, _revision));
            if (watermark.Value > _revision)
            {
                throw new BudgetLedgerStateException("The scan watermark is not known to this ledger state.");
            }
            if (query.After is { } cursor)
            {
                if (!_reservations.TryGetValue(cursor.AfterReservationId, out var cursorReservation)
                    || cursorReservation.Receipt.Reservation.Scope != query.Scope
                    || cursorReservation.StartedAt is null
                    || cursorReservation.StartRevision > watermark.Value)
                {
                    throw new BudgetLedgerStateException("The scan cursor is not valid at its anchored watermark.");
                }
            }
            var rows = scope.Reservations.Where(r => r.Receipt.Reservation.Scope == query.Scope && r.StartedAt is not null && r.Commit is null && !r.Released && r.StartRevision <= watermark.Value)
                .OrderBy(r => r.Receipt.Reservation.Id.Value.ToString("D"), StringComparer.Ordinal).ToArray();
            if (query.After is not null)
            {
                rows = [.. rows.Where(r => string.CompareOrdinal(r.Receipt.Reservation.Id.Value.ToString("D"), query.After.AfterReservationId.Value.ToString("D")) > 0)];
            }

            var selected = rows.Take(query.PageSize).Select(r => new BudgetUnresolvedReservation(r.Receipt, r.StartedAt!.Value)).ToImmutableArray();
            var next = rows.Length > selected.Length && selected.Length > 0 ? new BudgetReservationCursor(query.Scope, watermark, selected[^1].Receipt.Reservation.Id) : null;
            return ValueTask.FromResult(new BudgetUnresolvedReservationPage(query.Scope, watermark, selected, next));
        }
    }

    private ValueTask<BudgetLedgerReconciliationResult> ReconcileCoreAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(request.Reservation);
            if (state.Reconciliations.TryGetValue(request.IdempotencyKey, out var saved))
            {
                return saved.Evidence != request.Evidence
                ? throw Conflict("The reconciliation key is bound to different evidence.")
                : ValueTask.FromResult(saved.Result);
            }
            if (state.Commit is not null || state.Released)
            {
                throw new BudgetLedgerStateException("Fresh reconciliation evidence cannot target a terminal reservation.");
            }

            if (state.StartedAt is null)
            {
                throw new BudgetLedgerStateException("Only a started reservation can be reconciled.");
            }

            EnsureRevisionCapacity(request.Evidence is BudgetStillUnknown ? 1 : 2);
            BudgetLedgerReconciliationResult result = request.Evidence switch
            {
                BudgetActualMeasured measured => new BudgetLedgerReconciliationSettled(Settle(state, measured.Actual, cancellationToken)),
                BudgetActualEstimated estimated => new BudgetLedgerReconciliationSettled(Settle(state, estimated.Actual, cancellationToken)),
                BudgetNoUsageProven => ReleaseForReconciliation(state),
                BudgetStillUnknown => new BudgetLedgerReconciliationRetainedUnknown(request.Reservation),
                _ => throw new InvalidOperationException("Unknown reconciliation evidence.")
            };
            state.Reconciliations.Add(request.IdempotencyKey, new ReconciliationState(request.Evidence, result));
            _ = AdvanceRevision();
            return ValueTask.FromResult(result);
        }
    }

    private ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldCoreAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_overrunResolutionKeys.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return replay.Request != request
                    ? throw Conflict("The overrun-resolution key is bound to different evidence.")
                    : ValueTask.FromResult(replay.Result);
            }
            var boundary = GetScope(request.Hold.Boundary);
            var reservation = GetReservation(request.Hold.Reservation);
            if (!reservation.Lineage.Contains(boundary))
            {
                throw Missing("The overrun hold reference is unavailable.");
            }
            var hold = boundary.OverrunHolds.FirstOrDefault(item => item.Evidence.Reference == request.Hold)
                ?? throw new BudgetLedgerStateException("The overrun hold generation is stale or unavailable.");
            if (hold.Resolution is not null)
            {
                throw new BudgetLedgerStateException("A fresh key cannot resolve an already terminal overrun hold generation.");
            }
            if (hold.Evidence.Policy != BudgetOverrunHoldPolicy.RequireAuthorizedResolution || !hold.IsActive)
            {
                throw new BudgetLedgerStateException("The overrun hold generation cannot accept operator resolution.");
            }
            if (!BudgetOverrunSecurityBinding.Matches(request.Hold, request.EnforcementReceipt))
            {
                throw new BudgetLedgerStateException("The enforcement receipt is not structurally bound to the requested hold.");
            }
            var currentOverruns = CurrentOverruns(boundary, hold.Evidence.Dimension);
            var hardFailures = CurrentHardFailures(boundary, hold.Evidence.Dimension);
            BudgetOverrunHoldResolutionResult result;
            EnsureRevisionCapacity(1);
            result = !currentOverruns.IsEmpty || !hardFailures.IsEmpty
                ? new BudgetOverrunHoldResolutionBlocked(request.Hold, currentOverruns, hardFailures)
                : new BudgetOverrunHoldResolved(request.Hold, new BudgetAccountingRevision(checked(_revision + 1)), request.EnforcementReceipt);
            var saved = new OverrunResolutionState(request, result);
            cancellationToken.ThrowIfCancellationRequested();
            if (result is BudgetOverrunHoldResolved resolved)
            {
                hold.Resolution = resolved;
            }
            _overrunResolutionKeys.Add(request.IdempotencyKey, saved);
            _ = AdvanceRevision();
            return ValueTask.FromResult(result);
        }
    }

    private async ValueTask<TResult> ObserveAsync<TResult>(
        string operation,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<TResult, string> outcomeSelector,
        ActivityTagsCollection tags,
        CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "The wrapper supplies a bounded operation name.");
        Debug.Assert(action is not null, "The wrapper supplies the semantic operation.");
        Debug.Assert(outcomeSelector is not null, "The wrapper supplies a bounded result classifier.");
        Debug.Assert(tags is not null, "The wrapper supplies applicable correlation tags.");
        long? started = null;
        TryObserve(() => started = _timeProvider.GetTimestamp());
        AgentKitActivityScope? activityScope = null;
        tags.Add(AgentKitTagNames.BudgetOperation, operation);
        TryObserve(() => activityScope = AgentKitActivityScope.Start(AgentKitActivityNames.BudgetLedgerOperation, ActivityKind.Internal, tags));
        var scopeId = Tag(tags, AgentKitTagNames.BudgetScopeId);
        var reservationId = Tag(tags, AgentKitTagNames.BudgetReservationId);
        var tenantId = Tag(tags, AgentKitTagNames.TenantId);
        var principalId = Tag(tags, AgentKitTagNames.PrincipalId);
        var agentId = Tag(tags, AgentKitTagNames.AgentId);
        var sessionId = Tag(tags, AgentKitTagNames.SessionId);
        var runId = Tag(tags, AgentKitTagNames.RunId);
        var operationId = Tag(tags, AgentKitTagNames.OperationId);
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            var outcome = outcomeSelector(result);
            if (result is BudgetLedgerScopeCreated created)
            {
                scopeId = created.Scope.Id.ToString();
                TryObserve(() => activityScope?.Activity?.SetTag(AgentKitTagNames.BudgetScopeId, scopeId));
            }
            TryObserve(() =>
            {
                _ = activityScope?.Activity?.SetTag(AgentKitTagNames.Outcome, outcome);
                _ = (activityScope?.Activity?.SetStatus(IsStartForbidden(outcome) ? ActivityStatusCode.Error : ActivityStatusCode.Ok));
            });
            TryObserve(() => BudgetLedgerLog.Completed(_logger, operation, outcome, tenantId, principalId, agentId, sessionId, runId, scopeId, reservationId, operationId));
            RecordMetrics(operation, outcome, started);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryObserve(() =>
            {
                _ = activityScope?.Activity?.SetTag(AgentKitTagNames.Outcome, "cancelled");
                _ = activityScope?.Activity?.SetTag(AgentKitTagNames.ErrorType, nameof(OperationCanceledException));
                _ = (activityScope?.Activity?.SetStatus(ActivityStatusCode.Error, "cancellation"));
            });
            TryObserve(() => BudgetLedgerLog.Completed(_logger, operation, "cancelled", tenantId, principalId, agentId, sessionId, runId, scopeId, reservationId, operationId));
            RecordMetrics(operation, "cancelled", started);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            TryObserve(() =>
            {
                _ = activityScope?.Activity?.SetTag(AgentKitTagNames.Outcome, "faulted");
                _ = activityScope?.Activity?.SetTag(AgentKitTagNames.ErrorType, errorType);
                _ = (activityScope?.Activity?.SetStatus(ActivityStatusCode.Error, errorType));
            });
            TryObserve(() => BudgetLedgerLog.Failed(_logger, operation, errorType, tenantId, principalId, agentId, sessionId, runId, scopeId, reservationId, operationId));
            RecordMetrics(operation, "faulted", started);
            throw;
        }
        finally
        {
            TryObserve(() => activityScope?.Dispose());
        }
    }

    private void RecordMetrics(string operation, string outcome, long? started)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "The observer supplies a bounded operation name.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "The observer supplies a bounded terminal outcome.");
        TryObserve(() => BudgetLedgerMetrics.RecordCount(operation, outcome));
        if (started is { } timestamp)
        {
            TryObserve(() =>
            {
                var ended = _timeProvider.GetTimestamp();
                if (ended >= timestamp)
                {
                    BudgetLedgerMetrics.RecordDuration(operation, outcome, _timeProvider.GetElapsedTime(timestamp, ended));
                }
            });
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
            { AgentKitTagNames.AgentId, address.AgentId.ToString() }
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

    private static string? Tag(ActivityTagsCollection tags, string name)
    {
        Debug.Assert(tags is not null, "The observer creates a diagnostic tag collection before lookup.");
        Debug.Assert(!string.IsNullOrWhiteSpace(name), "The observer looks up a shared nonblank tag name.");
        return tags.FirstOrDefault(tag => tag.Key == name).Value?.ToString();
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

    private static void TryObserve(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try
        {
            observation();
        }
        catch
        {
        }
    }

    private ScopeState GetScope(BudgetLedgerScopeReference reference)
    {
        Debug.Assert(reference is not null, "The caller supplies a validated exact scope reference.");
        return _scopes.TryGetValue(reference.Id, out var scope) && scope.Reference == reference
            ? scope
            : throw Missing("The scope reference is unavailable.");
    }

    private ReservationState GetReservation(BudgetLedgerReservationReference reference)
    {
        Debug.Assert(reference is not null, "The caller supplies a validated exact reservation reference.");
        return _reservations.TryGetValue(reference.Id, out var reservation) && reservation.Receipt.Reservation == reference
            ? reservation
            : throw Missing("The reservation reference is unavailable.");
    }

    private static ImmutableArray<ScopeState> Lineage(ScopeState scope)
    {
        Debug.Assert(scope is not null, "The caller resolves the target scope before traversing its lineage.");
        var builder = ImmutableArray.CreateBuilder<ScopeState>();
        for (var current = scope; current is not null; current = current.Parent)
        {
            builder.Add(current);
        }
        return builder.ToImmutable();
    }

    private HashSet<ReservationState> FindExpired(DateTimeOffset now) =>
        [.. _reservations.Values.Where(r => r.StartedAt is null && r.Commit is null && !r.Released && now >= r.Receipt.EffectiveReservation.ExpiresAt)];

    private void Release(ReservationState state)
    {
        Debug.Assert(state is not null, "The caller resolves reservation state before release.");
        if (state.Released)
        {
            return;
        }
        state.Released = true;
        _ = AdvanceRevision();
    }

    private BudgetStartExpired Expire(ReservationState state)
    {
        Debug.Assert(state is not null, "The caller resolves expired reservation state before cleanup.");
        Debug.Assert(state.StartedAt is null && state.Commit is null, "Only unresolved unstarted reservations expire.");
        state.StartExpiration ??= new BudgetStartExpired(
            state.Receipt.Reservation.Id,
            state.Receipt.EffectiveReservation.ExpiresAt);
        Release(state);
        return state.StartExpiration;
    }

    private BudgetLedgerReconciliationReleased ReleaseForReconciliation(ReservationState state)
    {
        Debug.Assert(state is not null, "The reconciliation path resolves reservation state before release.");
        Release(state);
        return new BudgetLedgerReconciliationReleased(state.Receipt.Reservation);
    }

    private BudgetCommitResult Settle(ReservationState state, decimal actual, CancellationToken cancellationToken)
    {
        Debug.Assert(state is not null, "The caller resolves reservation state before settlement.");
        Debug.Assert(actual >= 0, "The settlement request validates nonnegative actual usage.");
        if (state.OriginalCommit is not null)
        {
            return state.OriginalCommit.Actual != actual
                ? throw Conflict("The reservation is settled with different actual usage.")
                : state.OriginalCommit;
        }
        if (state.StartedAt is null || state.Released)
        {
            throw new BudgetLedgerStateException("The reservation is not started or is released.");
        }
        EnsureRevisionCapacity(1);
        var accountingRevision = new BudgetAccountingRevision(checked(_revision + 1));
        var createdStates = ImmutableArray.CreateBuilder<(ScopeState Boundary, OverrunHoldState Hold)>();
        if (actual > state.Receipt.OriginalRequest.Amount)
        {
            foreach (var boundary in state.Lineage)
            {
                var evidence = HoldEvidence(boundary, state, accountingRevision, actual);
                createdStates.Add((boundary, new OverrunHoldState(evidence)));
            }
        }
        var createdEvidence = createdStates.Select(item => item.Hold.Evidence).ToImmutableArray();
        var commit = Commit(state.Receipt.Reservation.Id, state.Receipt.OriginalRequest.Amount, actual, accountingRevision, createdEvidence);
        cancellationToken.ThrowIfCancellationRequested();
        state.Commit = commit;
        state.OriginalCommit = state.Commit;
        state.AccountingRevision = accountingRevision;
        foreach (var (Boundary, Hold) in createdStates)
        {
            Boundary.OverrunHolds.Add(Hold);
        }
        _ = AdvanceRevision();
        return state.Commit;
    }

    private static BudgetCommitResult Commit(BudgetReservationId id, decimal reserved, decimal actual, BudgetAccountingRevision accountingRevision, ImmutableArray<BudgetOverrunHold> createdHolds)
    {
        Debug.Assert(id != default, "The persisted reservation supplies a nondefault identity.");
        Debug.Assert(reserved >= 0, "The original reservation request validates nonnegative capacity.");
        Debug.Assert(actual >= 0, "The settlement request validates nonnegative actual usage.");
        return new(id, reserved, actual, Math.Max(0, reserved - actual), Math.Max(0, actual - reserved), accountingRevision, createdHolds);
    }

    private long AdvanceRevision() => _revision = checked(_revision + 1);

    private void EnsureRevisionCapacity(int transitions)
    {
        Debug.Assert(transitions >= 0, "The caller preflights a nonnegative transition count.");
        _ = checked(_revision + transitions);
    }
    private static (BudgetQuantity Reserved, BudgetQuantity Committed) Usage(
        ScopeState scope,
        BudgetDimension dimension,
        HashSet<ReservationState>? excluded = null)
    {
        Debug.Assert(scope is not null, "The caller resolves scope state before aggregating usage.");
        Debug.Assert(dimension != default, "The caller supplies a validated budget dimension.");
        var reservations = scope.Reservations.Where(r => r.Receipt.OriginalRequest.Dimension == dimension).ToArray();
        var retained = reservations.Where(r => !r.Released && r.Commit is null && excluded?.Contains(r) != true).ToArray();
        var aggregation = reservations.FirstOrDefault()?.Aggregation ?? BudgetAggregationKind.Sum;
        var reserved = aggregation switch
        {
            BudgetAggregationKind.Maximum => retained.Length == 0 ? default : BudgetQuantity.FromDecimal(retained.Max(item => item.Receipt.OriginalRequest.Amount)),
            BudgetAggregationKind.ConcurrentGauge or BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => Sum(retained.Select(item => item.Receipt.OriginalRequest.Amount)),
            _ => throw new BudgetLedgerStateException("The retained accounting has an unsupported aggregation kind."),
        };
        var settled = reservations.Where(r => r.Commit is not null).Select(r => r.Commit!.Actual).ToArray();
        var committed = aggregation switch
        {
            BudgetAggregationKind.Maximum => settled.Length == 0 ? default : BudgetQuantity.FromDecimal(settled.Max()),
            BudgetAggregationKind.ConcurrentGauge => default,
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => Sum(settled),
            _ => throw new BudgetLedgerStateException("The committed accounting has an unsupported aggregation kind."),
        };
        return (reserved, committed);
    }

    private static BudgetLedgerScopeCreateRejected Rejected(BudgetScopeCreationFailureKind kind, string message)
    {
        Debug.Assert(Enum.IsDefined(kind), "The caller supplies a defined scope failure kind.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "The caller supplies a content-free safe message.");
        return new(new BudgetScopeCreationFailed(kind, message));
    }

    private static BudgetLedgerBatchReserveRejected Reject(ScopeState scope, BudgetReservationRequest item, decimal limit, BudgetQuantity observed, BudgetQuantity requested, string? unit = null)
    {
        Debug.Assert(scope is not null, "The caller resolves the rejecting scope boundary.");
        Debug.Assert(item is not null, "The caller selects one validated batch item for dimension metadata.");
        Debug.Assert(limit >= 0, "Captured limits are nonnegative.");
        return new(new BudgetLimitFailure(scope.Reference.Id, item.Dimension, BudgetLimitKind.Hard, limit, observed, requested, unit is null ? item.Unit : new BudgetUnit(unit), "The atomic batch would exceed a captured hard boundary."));
    }

    private static BudgetQuantity Sum(IEnumerable<decimal> values)
    {
        Debug.Assert(values is not null, "The caller supplies a non-null sequence of validated quantities.");
        return values.Aggregate(default(BudgetQuantity), static (total, value) => total.Add(BudgetQuantity.FromDecimal(value)));
    }
    private static BudgetQuantity Max(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) >= 0 ? left : right;

    private static BudgetOverrunHold HoldEvidence(ScopeState boundary, ReservationState reservation, BudgetAccountingRevision revision, decimal actual)
    {
        Debug.Assert(boundary is not null, "The caller resolves the charged boundary.");
        Debug.Assert(reservation is not null, "The caller resolves the triggering reservation.");
        Debug.Assert(actual > reservation.Receipt.OriginalRequest.Amount, "A hold is created only for a current row overrun.");
        var original = reservation.Receipt.OriginalRequest;
        return new BudgetOverrunHold(
            new BudgetOverrunHoldReference(boundary.Reference, reservation.Receipt.Reservation, revision),
            original.Dimension,
            original.Unit,
            original.Amount,
            actual,
            boundary.Request.Admission.OverrunHoldPolicy);
    }

    private ImmutableArray<BudgetOverrunHold> ActiveHoldEvidence(ScopeState boundary)
    {
        Debug.Assert(boundary is not null, "The caller resolves a charged boundary.");
        return [.. boundary.OverrunHolds
            .Where(hold => hold.IsActive)
            .Select(hold => CurrentEvidence(hold.Evidence))
            .OrderBy(hold => hold.Reference.TriggeringRevision.Value)];
    }

    private BudgetOverrunHold CurrentEvidence(BudgetOverrunHold evidence)
    {
        Debug.Assert(evidence is not null, "Persisted hold evidence is non-null.");
        var state = _reservations[evidence.Reference.Reservation.Id];
        return new BudgetOverrunHold(evidence.Reference, evidence.Dimension, evidence.Unit, evidence.Reserved, state.Commit?.Actual ?? evidence.CurrentActual, evidence.Policy);
    }

    private ImmutableArray<BudgetOverrunHold> CurrentOverruns(ScopeState boundary, BudgetDimension dimension) =>
        [.. boundary.OverrunHolds
            .Where(hold => hold.IsActive && hold.Evidence.Dimension == dimension)
            .Select(hold => CurrentEvidence(hold.Evidence))
            .Where(hold => hold.CurrentActual > hold.Reserved)];

    private static bool EligibleForClear(ScopeState boundary, ReservationState correcting, decimal correctedActual)
    {
        Debug.Assert(boundary is not null, "The correction supplies a charged boundary.");
        Debug.Assert(correcting is not null, "The correction supplies settled reservation state.");
        var original = correcting.Receipt.OriginalRequest;
        if (boundary.Reservations.Any(item =>
            item.Commit is not null
            && item.Receipt.OriginalRequest.Dimension == original.Dimension
            && (ReferenceEquals(item, correcting) ? correctedActual : item.Commit.Actual) > item.Receipt.OriginalRequest.Amount))
        {
            return false;
        }
        var hard = boundary.Request.OriginalRequest.Limits.FirstOrDefault(limit => limit.Dimension == original.Dimension && limit.Kind == BudgetLimitKind.Hard);
        if (hard is null)
        {
            return true;
        }
        var settledValues = boundary.Reservations
            .Where(item => item.Commit is not null && item.Receipt.OriginalRequest.Dimension == original.Dimension)
            .Select(item => ReferenceEquals(item, correcting) ? correctedActual : item.Commit!.Actual)
            .ToArray();
        var retainedValues = boundary.Reservations
            .Where(item => item.IsCapacityRetaining && item.Receipt.OriginalRequest.Dimension == original.Dimension)
            .Select(item => item.Receipt.OriginalRequest.Amount)
            .ToArray();
        var aggregation = correcting.Aggregation;
        var observed = aggregation switch
        {
            BudgetAggregationKind.Maximum => Max(
                retainedValues.Length == 0 ? default : BudgetQuantity.FromDecimal(retainedValues.Max()),
                settledValues.Length == 0 ? default : BudgetQuantity.FromDecimal(settledValues.Max())),
            BudgetAggregationKind.ConcurrentGauge => Sum(retainedValues),
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => Sum(retainedValues).Add(Sum(settledValues)),
            _ => throw new BudgetLedgerStateException("The corrected accounting has unsupported aggregation semantics."),
        };
        return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0;
    }

    private static ImmutableArray<BudgetLimitFailure> CurrentHardFailures(ScopeState boundary, BudgetDimension dimension)
    {
        Debug.Assert(boundary is not null, "The caller resolves the hold-owning boundary.");
        var hard = boundary.Request.OriginalRequest.Limits.FirstOrDefault(limit => limit.Dimension == dimension && limit.Kind == BudgetLimitKind.Hard);
        if (hard is null)
        {
            return [];
        }
        var (reserved, committed) = Usage(boundary, dimension);
        var aggregation = boundary.Reservations.First(item => item.Receipt.OriginalRequest.Dimension == dimension).Aggregation;
        var observed = aggregation switch
        {
            BudgetAggregationKind.Maximum => Max(reserved, committed),
            BudgetAggregationKind.ConcurrentGauge => reserved,
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => reserved.Add(committed),
            _ => throw new BudgetLedgerStateException("The hold accounting has unsupported aggregation semantics."),
        };
        return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0
            ? []
            : [new BudgetLimitFailure(boundary.Reference.Id, dimension, BudgetLimitKind.Hard, hard.Value, observed, default, hard.Unit, "Current accounting exceeds the captured hard boundary.")];
    }

    private static bool IsParentAddress(BudgetScopeAddress parent, BudgetScopeAddress child)
    {
        Debug.Assert(parent is not null, "The caller resolves a persisted parent address.");
        Debug.Assert(child is not null, "The validated scope request supplies a child address.");
        return parent.TenantId == child.TenantId
            && parent.PrincipalId == child.PrincipalId
            && parent.AgentId == child.AgentId
            && (parent.SessionId is null || parent.SessionId == child.SessionId)
            && (parent.RunId is null || parent.RunId == child.RunId)
            && (parent.OperationId is null || parent.OperationId == child.OperationId);
    }

    private static BudgetLedgerMutationConflictException Conflict(string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "The caller supplies a content-free conflict message.");
        return new(message);
    }

    private static BudgetLedgerReferenceUnavailableException Missing(string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "The caller supplies a content-free unavailable-reference message.");
        return new(message);
    }
}
