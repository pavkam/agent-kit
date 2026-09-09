// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;

/// <summary>Creates deterministic isolated in-memory ledgers for the shared suite.</summary>
public sealed class InMemoryBudgetLedgerConformanceFixture: IBudgetLedgerConformanceFixture
{
    private readonly ControllableTimeProvider _timeProvider = new();
    private readonly SequentialScopeIdGenerator _scopeIds = new();
    private readonly SequentialReservationIdGenerator _reservationIds = new();
    private readonly TestDimensionCatalog _catalog = new();

    /// <inheritdoc/>
    public IBudgetLedger CreateLedger() => new InMemoryBudgetLedger(
        _timeProvider,
        _scopeIds,
        _reservationIds,
        _catalog);

    /// <summary>Creates a fresh ledger using the supplied diagnostic logger.</summary>
    /// <param name="logger">The logger used to observe or inject diagnostic failures.</param>
    /// <returns>A ledger sharing this fixture's deterministic collaborators.</returns>
    internal IBudgetLedger CreateLedger(ILogger<InMemoryBudgetLedger> logger) => new InMemoryBudgetLedger(
        _timeProvider,
        _scopeIds,
        _reservationIds,
        _catalog,
        logger);

    /// <summary>Arms the next diagnostic timestamp read to fail without affecting semantic clock reads.</summary>
    internal void ArmTimestampFailure() => _timeProvider.ArmTimestampFailure();

    /// <summary>Arms diagnostic timestamps to move backwards without changing semantic wall-clock time.</summary>
    internal void ArmNegativeElapsedTime() => _timeProvider.ArmNegativeElapsedTime();

    /// <inheritdoc/>
    public void Advance(TimeSpan delta) => _timeProvider.Advance(delta);

    /// <inheritdoc/>
    public void ArmClockFailure() => _timeProvider.ArmFailure();

    /// <inheritdoc/>
    public void ArmCatalogFailure() => _catalog.ArmFailure();

    /// <inheritdoc/>
    public void ArmSecondReservationIdFailure() => _reservationIds.ArmSecondFailure();

    /// <inheritdoc/>
    public void ArmSecondReservationIdCancellation(CancellationTokenSource source) => _reservationIds.ArmSecondCancellation(source);

    /// <inheritdoc/>
    public void ArmScopeIdCancellation(CancellationTokenSource source) => _scopeIds.ArmCancellation(source);

    private sealed class SequentialScopeIdGenerator: IIdentifierGenerator<BudgetScopeId>
    {
        private int _value;
        private CancellationTokenSource? _cancellation;
        public BudgetScopeId Create()
        {
            _cancellation?.Cancel();
            _cancellation = null;
            return new(Guid.Parse($"10000000-0000-0000-0000-{++_value:000000000000}"));
        }

        internal void ArmCancellation(CancellationTokenSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            _cancellation = source;
        }
    }

    private sealed class SequentialReservationIdGenerator: IIdentifierGenerator<BudgetReservationId>
    {
        private int _value;
        private int _armedCount;
        private bool _throw;
        private CancellationTokenSource? _cancellation;

        public BudgetReservationId Create()
        {
            if (_throw || _cancellation is not null)
            {
                _armedCount++;
                if (_armedCount == 2)
                {
                    _cancellation?.Cancel();
                    if (_throw)
                    {
                        throw new InvalidOperationException("Injected reservation identity failure.");
                    }
                }
            }
            return new(Guid.Parse($"20000000-0000-0000-0000-{++_value:000000000000}"));
        }

        internal void ArmSecondFailure()
        {
            _armedCount = 0;
            _throw = true;
            _cancellation = null;
        }

        internal void ArmSecondCancellation(CancellationTokenSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            _armedCount = 0;
            _throw = false;
            _cancellation = source;
        }
    }

    private sealed class TestDimensionCatalog: IBudgetDimensionCatalog
    {
        private static readonly ImmutableArray<BudgetDimensionDescriptor> Descriptors =
        [
            new(new BudgetDimension("test.sum"), BudgetAggregationKind.Sum, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.maximum"), BudgetAggregationKind.Maximum, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.gauge"), BudgetAggregationKind.ConcurrentGauge, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.duration"), BudgetAggregationKind.Duration, [new BudgetUnit("seconds")]),
            new(new BudgetDimension("test.unlimited"), BudgetAggregationKind.Sum, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.multi-unit"), BudgetAggregationKind.Sum, [new BudgetUnit("count"), new BudgetUnit("bytes")]),
        ];
        private bool _throw;

        /// <inheritdoc/>
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            if (_throw)
            {
                _throw = false;
                throw new InvalidOperationException("Injected dimension catalog failure.");
            }
            descriptor = Descriptors.FirstOrDefault(item => item.Dimension == dimension);
            return descriptor is not null;
        }

        internal void ArmFailure() => _throw = true;
    }

    private sealed class ControllableTimeProvider: TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;
        private bool _throw;
        private bool _throwTimestamp;
        private int _backwardTimestampReads;

        public override DateTimeOffset GetUtcNow()
        {
            if (_throw)
            {
                _throw = false;
                throw new InvalidOperationException("Injected ledger clock failure.");
            }
            return _utcNow;
        }

        public override long GetTimestamp()
        {
            if (_throwTimestamp)
            {
                _throwTimestamp = false;
                throw new InvalidOperationException("Injected diagnostic timestamp failure.");
            }
            return _backwardTimestampReads > 0 ? _backwardTimestampReads-- == 2 ? 100 : 50 : base.GetTimestamp();
        }

        internal void Advance(TimeSpan delta)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delta, TimeSpan.Zero);
            _utcNow += delta;
        }

        internal void ArmFailure() => _throw = true;

        internal void ArmTimestampFailure() => _throwTimestamp = true;

        internal void ArmNegativeElapsedTime() => _backwardTimestampReads = 2;
    }
}
