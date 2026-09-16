// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Session;

using Microsoft.Data.Sqlite;

/// <summary>Verifies bootstrap and schema-validation edge cases for the internal relational session database.</summary>
public sealed class SqliteSessionDatabaseTests
{
    [Fact]
    public void Constructor_WhenParentDirectoryDoesNotExist_Throws()
    {
        var missingParent = Path.Combine(
            Path.GetTempPath(), $"agentkit-database-missing-parent-{Guid.NewGuid():N}", "sessions.db");
        var target = new SqliteSessionStoreTarget(missingParent, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDatabase(target, SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog()));

        exception.Message.ShouldBe("The SQLite session target parent directory does not exist.");
    }

    [Fact]
    public void Constructor_WhenOpenExistingAndFileIsMissing_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-database-missing-file-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var target = new SqliteSessionStoreTarget(Path.Combine(path, "sessions.db"),
            new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDatabase(target, SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog()));

        exception.Message.ShouldBe("The SQLite session target does not exist.");
    }

    [Fact]
    public void Constructor_WhenSchemaModeIsValidateExactAndSchemaTableMissing_Throws()
    {
        // ValidateExact must never install schema; an otherwise-empty database is rejected with the typed failure.
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-database-validate-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ConnectionString))
        {
            connection.Open();
        }

        var target = new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDatabase(target, SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog()));

        exception.Message.ShouldBe("The SQLite session schema or persistent store identity is unavailable.");
        using var probe = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
        probe.Open();
        using var command = probe.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'agentkit_session_schema';";
        Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenPersistedStoreInstanceIdDiffers_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-database-instance-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        _ = new SqliteSessionDatabase(
            new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog());
        var foreign = new SqliteSessionStoreTarget(databasePath, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDatabase(foreign, SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog()));

        exception.Message.ShouldBe("The SQLite session schema or persistent store identity is unavailable.");
    }

    [Fact]
    public void Constructor_WhenValidateExactAndOneRequiredTableIsMissing_Throws()
    {
        // The schema marker row can validate while a later required table is absent; every required table must be checked.
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-database-partial-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE TABLE {SqliteSessionSchema.SchemaTable} (
                    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                    schema_version INTEGER NOT NULL,
                    store_instance_id TEXT NOT NULL
                );
                INSERT INTO {SqliteSessionSchema.SchemaTable} VALUES (1, 1, $id);
                """;
            _ = command.Parameters.AddWithValue("$id", instance.Value.ToString("D"));
            _ = command.ExecuteNonQuery();
        }

        var target = new SqliteSessionStoreTarget(
            databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDatabase(target, SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog()));

        exception.Message.ShouldBe("The SQLite session schema or persistent store identity is unavailable.");
    }

    [Fact]
    public void Constructor_WhenLegacyWholeBlobTableExists_LeavesItInertAndCreatesRelationalSchemaFresh()
    {
        // The pre-1.0 single-row-blob format is a breaking change: ApplyKnownMigrations never reads or reinterprets
        // it, it just creates the new relational tables fresh alongside the untouched legacy table.
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-database-legacy-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var databasePath = Path.Combine(path, "sessions.db");
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE agentkit_session(singleton INTEGER PRIMARY KEY CHECK(singleton=1),state_json BLOB NOT NULL); " +
                "INSERT INTO agentkit_session VALUES(1,$state);";
            _ = command.Parameters.AddWithValue("$state", "{}"u8.ToArray());
            _ = command.ExecuteNonQuery();
        }

        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        var validateOnly = Should.Throw<InvalidOperationException>(() => new SqliteSessionDatabase(
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact),
            SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog()));
        _ = new SqliteSessionDatabase(
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ApplyKnownMigrations),
            SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog());
        var migrated = new SqliteSessionDatabase(
            new SqliteSessionStoreTarget(databasePath, instance, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact),
            SqliteSessionStoreSettings.CreateDefault(), EmptyCatalog());

        validateOnly.Message.ShouldBe("The SQLite session schema or persistent store identity is unavailable.");
        _ = migrated.ShouldNotBeNull();
        using var probe = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
        probe.Open();
        using var legacyProbe = probe.CreateCommand();
        legacyProbe.CommandText = "SELECT state_json FROM agentkit_session WHERE singleton = 1;";
        ((byte[]) legacyProbe.ExecuteScalar()!).ShouldBe("{}"u8.ToArray());
    }

    private static SessionEntryCodecCatalog EmptyCatalog() => new([], TimeProvider.System);
}
