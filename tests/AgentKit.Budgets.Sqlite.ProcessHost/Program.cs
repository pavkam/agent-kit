// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using AgentKit.Budgets.Sqlite;

var path = args[0];
var storeId = new SqliteBudgetLedgerInstanceId(Guid.Parse(args[1]));
var readyPath = args[2];
var ledger = new SqliteBudgetLedger(
    new(path, storeId, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
    SqliteBudgetLedgerSettings.CreateDefault(), TimeProvider.System, new ScopeIds(), new ReservationIds(), new Catalog());
await ledger.InitializeAsync();
var address = new BudgetScopeAddress(new("process-tenant"), new("process-principal"),
    new(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
var scopeResult = await ledger.CreateScopeAsync(new(new(null, address,
    [new(new("test.sum"), 10, new("count"), BudgetLimitKind.Hard)], new("process-scope")),
    new(8, 8, TimeSpan.FromMinutes(5))));
var scope = scopeResult is BudgetLedgerScopeCreated created
    ? created.Scope
    : throw new InvalidOperationException("Scope creation failed.");
var reservationResult = await ledger.ReserveBatchAsync(new(scope,
    [new(scope.Id, new("test.sum"), 1, new("count"), new(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new("process-reservation"))]));
var reservation = reservationResult is BudgetLedgerBatchReserved reserved
    ? reserved.Receipts[0].Reservation
    : throw new InvalidOperationException("Reservation failed.");
_ = await ledger.MarkStartedAsync(reservation);
var temporaryReadyPath = readyPath + ".tmp";
await File.WriteAllTextAsync(temporaryReadyPath, "READY");
File.Move(temporaryReadyPath, readyPath);
await Task.Delay(Timeout.InfiniteTimeSpan);
