// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Creates deterministic isolated in-memory ledgers for the shared suite.</summary>
public sealed class SqliteBudgetLedgerConformanceFixture: IBudgetLedgerConformanceFixture
{
    private readonly ILogger<SqliteBudgetLedger>? _logger;
    private readonly string? _targetSentinel;

    /// <summary>Creates a deterministic fixture without a diagnostics sink.</summary>
    public SqliteBudgetLedgerConformanceFixture() { }

    /// <summary>Creates a deterministic fixture with a diagnostics sink.</summary>
    /// <param name="logger">The non-null logger supplied to created adapters.</param>
    /// <param name="targetSentinel">Optional safe test text embedded only in the protected target path.</param>
    internal SqliteBudgetLedgerConformanceFixture(ILogger<SqliteBudgetLedger> logger, string? targetSentinel = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _targetSentinel = targetSentinel;
    }
    /// <inheritdoc/>
    public BudgetLedgerDescriptor ExpectedDescriptor { get; } = new(
        true, BudgetLedgerConcurrencyDomain.HostLocal);

    private readonly ControllableTimeProvider _timeProvider = new();
    private readonly SequentialScopeIdGenerator _scopeIds = new();
    private readonly SequentialReservationIdGenerator _reservationIds = new();
    private readonly TestDimensionCatalog _catalog = new();

    /// <inheritdoc/>
    public IBudgetLedger CreateLedger() => CreateLedgerWithSettings(SqliteBudgetLedgerSettings.CreateDefault());

    /// <summary>Creates a fresh ledger over an isolated target using explicitly configured settings.</summary>
    /// <param name="settings">The immutable transaction and codec bounds to apply.</param>
    /// <returns>A fully initialized ledger sharing this fixture's deterministic collaborators.</returns>
    internal IBudgetLedger CreateLedgerWithSettings(SqliteBudgetLedgerSettings settings)
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }
        var directory = Path.Combine(root, $"agentkit-budget-conformance-{_targetSentinel}{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var ledger = new SqliteBudgetLedger(
            new SqliteBudgetLedgerTarget(Path.Combine(directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            settings, _timeProvider, _scopeIds, _reservationIds, _catalog, _logger);
        ledger.InitializeAsync().AsTask().GetAwaiter().GetResult();
        return ledger;
    }

    /// <inheritdoc/>
    public void Advance(TimeSpan delta) => _timeProvider.Advance(delta);

    /// <inheritdoc/>
    public void ArmClockFailure() => _timeProvider.ArmFailure();

    /// <inheritdoc/>
    public void ArmCatalogFailure() => _catalog.ArmFailure();

    internal void ArmCatalogFailure(string message) => _catalog.ArmFailure(message);

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
        private string _failureMessage = "Injected dimension catalog failure.";

        /// <inheritdoc/>
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            if (_throw)
            {
                _throw = false;
                throw new InvalidOperationException(_failureMessage);
            }
            descriptor = Descriptors.FirstOrDefault(item => item.Dimension == dimension);
            return descriptor is not null;
        }

        internal void ArmFailure() => _throw = true;

        internal void ArmFailure(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            _failureMessage = message;
            _throw = true;
        }
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
