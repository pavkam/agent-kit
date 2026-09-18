// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Creates deterministic isolated durable JSON ledgers for the shared budget-ledger suite.</summary>
/// <remarks>
/// Each created ledger gets its own canonical store root so cases never share durable accounting, while the clock,
/// identity generators, and dimension catalog stay shared so a case can arm a collaborator failure and observe it in the
/// next ledger call. The fixture retains every created root so a case can reopen one and assert recovery.
/// </remarks>
public sealed class JsonBudgetLedgerConformanceFixture: IBudgetLedgerConformanceFixture
{
    private readonly ControllableTimeProvider _timeProvider = new();
    private readonly SequentialScopeIdGenerator _scopeIds = new();
    private readonly SequentialReservationIdGenerator _reservationIds = new();
    private readonly TestDimensionCatalog _catalog = new();
    private readonly ILogger<JsonBudgetLedger>? _logger;

    /// <summary>Creates a deterministic fixture without a diagnostics sink.</summary>
    public JsonBudgetLedgerConformanceFixture() { }

    /// <summary>Creates a deterministic fixture that supplies a caller-owned diagnostics sink to every created ledger.</summary>
    /// <param name="logger">The non-null logger supplied to created adapters.</param>
    internal JsonBudgetLedgerConformanceFixture(ILogger<JsonBudgetLedger> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <value>Durable local accounting whose transitions linearize only inside the single writer process.</value>
    public BudgetLedgerDescriptor ExpectedDescriptor { get; } = new(
        durable: true, concurrencyDomain: BudgetLedgerConcurrencyDomain.ProcessLocal);

    /// <inheritdoc/>
    public IBudgetLedger CreateLedger() => Open(
        Path.Combine(TestTemporaryDirectory.Create(), "ledger"),
        new JsonBudgetLedgerInstanceId(Guid.NewGuid()),
        JsonStoreRecoveryMode.RecoverTornAppends);

    /// <summary>Opens or creates one ledger over an exact root using this fixture's deterministic collaborators.</summary>
    /// <param name="directoryPath">The fully qualified store root, which may already contain a journal.</param>
    /// <param name="storeInstanceId">The persistent store identity the manifest must carry.</param>
    /// <param name="recoveryMode">The torn-append policy applied during initialization.</param>
    /// <param name="openMode">Whether the root and its manifest may be created.</param>
    /// <param name="settings">The evidence bounds and encoding contract to apply, or null for <see cref="JsonBudgetLedgerSettings.CreateDefault"/>.</param>
    /// <returns>A fully initialized ledger the caller owns and must dispose.</returns>
    /// <remarks>A failed initialization disposes the partially opened ledger, so its advisory exclusive lock never blocks the next attempt against the same root.</remarks>
    /// <exception cref="ArgumentException"><paramref name="directoryPath"/> is null or blank.</exception>
    internal JsonBudgetLedger Open(
        string directoryPath,
        JsonBudgetLedgerInstanceId storeInstanceId,
        JsonStoreRecoveryMode recoveryMode,
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonBudgetLedgerSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var ledger = new JsonBudgetLedger(
            new JsonBudgetLedgerTarget(directoryPath, storeInstanceId, openMode, recoveryMode),
            settings ?? JsonBudgetLedgerSettings.CreateDefault(),
            _timeProvider,
            _scopeIds,
            _reservationIds,
            _catalog,
            _logger);
        try
        {
            ledger.InitializeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            ledger.Dispose();
            throw;
        }

        return ledger;
    }

    /// <inheritdoc/>
    public void Advance(TimeSpan delta) => _timeProvider.Advance(delta);

    /// <inheritdoc/>
    public void ArmClockFailure() => _timeProvider.ArmFailure();

    /// <inheritdoc/>
    public void ArmCatalogFailure() => _catalog.ArmFailure();

    /// <inheritdoc/>
    public void ArmSecondReservationIdFailure() => _reservationIds.ArmSecondFailure();

    /// <inheritdoc/>
    public void ArmSecondReservationIdCancellation(CancellationTokenSource source) =>
        _reservationIds.ArmSecondCancellation(source);

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
            return new BudgetScopeId(Guid.Parse($"10000000-0000-0000-0000-{++_value:000000000000}"));
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

            return new BudgetReservationId(Guid.Parse($"20000000-0000-0000-0000-{++_value:000000000000}"));
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
        private static readonly ImmutableArray<BudgetDimensionDescriptor> _descriptors =
        [
            new(new BudgetDimension("test.sum"), BudgetAggregationKind.Sum, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.maximum"), BudgetAggregationKind.Maximum, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.gauge"), BudgetAggregationKind.ConcurrentGauge, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.duration"), BudgetAggregationKind.Duration, [new BudgetUnit("seconds")]),
            new(new BudgetDimension("test.unlimited"), BudgetAggregationKind.Sum, [new BudgetUnit("count")]),
            new(new BudgetDimension("test.multi-unit"), BudgetAggregationKind.Sum,
                [new BudgetUnit("count"), new BudgetUnit("bytes")]),
        ];
        private bool _throw;

        public bool TryGet(
            BudgetDimension dimension,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            if (_throw)
            {
                _throw = false;
                throw new InvalidOperationException("Injected dimension catalog failure.");
            }

            descriptor = _descriptors.FirstOrDefault(item => item.Dimension == dimension);
            return descriptor is not null;
        }

        internal void ArmFailure() => _throw = true;
    }

    private sealed class ControllableTimeProvider: TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;
        private bool _throw;

        public override DateTimeOffset GetUtcNow()
        {
            if (_throw)
            {
                _throw = false;
                throw new InvalidOperationException("Injected ledger clock failure.");
            }

            return _utcNow;
        }

        internal void Advance(TimeSpan delta)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delta, TimeSpan.Zero);
            _utcNow += delta;
        }

        internal void ArmFailure() => _throw = true;
    }
}
