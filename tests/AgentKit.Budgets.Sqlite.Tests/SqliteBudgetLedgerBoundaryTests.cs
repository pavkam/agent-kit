// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies public SQLite ledger argument boundaries before target access.</summary>
public sealed class SqliteBudgetLedgerBoundaryTests
{
    /// <summary>Proves every required constructor dependency is attributed exactly and an omitted logger is accepted.</summary>
    [Fact]
    public void Constructor_WhenRequiredDependencyIsNull_ThrowsExactParameter()
    {
        var target = new SqliteBudgetLedgerTarget("/missing/ledger.db", new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var clock = TimeProvider.System;
        var scopes = new ScopeIds();
        var reservations = new ReservationIds();
        var catalog = new Catalog();

        Should.Throw<ArgumentNullException>(() => new SqliteBudgetLedger(null!, settings, clock, scopes, reservations, catalog)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteBudgetLedger(target, null!, clock, scopes, reservations, catalog)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteBudgetLedger(target, settings, null!, scopes, reservations, catalog)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new SqliteBudgetLedger(target, settings, clock, null!, reservations, catalog)).ParamName.ShouldBe("scopeIds");
        Should.Throw<ArgumentNullException>(() => new SqliteBudgetLedger(target, settings, clock, scopes, null!, catalog)).ParamName.ShouldBe("reservationIds");
        Should.Throw<ArgumentNullException>(() => new SqliteBudgetLedger(target, settings, clock, scopes, reservations, null!)).ParamName.ShouldBe("dimensions");
        _ = Should.NotThrow(() => new SqliteBudgetLedger(target, settings, clock, scopes, reservations, catalog));
    }

    /// <summary>Proves fresh adapter cardinality rejection precedes catalogs, clocks, and reservation identity sources.</summary>
    [Fact]
    public async Task Operations_WhenFreshEvidenceExceedsAdapterBounds_RejectBeforeCollaborators()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }
        var directory = Path.Combine(root, $"agentkit-budget-boundary-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var defaults = SqliteBudgetLedgerSettings.CreateDefault();
            var settings = new SqliteBudgetLedgerSettings(defaults.LockTimeout, defaults.MaximumPayloadBytes,
                defaults.MaximumResultBytes, 1, 1, defaults.MaximumLineageDepth);
            var catalog = new ArmingCatalog();
            var reservations = new ArmingReservationIds();
            var ledger = new SqliteBudgetLedger(new(Path.Combine(directory, "ledger.db"), new(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), settings,
                TimeProvider.System, new ScopeIds(), reservations, catalog);
            var cancellationToken = TestContext.Current.CancellationToken;
            await ledger.InitializeAsync(cancellationToken);
            var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
            var oversizedScope = new BudgetLedgerScopeCreateRequest(new(null, address,
                [new(new("one"), 1, new("count"), BudgetLimitKind.Hard), new(new("two"), 1, new("count"), BudgetLimitKind.Hard)], new("oversized-scope")),
                new(8, 8, TimeSpan.FromMinutes(5)));

            _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.CreateScopeAsync(oversizedScope, cancellationToken));
            catalog.Calls.ShouldBe(0);
            var scope = (await ledger.CreateScopeAsync(new(new(null, address, [], new("scope")), new(8, 8, TimeSpan.FromMinutes(5))), cancellationToken))
                .ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
            catalog.Arm();
            reservations.Arm();
            var operation = new OperationId(Guid.NewGuid());
            var oversizedBatch = new BudgetLedgerBatchReserveRequest(scope,
                [new(scope.Id, new("one"), 1, new("count"), operation, null, new("one")),
                    new(scope.Id, new("one"), 1, new("count"), operation, null, new("two"))]);

            _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(oversizedBatch, cancellationToken));
            catalog.Calls.ShouldBe(0);
            reservations.Calls.ShouldBe(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ScopeIds: IIdentifierGenerator<BudgetScopeId> { public BudgetScopeId Create() => new(Guid.NewGuid()); }
    private sealed class ReservationIds: IIdentifierGenerator<BudgetReservationId> { public BudgetReservationId Create() => new(Guid.NewGuid()); }
    private sealed class Catalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        { descriptor = null; return false; }
    }

    private sealed class ArmingCatalog: IBudgetDimensionCatalog
    {
        private bool _armed;
        internal int Calls { get; private set; }
        internal void Arm() => _armed = true;
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            Calls++;
            if (_armed)
            {
                throw new InvalidOperationException("Catalog must not be called.");
            }
            descriptor = new(dimension, BudgetAggregationKind.Sum, [new("count")]);
            return true;
        }
    }

    private sealed class ArmingReservationIds: IIdentifierGenerator<BudgetReservationId>
    {
        private bool _armed;
        internal int Calls { get; private set; }
        internal void Arm() => _armed = true;
        public BudgetReservationId Create()
        {
            Calls++;
            return _armed ? throw new InvalidOperationException("Reservation IDs must not be called.") : new(Guid.NewGuid());
        }
    }
}
