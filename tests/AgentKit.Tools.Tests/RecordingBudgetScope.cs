// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Immutable;

/// <summary>A deterministic budget scope that records every reservation's lifecycle and refuses chosen dimensions on demand.</summary>
internal sealed class RecordingBudgetScope: IBudgetScope
{
    private readonly Lock _gate = new();
    private readonly List<RecordedReservation> _reservations = [];

    /// <summary>Gets the scope identity.</summary>
    public BudgetScopeId Id { get; } = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    /// <summary>Gets the scope address.</summary>
    public BudgetScopeAddress Address { get; } = new(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
        new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
        new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444")));

    /// <summary>Gets the dimensions whose reservations are rejected.</summary>
    public HashSet<BudgetDimension> Refused { get; } = [];

    /// <summary>Gets or sets whether the ledger reports it cannot confirm any reservation.</summary>
    public bool Unavailable { get; set; }

    /// <summary>Gets a snapshot of every accepted reservation in order.</summary>
    public IReadOnlyList<RecordedReservation> Reservations
    {
        get
        {
            lock (_gate)
            {
                return [.. _reservations];
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<BudgetReservationResult> ReserveAsync(BudgetReservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new BudgetLedgerPersistenceUnavailableException("The ledger is unavailable.", acknowledgementUnknown: false);
        }

        if (Refused.Contains(request.Dimension))
        {
            return ValueTask.FromResult<BudgetReservationResult>(new BudgetRejected(new BudgetLimitFailure(
                Id,
                request.Dimension,
                BudgetLimitKind.Hard,
                configuredValue: 0,
                observedValue: BudgetQuantity.FromDecimal(0),
                requestedAmount: BudgetQuantity.FromDecimal(request.Amount),
                request.Unit,
                "The test scope refuses this dimension.")));
        }

        var recorded = new RecordedReservation(request);
        lock (_gate)
        {
            _reservations.Add(recorded);
        }

        return ValueTask.FromResult<BudgetReservationResult>(new BudgetReserved(recorded));
    }

    /// <inheritdoc/>
    public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <summary>Records one reservation's request and lifecycle.</summary>
    internal sealed class RecordedReservation: IBudgetReservation
    {
        internal RecordedReservation(BudgetReservationRequest request)
        {
            Request = request;
            Id = new BudgetReservationId(Guid.NewGuid());
        }

        /// <summary>Gets the request that created the reservation.</summary>
        public BudgetReservationRequest Request { get; }

        /// <summary>Gets whether the reservation was marked started.</summary>
        public bool Started { get; private set; }

        /// <summary>Gets the committed actual amount, or <see langword="null"/> while unsettled.</summary>
        public decimal? Committed { get; private set; }

        /// <summary>Gets whether the reservation was disposed without settling.</summary>
        public bool Released { get; private set; }

        /// <inheritdoc/>
        public BudgetReservationId Id { get; }

        /// <inheritdoc/>
        public BudgetScopeId ScopeId => Request.ScopeId;

        /// <inheritdoc/>
        public BudgetDimension Dimension => Request.Dimension;

        /// <inheritdoc/>
        public decimal Reserved => Request.Amount;

        /// <inheritdoc/>
        public ValueTask<BudgetStartResult> MarkStartedAsync(CancellationToken cancellationToken = default)
        {
            Started = true;
            return ValueTask.FromResult<BudgetStartResult>(new BudgetStarted(Id, wasAlreadyStarted: false));
        }

        /// <inheritdoc/>
        public ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default)
        {
            Committed = actual;
            return ValueTask.FromResult(new BudgetCommitResult(Id, Request.Amount, actual, 0, 0, new BudgetAccountingRevision(1), []));
        }

        /// <inheritdoc/>
        public ValueTask<BudgetCorrectionResult> CorrectAsync(decimal correctedActual, long revision, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Released = Committed is null;
            return ValueTask.CompletedTask;
        }
    }
}
