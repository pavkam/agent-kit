// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies host-local transaction serialization across independent ledger instances.</summary>
public sealed class SqliteBudgetLedgerConcurrencyTests: IDisposable
{
    private readonly string _directory = CreateDirectoryPath();

    /// <summary>Creates the explicit existing target parent.</summary>
    public SqliteBudgetLedgerConcurrencyTests() => Directory.CreateDirectory(_directory);
    /// <summary>Removes the isolated target.</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

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
        var scope = (await first.CreateScopeAsync(new(new(null, address,
            [new(new("test.sum"), 1, new("count"), BudgetLimitKind.Hard)], new("scope")), new(8, 8, TimeSpan.FromMinutes(5))), cancellationToken))
            .ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var firstRequest = new BudgetLedgerBatchReserveRequest(scope,
            [new(scope.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("first"))]);
        var secondRequest = new BudgetLedgerBatchReserveRequest(scope,
            [new(scope.Id, new("test.sum"), 1, new("count"), new(Guid.NewGuid()), null, new("second"))]);

        var results = await Task.WhenAll(
            Task.Run(async () => await first.ReserveBatchAsync(firstRequest, cancellationToken), cancellationToken),
            Task.Run(async () => await second.ReserveBatchAsync(secondRequest, cancellationToken), cancellationToken));

        results.Count(static result => result is BudgetLedgerBatchReserved).ShouldBe(1);
        results.Count(static result => result is BudgetLedgerBatchReserveRejected).ShouldBe(1);
    }

    private static SqliteBudgetLedger Create(SqliteBudgetLedgerTarget target) => new(target,
        SqliteBudgetLedgerSettings.CreateDefault(), TimeProvider.System, new ScopeIds(), new ReservationIds(), new Catalog());

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
}
