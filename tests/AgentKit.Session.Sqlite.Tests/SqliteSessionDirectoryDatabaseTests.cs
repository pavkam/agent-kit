// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

/// <summary>Verifies bootstrap edge cases for the internal relational session-directory database.</summary>
public sealed class SqliteSessionDirectoryDatabaseTests
{
    [Fact]
    public void Constructor_WhenParentDirectoryDoesNotExist_Throws()
    {
        var missingParent = Path.Combine(
            Path.GetTempPath(), $"agentkit-directory-database-missing-parent-{Guid.NewGuid():N}", "sessions.db");
        var target = new SqliteSessionStoreTarget(missingParent, new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDirectoryDatabase(target, SqliteSessionStoreSettings.CreateDefault()));

        exception.Message.ShouldBe("The SQLite session target parent directory does not exist.");
    }

    [Fact]
    public void Constructor_WhenOpenExistingAndFileIsMissing_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-directory-database-missing-file-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        var target = new SqliteSessionStoreTarget(Path.Combine(path, "sessions.db"),
            new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        var exception = Should.Throw<InvalidOperationException>(() =>
            new SqliteSessionDirectoryDatabase(target, SqliteSessionStoreSettings.CreateDefault()));

        exception.Message.ShouldBe("The SQLite session target does not exist.");
    }
}
