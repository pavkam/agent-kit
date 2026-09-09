// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using Microsoft.Data.Sqlite;

/// <summary>Verifies the fixed-target schema bootstrap boundary.</summary>
public sealed class SqliteBudgetLedgerDatabaseTests: IDisposable
{
    private readonly string _directory = CreateTemporaryDirectory();

    /// <summary>Creates an isolated existing bootstrap parent.</summary>
    public SqliteBudgetLedgerDatabaseTests()
    {
    }

    /// <summary>Removes the isolated target after each scenario.</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string CreateTemporaryDirectory()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }
        var directory = Path.Combine(root, $"agentkit-budget-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Proves known schema creation and exact validation preserve persistent identity.</summary>
    [Fact]
    public void Initialize_WhenCreatedThenValidated_PreservesExactSchemaAndIdentity()
    {
        var id = new SqliteBudgetLedgerInstanceId(Guid.NewGuid());
        var path = Path.Combine(_directory, "ledger.db");
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var create = new SqliteBudgetLedgerDatabase(new(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), settings);

        create.Initialize(CancellationToken.None);
        var validate = new SqliteBudgetLedgerDatabase(new(path, id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), settings);
        Should.NotThrow(() => validate.Initialize(CancellationToken.None));

        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name LIKE 'budget_%';";
        Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture).ShouldBe(12);
    }

    /// <summary>Proves validation-only mode does not initialize an empty target.</summary>
    [Fact]
    public void Initialize_WhenSchemaIsEmptyAndValidationOnly_ThrowsWithoutSchema()
    {
        var path = Path.Combine(_directory, "ledger.db");
        using (File.Create(path)) { }
        var database = new SqliteBudgetLedgerDatabase(
            new(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact),
            SqliteBudgetLedgerSettings.CreateDefault());

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => database.Initialize(CancellationToken.None));

        exception.AcknowledgementUnknown.ShouldBeFalse();
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE name LIKE 'budget_%';";
        Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture).ShouldBe(0);
    }

    /// <summary>Proves target identity mismatch fails closed.</summary>
    [Fact]
    public void Initialize_WhenExpectedIdentityChanges_ThrowsUnavailable()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        new SqliteBudgetLedgerDatabase(
            new(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), settings)
            .Initialize(CancellationToken.None);
        var mismatched = new SqliteBudgetLedgerDatabase(
            new(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), settings);

        _ = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => mismatched.Initialize(CancellationToken.None));
    }

    /// <summary>Proves exact validation rejects executable schema additions even when all required tables remain present.</summary>
    [Fact]
    public void Initialize_WhenTriggerIsAdded_ThrowsUnavailable()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var id = new SqliteBudgetLedgerInstanceId(Guid.NewGuid());
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        new SqliteBudgetLedgerDatabase(new(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), settings)
            .Initialize(CancellationToken.None);
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER budget_tamper AFTER UPDATE ON budget_ledger_metadata BEGIN SELECT 1; END;";
            _ = command.ExecuteNonQuery();
        }
        var validate = new SqliteBudgetLedgerDatabase(new(path, id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), settings);

        _ = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => validate.Initialize(CancellationToken.None));
    }

    /// <summary>Proves finite lock contention is normalized before any commit acknowledgement is attempted.</summary>
    [Fact]
    public void Write_WhenAnotherConnectionHoldsWriteLock_ThrowsKnownUncommittedUnavailable()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var id = new SqliteBudgetLedgerInstanceId(Guid.NewGuid());
        var defaults = SqliteBudgetLedgerSettings.CreateDefault();
        var settings = new SqliteBudgetLedgerSettings(TimeSpan.FromSeconds(1), defaults.MaximumPayloadBytes,
            defaults.MaximumResultBytes, defaults.MaximumBatchSize, defaults.MaximumLimitsPerScope, defaults.MaximumLineageDepth);
        var database = new SqliteBudgetLedgerDatabase(
            new(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations), settings);
        database.Initialize(CancellationToken.None);
        using var blocker = new SqliteConnection($"Data Source={path};Pooling=False");
        blocker.Open();
        using var transaction = blocker.BeginTransaction(deferred: false);

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() =>
            database.Write(static (_, _) => true, CancellationToken.None));

        exception.AcknowledgementUnknown.ShouldBeFalse();
    }

    /// <summary>Proves failure at the commit-attempt boundary is classified as acknowledgement uncertainty and remains safely retryable.</summary>
    [Fact]
    public void Write_WhenCommitAcknowledgementFaultIsInjected_ReportsUnknownAndRetryReadsAuthoritativeState()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var id = new SqliteBudgetLedgerInstanceId(Guid.NewGuid());
        var target = new SqliteBudgetLedgerTarget(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        var bootstrap = new SqliteBudgetLedgerDatabase(target, settings);
        bootstrap.Initialize(CancellationToken.None);
        var faulting = new SqliteBudgetLedgerDatabase(target, settings,
            static () => throw new IOException("Injected loss of commit acknowledgement."));

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => faulting.Write(static (connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE budget_ledger_metadata SET revision=revision+1;";
            _ = command.ExecuteNonQuery();
            return true;
        }, CancellationToken.None));

        exception.AcknowledgementUnknown.ShouldBeTrue();
        bootstrap.Write(static (connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT revision FROM budget_ledger_metadata;";
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }, CancellationToken.None).ShouldBe(1);
    }

    /// <summary>Proves active-capacity and unresolved scans use partial indexes instead of settled lifetime history.</summary>
    [Fact]
    public void Initialize_WhenQueryPlansAreInspected_UsesBoundedActiveIndexes()
    {
        var path = Path.Combine(_directory, "ledger.db");
        var database = new SqliteBudgetLedgerDatabase(
            new(path, new(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteBudgetLedgerSettings.CreateDefault());
        database.Initialize(CancellationToken.None);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        var scopeId = Guid.NewGuid().ToByteArray();
        PopulateSettledHistory(connection, scopeId);
        var scopeHex = Convert.ToHexString(scopeId);

        QueryPlan(connection, $"EXPLAIN QUERY PLAN SELECT r.receipt,r.receipt_digest,r.aggregation,r.started_ticks,r.started_offset_ticks,r.start_revision,r.released,r.start_expiration,r.start_expiration_digest,r.original_commit,r.original_commit_digest,r.current_commit,r.current_commit_digest,r.accounting_revision,r.latest_correction_revision FROM budget_reservation_charges c JOIN budget_reservations r ON r.reservation_id=c.reservation_id WHERE c.scope_id=X'{scopeHex}' AND c.active=1;")
            .ShouldContain("budget_active_charges_idx");
        QueryPlan(connection, $"EXPLAIN QUERY PLAN SELECT receipt,receipt_digest,aggregation,started_ticks,started_offset_ticks,start_revision,released,start_expiration,start_expiration_digest,original_commit,original_commit_digest,current_commit,current_commit_digest,accounting_revision,latest_correction_revision FROM budget_reservations WHERE scope_id=X'{scopeHex}' AND started_ticks IS NOT NULL AND current_commit IS NULL AND released=0 AND start_revision<=1000;")
            .ShouldContain("budget_unresolved_reservations_idx");
    }

    private static void PopulateSettledHistory(SqliteConnection connection, byte[] scopeId)
    {
        using (var scope = connection.CreateCommand())
        {
            scope.CommandText = "INSERT INTO budget_scopes(scope_id,parent_scope_id,depth,request,request_digest) VALUES($scope,NULL,1,X'01',zeroblob(32));";
            _ = scope.Parameters.AddWithValue("$scope", scopeId);
            _ = scope.ExecuteNonQuery();
        }
        for (var index = 0; index < 101; index++)
        {
            var active = index == 100;
            var reservationId = Guid.NewGuid().ToByteArray();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO budget_reservations(reservation_id,scope_id,receipt,receipt_digest,aggregation,started_ticks,started_offset_ticks,start_revision,released,current_commit,current_commit_digest,latest_correction_revision) VALUES($id,$scope,X'01',zeroblob(32),0,1,0,$revision,0,$commit,$commit_digest,0); INSERT INTO budget_reservation_charges(scope_id,reservation_id,active) VALUES($scope,$id,$active);";
            _ = command.Parameters.AddWithValue("$id", reservationId);
            _ = command.Parameters.AddWithValue("$scope", scopeId);
            _ = command.Parameters.AddWithValue("$revision", index + 1);
            _ = command.Parameters.AddWithValue("$commit", active ? DBNull.Value : new byte[] { 1 });
            _ = command.Parameters.AddWithValue("$commit_digest", active ? DBNull.Value : new byte[32]);
            _ = command.Parameters.AddWithValue("$active", active ? 1 : 0);
            _ = command.ExecuteNonQuery();
        }
    }

    private static string QueryPlan(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader.GetString(3));
        }
        return string.Join(' ', result);
    }
}
