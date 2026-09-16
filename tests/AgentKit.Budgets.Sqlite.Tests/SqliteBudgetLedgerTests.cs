// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

/// <summary>Verifies SqliteBudgetLedger behavior and contracts.</summary>
public sealed class SqliteBudgetLedgerTests: BudgetLedgerConformanceTests<SqliteBudgetLedgerConformanceFixture>, IDisposable
{
    /// <summary>Creates the explicit existing target parent.</summary>
    public SqliteBudgetLedgerTests() => Directory.CreateDirectory(_directory);

    private readonly string _directory = CreateDirectoryPath();
    private static string CreateDirectoryPath()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        return Path.Combine(root, $"agentkit-budget-concurrency-{Guid.NewGuid():N}");
    }

    /// <summary>Proves two adapters cannot both consume one remaining hard-limit slot.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenTwoInstancesRaceForLastSlot_AdmitsExactlyOne()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var id = new SqliteBudgetLedgerInstanceId(Guid.NewGuid());
        var first = Create(new(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));
        var second = Create(new(path, id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var cancellationToken = TestContext.Current.CancellationToken;
        await first.InitializeAsync(cancellationToken);
        await second.InitializeAsync(cancellationToken);
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var scope = (await first.CreateScopeAsync(new(new(null, address, [new(new("test.sum"), 1, new("count"), BudgetLimitKind.Hard)], new("scope")), new(8, 8, TimeSpan.FromMinutes(5))), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var firstRequest = new BudgetLedgerBatchReserveRequest(scope, [new(scope.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("first"))]);
        var secondRequest = new BudgetLedgerBatchReserveRequest(scope, [new(scope.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("second"))]);
        var results = await Task.WhenAll(Task.Run(async () => await first.ReserveBatchAsync(firstRequest, cancellationToken), cancellationToken), Task.Run(async () => await second.ReserveBatchAsync(secondRequest, cancellationToken), cancellationToken));
        results.Count(static result => result is BudgetLedgerBatchReserved).ShouldBe(1);
        results.Count(static result => result is BudgetLedgerBatchReserveRejected).ShouldBe(1);
    }

    private static SqliteBudgetLedger Create(SqliteBudgetLedgerTarget target) => new(target, SqliteBudgetLedgerSettings.CreateDefault(), TimeProvider.System, new ScopeIds(), new ReservationIds(), new Catalog());
    private sealed class ScopeIds: IIdentifierGenerator<BudgetScopeId>
    {
        public BudgetScopeId Create() => new(Guid.NewGuid());
    }

    private sealed class ReservationIds: IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => new(Guid.NewGuid());
    }

    private sealed class Catalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = dimension == new BudgetDimension("test.sum") ? new(dimension, BudgetAggregationKind.Sum, [new("count")]) : null;
            return descriptor is not null;
        }
    }

    /// <summary>Proves every required constructor dependency is attributed exactly and an omitted logger is accepted.</summary>
    [Fact]
    public void Constructor_WhenRequiredDependencyIsNull_ThrowsExactParameter()
    {
        var target = new SqliteBudgetLedgerTarget("/missing/ledger.db", new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var clock = TimeProvider.System;
        var scopes = new ScopeIds();
        var reservations = new ReservationIds();
        var catalog = new CatalogSqliteBudgetLedgerBoundary();
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
            var settings = new SqliteBudgetLedgerSettings(defaults.LockTimeout, defaults.MaximumPayloadBytes, defaults.MaximumResultBytes, 1, 1, defaults.MaximumLineageDepth);
            var catalog = new ArmingCatalog();
            var reservations = new ArmingReservationIds();
            var ledger = new SqliteBudgetLedger(new(Path.Combine(directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), settings, TimeProvider.System, new ScopeIds(), reservations, catalog);
            var cancellationToken = TestContext.Current.CancellationToken;
            await ledger.InitializeAsync(cancellationToken);
            var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
            var oversizedScope = new BudgetLedgerScopeCreateRequest(new(null, address, [new(new("one"), 1, new("count"), BudgetLimitKind.Hard), new(new("two"), 1, new("count"), BudgetLimitKind.Hard)], new("oversized-scope")), new(8, 8, TimeSpan.FromMinutes(5)));
            _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.CreateScopeAsync(oversizedScope, cancellationToken));
            catalog.Calls.ShouldBe(0);
            var scope = (await ledger.CreateScopeAsync(new(new(null, address, [], new("scope")), new(8, 8, TimeSpan.FromMinutes(5))), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
            catalog.Arm();
            reservations.Arm();
            var operation = new OperationId(Guid.NewGuid());
            var oversizedBatch = new BudgetLedgerBatchReserveRequest(scope, [new(scope.Id, new("one"), 1, new("count"), operation, null, new("one")), new(scope.Id, new("one"), 1, new("count"), operation, null, new("two"))]);
            _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(oversizedBatch, cancellationToken));
            catalog.Calls.ShouldBe(0);
            reservations.Calls.ShouldBe(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CatalogSqliteBudgetLedgerBoundary: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }
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

    private readonly string _directorySqliteBudgetLedgerCreateScope = CreateDirectory();
    /// <summary>Proves exact replay survives process-local ledger replacement.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenLedgerRestarts_ReplaysOriginalReference()
    {
        var path = Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db");
        var target = new SqliteBudgetLedgerTarget(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var first = CreateLedger(target, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await first.InitializeAsync(cancellationToken);
        var request = Request("scope");
        var created = (await first.CreateScopeAsync(request, cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>();
        var restartedTarget = new SqliteBudgetLedgerTarget(path, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);
        var restarted = CreateLedger(restartedTarget, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.Parse("10000000-0000-0000-0000-000000000099")));
        await restarted.InitializeAsync(cancellationToken);
        (await restarted.CreateScopeAsync(request, cancellationToken)).ShouldBe(new BudgetLedgerScopeCreated(created.Scope));
    }

    /// <summary>Proves the adapter lineage bound rejects a child before creating an unreadable row.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenAdapterLineageBoundReached_RejectsChild()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var defaults = SqliteBudgetLedgerSettings.CreateDefault();
        var settings = new SqliteBudgetLedgerSettings(defaults.LockTimeout, defaults.MaximumPayloadBytes, defaults.MaximumResultBytes, defaults.MaximumBatchSize, defaults.MaximumLimitsPerScope, 1);
        var ledger = new SqliteBudgetLedger(target, settings, TimeProvider.System, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ReservationIds(), new CatalogSqliteBudgetLedgerCreateScope());
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var parent = (await ledger.CreateScopeAsync(Request("parent"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var template = Request("child");
        var childRequest = new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(parent.Id, template.OriginalRequest.Address, template.OriginalRequest.Limits, template.OriginalRequest.IdempotencyKey), template.Admission);
        var result = await ledger.CreateScopeAsync(childRequest, cancellationToken);
        result.ShouldBeOfType<BudgetLedgerScopeCreateRejected>().Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.MaximumDepthExceeded);
    }

    /// <summary>Proves an atomic reservation and its effective expiry replay after ledger replacement.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerRestarts_ReplaysOriginalReceipts()
    {
        var path = Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db");
        var target = new SqliteBudgetLedgerTarget(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var first = CreateLedger(target, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await first.InitializeAsync(cancellationToken);
        var scope = (await first.CreateScopeAsync(Request("scope"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new("test.sum"), 2, new("count"), new(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, new("reserve"));
        var request = new BudgetLedgerBatchReserveRequest(scope, [item]);
        var accepted = (await first.ReserveBatchAsync(request, cancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        var restarted = CreateLedger(new(path, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.NewGuid()));
        await restarted.InitializeAsync(cancellationToken);
        (await restarted.ReserveBatchAsync(request, cancellationToken)).ShouldBe(accepted);
    }

    /// <summary>Proves started state survives replacement and cannot be released as unstarted capacity.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenLedgerRestarts_RetainsUnknownReservation()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        (await ledger.MarkStartedAsync(reservation, cancellationToken)).ShouldBe(new BudgetStarted(reservation.Id, false));
        var restarted = CreateLedger(new(target.DatabasePath, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.NewGuid()));
        await restarted.InitializeAsync(cancellationToken);
        (await restarted.MarkStartedAsync(reservation, cancellationToken)).ShouldBe(new BudgetStarted(reservation.Id, true));
        (await restarted.ReleaseUnstartedAsync(reservation, cancellationToken)).ShouldBe(new BudgetLedgerRetainedStarted(reservation));
    }

    /// <summary>Proves an abruptly terminated helper process leaves started unknown usage recoverable by a new process-local adapter.</summary>
    [Fact]
    public async Task ReadUnresolvedStartedAsync_WhenWriterProcessIsKilled_RetainsStartedUnknownReservation()
    {
        var path = Path.Combine(_directorySqliteBudgetLedgerCreateScope, "process-loss.db");
        var readyPath = Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ready");
        var storeId = new SqliteBudgetLedgerInstanceId(Guid.NewGuid());
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var framework = output.Name;
        var configuration = output.Parent!.Name;
        var host = Path.GetFullPath($"../../../../AgentKit.Budgets.Sqlite.ProcessHost/bin/{configuration}/{framework}/AgentKit.Budgets.Sqlite.ProcessHost.dll", AppContext.BaseDirectory);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
        };
        start.ArgumentList.Add(host);
        start.ArgumentList.Add(path);
        start.ArgumentList.Add(storeId.Value.ToString("D"));
        start.ArgumentList.Add(readyPath);
        Process? process = null;
        try
        {
            process = Process.Start(start).ShouldNotBeNull();
            var cancellationToken = TestContext.Current.CancellationToken;
            for (var attempt = 0; attempt < 100 && !File.Exists(readyPath); attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            File.ReadAllText(readyPath).ShouldBe("READY");
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
            var ledger = CreateLedger(new(path, storeId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.NewGuid()));
            await ledger.InitializeAsync(cancellationToken);
            var address = new BudgetScopeAddress(new("process-tenant"), new("process-principal"), new(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
            var scope = new BudgetLedgerScopeReference(new(Guid.Parse("30000000-0000-0000-0000-000000000001")), address);
            var page = await ledger.ReadUnresolvedStartedAsync(new(scope, 8, null), cancellationToken);
            page.Items.Single().Receipt.Reservation.Id.ShouldBe(new BudgetReservationId(Guid.Parse("40000000-0000-0000-0000-000000000001")));
        }
        finally
        {
            if (process is not null)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanup.Token);
                process.Dispose();
            }
        }
    }

    /// <summary>Proves settlement and its hold generation replay exactly after replacement.</summary>
    [Fact]
    public async Task SettleAsync_WhenLedgerRestarts_ReplaysOriginalCommit()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        _ = await ledger.MarkStartedAsync(reservation, cancellationToken);
        var request = new BudgetLedgerSettlementRequest(reservation, 12);
        var committed = await ledger.SettleAsync(request, cancellationToken);
        var restarted = CreateLedger(new(target.DatabasePath, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.NewGuid()));
        await restarted.InitializeAsync(cancellationToken);
        (await restarted.SettleAsync(request, cancellationToken)).ShouldBe(committed);
        committed.CreatedOverrunHolds.Length.ShouldBe(1);
    }

    /// <summary>Proves snapshots use exact indexed projections without decoding settled lifetime history.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenSettledHistoryBlobIsCorrupt_UsesIndexedAccountingProjection()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        _ = await ledger.MarkStartedAsync(reservation, cancellationToken);
        _ = await ledger.SettleAsync(new(reservation, 2), cancellationToken);
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={target.DatabasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE budget_reservations SET receipt=X'01',receipt_digest=zeroblob(32) WHERE reservation_id=$id;";
            _ = command.Parameters.AddWithValue("$id", reservation.Id.Value.ToByteArray());
            _ = command.ExecuteNonQuery();
        }

        var snapshot = await ledger.GetSnapshotAsync(reservation.Scope, cancellationToken);
        snapshot.Usages.Single().Committed.ShouldBe(BudgetQuantity.FromDecimal(2));
    }

    /// <summary>Proves corrupt cyclic ancestry fails as storage unavailability instead of looping.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenPersistedLineageIsCyclic_ThrowsPersistenceUnavailable()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var scope = (await ledger.CreateScopeAsync(Request("cycle"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        Execute(target.DatabasePath, "UPDATE budget_scopes SET parent_scope_id=scope_id,depth=2;");
        var item = new BudgetReservationRequest(scope.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("cycle-item"));
        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await ledger.ReserveBatchAsync(new(scope, [item]), cancellationToken));
        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves a persisted child whose immutable parent row is missing fails closed as storage corruption.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenPersistedParentIsMissing_ThrowsPersistenceUnavailable()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new IncrementingScopeIds());
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var parent = (await ledger.CreateScopeAsync(Request("missing-parent"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var template = Request("orphan-child");
        var childRequest = new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(parent.Id, template.OriginalRequest.Address, template.OriginalRequest.Limits, template.OriginalRequest.IdempotencyKey), template.Admission);
        var child = (await ledger.CreateScopeAsync(childRequest, cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        Execute(target.DatabasePath, $"PRAGMA foreign_keys=OFF; DELETE FROM budget_scopes WHERE scope_id=X'{Convert.ToHexString(parent.Id.Value.ToByteArray())}';");
        var item = new BudgetReservationRequest(child.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("orphan-item"));
        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await ledger.ReserveBatchAsync(new(child, [item]), cancellationToken));
        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves invalid stored projection enums are normalized as persistence corruption.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenProjectionAggregationIsCorrupt_ThrowsPersistenceUnavailable()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        Execute(target.DatabasePath, "UPDATE budget_dimension_projections SET aggregation=999;");
        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await ledger.GetSnapshotAsync(reservation.Scope));
        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves invalid persisted timestamp columns are normalized without escaping storage exceptions.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenStartedOffsetIsCorrupt_ThrowsPersistenceUnavailable()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        Execute(target.DatabasePath, "UPDATE budget_reservations SET started_offset_ticks=9223372036854775807;");
        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await ledger.GetSnapshotAsync(reservation.Scope));
        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves maximum text and sortable-key disagreement is rejected as persisted corruption.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenMaximumTextDisagreesWithKey_ThrowsPersistenceUnavailable()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.NewGuid()));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var template = Request("maximum-corruption");
        var scope = (await ledger.CreateScopeAsync(new(new(null, template.OriginalRequest.Address, [new(new("test.maximum"), 10, new("count"), BudgetLimitKind.Hard)], template.OriginalRequest.IdempotencyKey), template.Admission), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var operation = new OperationId(Guid.NewGuid());
        var receipts = (await ledger.ReserveBatchAsync(new(scope, [new(scope.Id, new("test.maximum"), 10, new("count"), operation, null, new("maximum-row")), new(scope.Id, new("test.maximum"), 8, new("count"), operation, null, new("lower-row"))]), cancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts;
        Execute(target.DatabasePath, "UPDATE budget_maximum_values SET amount_text='9' || char(0) || printf('%01000d',0) WHERE amount_text='10';");
        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await ledger.ReleaseUnstartedAsync(receipts[1].Reservation, cancellationToken));
        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    private async Task<(SqliteBudgetLedger Ledger, SqliteBudgetLedgerTarget Target, BudgetLedgerReservationReference Reservation)> CreateReservedAsync()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directorySqliteBudgetLedgerCreateScope, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new ScopeIdsSqliteBudgetLedgerCreateScope(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var scope = (await ledger.CreateScopeAsync(Request("scope"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new("test.sum"), 2, new("count"), new(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, new("reserve"));
        var reserved = (await ledger.ReserveBatchAsync(new(scope, [item]), cancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        return (ledger, target, reserved.Receipts[0].Reservation);
    }

    private static SqliteBudgetLedger CreateLedger(SqliteBudgetLedgerTarget target, IIdentifierGenerator<BudgetScopeId> scopes) => new(target, SqliteBudgetLedgerSettings.CreateDefault(), TimeProvider.System, scopes, new ReservationIds(), new CatalogSqliteBudgetLedgerCreateScope());
    private static BudgetLedgerScopeCreateRequest Request(string key) => new(new BudgetScopeRequest(null, new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, null, null), [new BudgetLimit(new BudgetDimension("test.sum"), 10, new BudgetUnit("count"), BudgetLimitKind.Hard)], new IdempotencyKey(key)), new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
    private static string CreateDirectory()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        var path = Path.Combine(root, $"agentkit-budget-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    private sealed class ScopeIdsSqliteBudgetLedgerCreateScope(Guid value): IIdentifierGenerator<BudgetScopeId>
    {
        public BudgetScopeId Create() => new(value);
    }

    private sealed class IncrementingScopeIds: IIdentifierGenerator<BudgetScopeId>
    {
        private int _value;
        public BudgetScopeId Create() => new(Guid.Parse($"10000000-0000-0000-0000-{++_value:000000000000}"));
    }

    private sealed class CatalogSqliteBudgetLedgerCreateScope: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = dimension == new BudgetDimension("test.sum") ? new(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("count")]) : dimension == new BudgetDimension("test.maximum") ? new(dimension, BudgetAggregationKind.Maximum, [new BudgetUnit("count")]) : null;
            return descriptor is not null;
        }
    }

    /// <summary>Proves caller argument guards run before activities or storage access.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenArgumentIsNull_EmitsNoActivity()
    {
        using var parent = new Activity("sqlite-invalid-test").Start();
        var started = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
                {
                    _ = Interlocked.Increment(ref started);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new SqliteBudgetLedgerConformanceFixture().CreateLedger();
        var before = Volatile.Read(ref started);
        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await ledger.GetSnapshotAsync(null!, TestContext.Current.CancellationToken));
        Volatile.Read(ref started).ShouldBe(before);
    }

    /// <summary>Proves cancellation and typed admission rejection use distinct truthful terminal evidence.</summary>
    [Fact]
    public async Task Operations_WhenCancelledOrRejected_EmitTruthfulErrorOutcomes()
    {
        using var parent = new Activity("sqlite-terminal-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateScopedListener(parent, stopped);
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var ledger = new SqliteBudgetLedgerConformanceFixture(logger).CreateLedger();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.ReadUnresolvedStartedAsync(new(new(new(Guid.NewGuid()), new(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null)), 1, null), cancellation.Token));
        var rejected = await ledger.CreateScopeAsync(CreateRequest("diagnostic-rejected", new("missing.dimension")), TestContext.Current.CancellationToken);
        _ = rejected.ShouldBeOfType<BudgetLedgerScopeCreateRejected>();
        var cancelled = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "cancelled");
        cancelled.Status.ShouldBe(ActivityStatusCode.Error);
        _ = cancelled.GetTagItem(AgentKitTagNames.ErrorType).ShouldNotBeNull();
        var rejection = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "rejected");
        rejection.Status.ShouldBe(ActivityStatusCode.Error);
        rejection.GetTagItem(AgentKitTagNames.ErrorType).ShouldBeNull();
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 7070 && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "cancelled")));
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 7070 && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "rejected")));
    }

    /// <summary>Proves persistence and collaborator faults retain normalized exception evidence.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenCatalogFails_EmitsFaultWithoutProtectedContent()
    {
        const string sentinel = "protected-key-sentinel";
        const string pathSentinel = "protected-path-sentinel";
        const string resourceSentinel = "protected-resource-sentinel";
        using var parent = new Activity("sqlite-fault-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateScopedListener(parent, stopped);
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name == AgentKitMetricNames.BudgetLedgerOperationCount)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                measurements.Enqueue(tags.ToArray());
            }
        });
        meterListener.Start();
        var fixture = new SqliteBudgetLedgerConformanceFixture(logger, pathSentinel);
        var ledger = fixture.CreateLedger();
        fixture.ArmCatalogFailure(resourceSentinel);
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await ledger.CreateScopeAsync(CreateRequest(sentinel, new("test.sum")), TestContext.Current.CancellationToken));
        var activity = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "faulted");
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        var errorType = activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldNotBeNull();
        errorType.ToString()!.ShouldContain(nameof(InvalidOperationException));
        activity.TagObjects.Any(item => item.Value is not null && (item.Value.ToString()!.Contains(sentinel, StringComparison.Ordinal) || item.Value.ToString()!.Contains(pathSentinel, StringComparison.Ordinal) || item.Value.ToString()!.Contains(resourceSentinel, StringComparison.Ordinal))).ShouldBeFalse();
        logger.Entries.SelectMany(static entry => entry.State).Any(item => item.Value is not null && (item.Value.ToString()!.Contains(sentinel, StringComparison.Ordinal) || item.Value.ToString()!.Contains(pathSentinel, StringComparison.Ordinal) || item.Value.ToString()!.Contains(resourceSentinel, StringComparison.Ordinal))).ShouldBeFalse();
        measurements.SelectMany(static tags => tags).Any(item => item.Value is not null && (item.Value.ToString()!.Contains(sentinel, StringComparison.Ordinal) || item.Value.ToString()!.Contains(pathSentinel, StringComparison.Ordinal) || item.Value.ToString()!.Contains(resourceSentinel, StringComparison.Ordinal))).ShouldBeFalse();
    }

    /// <summary>Proves failing standard observers cannot change a committed result or ambient parent.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenObserversThrow_PreservesResultAndParent()
    {
        using var parent = new Activity("sqlite-throwing-observer-test").Start();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name is AgentKitMetricNames.BudgetLedgerOperationCount or AgentKitMetricNames.BudgetLedgerOperationDuration)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                throw new InvalidOperationException("meter observer failure");
            }
        });
        meterListener.SetMeasurementEventCallback<double>((_, _, _, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                throw new InvalidOperationException("meter observer failure");
            }
        });
        meterListener.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
                {
                    throw new InvalidOperationException("observer failure");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new SqliteBudgetLedgerConformanceFixture(new ThrowingLogger()).CreateLedger();
        var result = await ledger.CreateScopeAsync(CreateRequest("throwing-observer", new("test.sum")), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    /// <summary>Proves disabled diagnostic listeners leave semantic execution unchanged.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenDiagnosticsAreDisabled_Succeeds()
    {
        var ledger = new SqliteBudgetLedgerConformanceFixture().CreateLedger();
        var result = await ledger.CreateScopeAsync(CreateRequest("disabled-observers", new("test.sum")), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
    }

    private static BudgetLedgerScopeCreateRequest CreateRequest(string key, BudgetDimension dimension) => new(new BudgetScopeRequest(null, new(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null), [new(dimension, 10, new("count"), BudgetLimitKind.Hard)], new(key)), new(8, 32, TimeSpan.FromMinutes(5)));
    private static ActivityListener CreateScopedListener(Activity parent, ConcurrentQueue<Activity> stopped) => new()
    {
        ShouldListenTo = static source => source.Name == "AgentKit",
        Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
        {
            return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
        },
        ActivityStopped = activity =>
        {
            if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
            {
                stopped.Enqueue(activity);
            }
        },
    };
    /// <summary>Proves snapshot correlation and terminal outcome are emitted without affecting the semantic receipt.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenObserved_EmitsCorrelatedSuccessfulActivity()
    {
        using var parent = new Activity("sqlite-budget-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name == AgentKitMetricNames.BudgetLedgerOperationCount)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                measurements.Enqueue(tags.ToArray());
            }
        });
        meterListener.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parent.SpanId && activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation)
                {
                    stopped.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var ledger = new SqliteBudgetLedgerConformanceFixture(logger).CreateLedger();
        var address = new BudgetScopeAddress(new("tenant"), new("principal"), new(Guid.NewGuid()), null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(null, address, [new(new("test.sum"), 10, new("count"), BudgetLimitKind.Hard)], new("diagnostic")), new(8, 32, TimeSpan.FromMinutes(5)));
        var cancellationToken = TestContext.Current.CancellationToken;
        var scope = (await ledger.CreateScopeAsync(request, cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var snapshot = await ledger.GetSnapshotAsync(scope, cancellationToken);
        snapshot.ScopeId.ShouldBe(scope.Id);
        var activity = stopped.Last(item => item.GetTagItem(AgentKitTagNames.BudgetOperation)?.ToString() == "snapshot");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.BudgetScopeId)?.ToString().ShouldBe(scope.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.TenantId)?.ToString().ShouldBe(scope.Address.TenantId.ToString());
        var metric = measurements.Last(tags => tags.Any(tag => tag.Key == AgentKitTagNames.BudgetOperation && Equals(tag.Value, "snapshot")));
        metric.ShouldContain(tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "succeeded"));
        metric.ShouldNotContain(tag => tag.Key == AgentKitTagNames.TenantId || tag.Key == AgentKitTagNames.BudgetScopeId);
        var log = logger.Entries.Last(entry => entry.EventId.Id == 7070 && entry.State.Any(item => item.Key == "BudgetOperation" && Equals(item.Value, "snapshot")));
        log.State.ShouldContain(item => item.Key == "BudgetScopeId" && Equals(item.Value, scope.Id.ToString()));
        log.State.ShouldContain(item => item.Key == "TenantId" && Equals(item.Value, scope.Address.TenantId.ToString()));
        log.State.ShouldNotContain(item => item.Key.Contains("Resource", StringComparison.OrdinalIgnoreCase) || item.Key.Contains("Fingerprint", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CaptureLogger: ILogger<SqliteBudgetLedger>
    {
        internal ConcurrentQueue<(EventId EventId, KeyValuePair<string, object?>[] State)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.Enqueue((eventId, values.ToArray()));
            }
        }
    }

    private sealed class ThrowingLogger: ILogger<SqliteBudgetLedger>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger failure");
    }

    /// <summary>Verifies the generated log-state accessors work through the classic non-generic enumeration surface
    /// that some third-party logging providers use instead of the generic key/value interface.</summary>
    [Fact]
    public async Task Operations_WhenLoggerEnumeratesStateViaLegacyEnumerable_ExercisesGeneratedStateAccessors()
    {
        var logger = new LegacyEnumeratingLogger();
        var ledger = new SqliteBudgetLedgerConformanceFixture(logger).CreateLedger();
        var created = await ledger.CreateScopeAsync(Request("legacy-enumerable-scope"), TestContext.Current.CancellationToken);
        _ = created.ShouldBeOfType<BudgetLedgerScopeCreated>();
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.GetSnapshotAsync(new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), Request("legacy-enumerable-scope").OriginalRequest.Address), TestContext.Current.CancellationToken));
        logger.CompletedCounts.ShouldNotBeEmpty();
        logger.FailedCounts.ShouldNotBeEmpty();
        logger.CompletedCounts.ShouldAllBe(count => count > 0);
        logger.FailedCounts.ShouldAllBe(count => count > 0);
    }

    private sealed class LegacyEnumeratingLogger: ILogger<SqliteBudgetLedger>
    {
        internal List<int> CompletedCounts { get; } = [];
        internal List<int> FailedCounts { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not System.Collections.IEnumerable legacy)
            {
                return;
            }

            var count = 0;
            foreach (var _ in legacy)
            {
                count++;
            }

            if (state is IReadOnlyList<KeyValuePair<string, object?>> indexed && indexed.Count > 0)
            {
                for (var index = 0; index < indexed.Count; index++)
                {
                    _ = indexed[index];
                }

                try
                {
                    _ = indexed[indexed.Count];
                }
                catch (IndexOutOfRangeException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            _ = state?.ToString();
            _ = formatter(state, exception);

            if (eventId.Id == 7070)
            {
                CompletedCounts.Add(count);
            }
            else if (eventId.Id == 7071)
            {
                FailedCounts.Add(count);
            }
        }
    }

    /// <summary>Proves fixed keys preserve numeric ordering across scale and decimal boundaries.</summary>
    [Fact]
    public void MaximumKey_WhenValuesDiffer_PreservesNumericOrdering()
    {
        decimal[] values = [0m, 0.0000000000000000000000000001m, 0.1m, 1m, 1.00m, decimal.MaxValue];
        var ordered = values.Select(value => (Value: value, Key: SqliteBudgetLedger.MaximumKey(value))).OrderBy(static item => item.Key, ByteArrayComparer.Instance).Select(static item => item.Value).ToArray();
        ordered.ShouldBe(values);
        SqliteBudgetLedger.MaximumKey(1m).ShouldBe(SqliteBudgetLedger.MaximumKey(1.00m));
    }

    private sealed class ByteArrayComparer: IComparer<byte[]>
    {
        internal static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right) => left.AsSpan().SequenceCompareTo(right);
    }

    /// <summary>Removes the isolated target.</summary>
    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        Directory.Delete(_directorySqliteBudgetLedgerCreateScope, recursive: true);
    }
}
