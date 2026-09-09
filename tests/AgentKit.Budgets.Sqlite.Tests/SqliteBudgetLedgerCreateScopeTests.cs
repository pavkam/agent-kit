// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using System.Diagnostics;

/// <summary>Verifies durable scope creation and replay across ledger instances.</summary>
public sealed class SqliteBudgetLedgerCreateScopeTests: IDisposable
{
    private readonly string _directory = CreateDirectory();

    /// <summary>Removes the isolated database after each scenario.</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>Proves exact replay survives process-local ledger replacement.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenLedgerRestarts_ReplaysOriginalReference()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var target = new SqliteBudgetLedgerTarget(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var first = CreateLedger(target, new ScopeIds(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await first.InitializeAsync(cancellationToken);
        var request = Request("scope");

        var created = (await first.CreateScopeAsync(request, cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>();
        var restartedTarget = new SqliteBudgetLedgerTarget(path, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);
        var restarted = CreateLedger(restartedTarget, new ScopeIds(Guid.Parse("10000000-0000-0000-0000-000000000099")));
        await restarted.InitializeAsync(cancellationToken);

        (await restarted.CreateScopeAsync(request, cancellationToken)).ShouldBe(new BudgetLedgerScopeCreated(created.Scope));
    }

    /// <summary>Proves the adapter lineage bound rejects a child before creating an unreadable row.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenAdapterLineageBoundReached_RejectsChild()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var defaults = SqliteBudgetLedgerSettings.CreateDefault();
        var settings = new SqliteBudgetLedgerSettings(defaults.LockTimeout, defaults.MaximumPayloadBytes,
            defaults.MaximumResultBytes, defaults.MaximumBatchSize, defaults.MaximumLimitsPerScope, 1);
        var ledger = new SqliteBudgetLedger(target, settings, TimeProvider.System,
            new ScopeIds(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ReservationIds(), new Catalog());
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var parent = (await ledger.CreateScopeAsync(Request("parent"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var template = Request("child");
        var childRequest = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(parent.Id, template.OriginalRequest.Address, template.OriginalRequest.Limits,
                template.OriginalRequest.IdempotencyKey), template.Admission);

        var result = await ledger.CreateScopeAsync(childRequest, cancellationToken);

        result.ShouldBeOfType<BudgetLedgerScopeCreateRejected>().Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.MaximumDepthExceeded);
    }

    /// <summary>Proves an atomic reservation and its effective expiry replay after ledger replacement.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenLedgerRestarts_ReplaysOriginalReceipts()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var target = new SqliteBudgetLedgerTarget(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var first = CreateLedger(target, new ScopeIds(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await first.InitializeAsync(cancellationToken);
        var scope = (await first.CreateScopeAsync(Request("scope"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new("test.sum"), 2, new("count"), new(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, new("reserve"));
        var request = new BudgetLedgerBatchReserveRequest(scope, [item]);

        var accepted = (await first.ReserveBatchAsync(request, cancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        var restarted = CreateLedger(new(path, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIds(Guid.NewGuid()));
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
        var restarted = CreateLedger(new(target.DatabasePath, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIds(Guid.NewGuid()));
        await restarted.InitializeAsync(cancellationToken);

        (await restarted.MarkStartedAsync(reservation, cancellationToken)).ShouldBe(new BudgetStarted(reservation.Id, true));
        (await restarted.ReleaseUnstartedAsync(reservation, cancellationToken)).ShouldBe(new BudgetLedgerRetainedStarted(reservation));
    }

    /// <summary>Proves an abruptly terminated helper process leaves started unknown usage recoverable by a new process-local adapter.</summary>
    [Fact]
    public async Task ReadUnresolvedStartedAsync_WhenWriterProcessIsKilled_RetainsStartedUnknownReservation()
    {
        var path = Path.Combine(_directory, "process-loss.db");
        var readyPath = Path.Combine(_directory, "ready");
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
            var ledger = CreateLedger(new(path, storeId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIds(Guid.NewGuid()));
            await ledger.InitializeAsync(cancellationToken);
            var address = new BudgetScopeAddress(new("process-tenant"), new("process-principal"),
                new(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
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
        var restarted = CreateLedger(new(target.DatabasePath, target.ExpectedStoreInstanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), new ScopeIds(Guid.NewGuid()));
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
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new ScopeIds(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var scope = (await ledger.CreateScopeAsync(Request("cycle"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        Execute(target.DatabasePath, "UPDATE budget_scopes SET parent_scope_id=scope_id,depth=2;");
        var item = new BudgetReservationRequest(scope.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("cycle-item"));

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () =>
            await ledger.ReserveBatchAsync(new(scope, [item]), cancellationToken));

        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves a persisted child whose immutable parent row is missing fails closed as storage corruption.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenPersistedParentIsMissing_ThrowsPersistenceUnavailable()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new IncrementingScopeIds());
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var parent = (await ledger.CreateScopeAsync(Request("missing-parent"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var template = Request("orphan-child");
        var childRequest = new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(parent.Id, template.OriginalRequest.Address,
            template.OriginalRequest.Limits, template.OriginalRequest.IdempotencyKey), template.Admission);
        var child = (await ledger.CreateScopeAsync(childRequest, cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        Execute(target.DatabasePath, $"PRAGMA foreign_keys=OFF; DELETE FROM budget_scopes WHERE scope_id=X'{Convert.ToHexString(parent.Id.Value.ToByteArray())}';");
        var item = new BudgetReservationRequest(child.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("orphan-item"));

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () =>
            await ledger.ReserveBatchAsync(new(child, [item]), cancellationToken));

        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves invalid stored projection enums are normalized as persistence corruption.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenProjectionAggregationIsCorrupt_ThrowsPersistenceUnavailable()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        Execute(target.DatabasePath, "UPDATE budget_dimension_projections SET aggregation=999;");

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () =>
            await ledger.GetSnapshotAsync(reservation.Scope));

        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves invalid persisted timestamp columns are normalized without escaping storage exceptions.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenStartedOffsetIsCorrupt_ThrowsPersistenceUnavailable()
    {
        var (ledger, target, reservation) = await CreateReservedAsync();
        _ = await ledger.MarkStartedAsync(reservation, TestContext.Current.CancellationToken);
        Execute(target.DatabasePath, "UPDATE budget_reservations SET started_offset_ticks=9223372036854775807;");

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () =>
            await ledger.GetSnapshotAsync(reservation.Scope));

        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves maximum text and sortable-key disagreement is rejected as persisted corruption.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenMaximumTextDisagreesWithKey_ThrowsPersistenceUnavailable()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new ScopeIds(Guid.NewGuid()));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var template = Request("maximum-corruption");
        var scope = (await ledger.CreateScopeAsync(new(new(null, template.OriginalRequest.Address,
            [new(new("test.maximum"), 10, new("count"), BudgetLimitKind.Hard)], template.OriginalRequest.IdempotencyKey), template.Admission), cancellationToken))
            .ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var operation = new OperationId(Guid.NewGuid());
        var receipts = (await ledger.ReserveBatchAsync(new(scope,
            [new(scope.Id, new("test.maximum"), 10, new("count"), operation, null, new("maximum-row")),
                new(scope.Id, new("test.maximum"), 8, new("count"), operation, null, new("lower-row"))]), cancellationToken))
            .ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts;
        Execute(target.DatabasePath, "UPDATE budget_maximum_values SET amount_text='9' || char(0) || printf('%01000d',0) WHERE amount_text='10';");

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () =>
            await ledger.ReleaseUnstartedAsync(receipts[1].Reservation, cancellationToken));

        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    private async Task<(SqliteBudgetLedger Ledger, SqliteBudgetLedgerTarget Target, BudgetLedgerReservationReference Reservation)> CreateReservedAsync()
    {
        var target = new SqliteBudgetLedgerTarget(Path.Combine(_directory, "ledger.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var ledger = CreateLedger(target, new ScopeIds(Guid.Parse("10000000-0000-0000-0000-000000000001")));
        var cancellationToken = TestContext.Current.CancellationToken;
        await ledger.InitializeAsync(cancellationToken);
        var scope = (await ledger.CreateScopeAsync(Request("scope"), cancellationToken)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, new("test.sum"), 2, new("count"), new(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, new("reserve"));
        var reserved = (await ledger.ReserveBatchAsync(new(scope, [item]), cancellationToken)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        return (ledger, target, reserved.Receipts[0].Reservation);
    }

    private static SqliteBudgetLedger CreateLedger(SqliteBudgetLedgerTarget target, IIdentifierGenerator<BudgetScopeId> scopes) =>
        new(target, SqliteBudgetLedgerSettings.CreateDefault(), TimeProvider.System, scopes, new ReservationIds(), new Catalog());

    private static BudgetLedgerScopeCreateRequest Request(string key) => new(
        new BudgetScopeRequest(null,
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, null, null),
            [new BudgetLimit(new BudgetDimension("test.sum"), 10, new BudgetUnit("count"), BudgetLimitKind.Hard)],
            new IdempotencyKey(key)),
        new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));

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

    private sealed class ScopeIds(Guid value): IIdentifierGenerator<BudgetScopeId>
    {
        public BudgetScopeId Create() => new(value);
    }

    private sealed class IncrementingScopeIds: IIdentifierGenerator<BudgetScopeId>
    {
        private int _value;
        public BudgetScopeId Create() => new(Guid.Parse($"10000000-0000-0000-0000-{++_value:000000000000}"));
    }

    private sealed class ReservationIds: IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => new(Guid.NewGuid());
    }

    private sealed class Catalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = dimension == new BudgetDimension("test.sum")
                ? new(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("count")])
                : dimension == new BudgetDimension("test.maximum")
                    ? new(dimension, BudgetAggregationKind.Maximum, [new BudgetUnit("count")])
                    : null;
            return descriptor is not null;
        }
    }
}
