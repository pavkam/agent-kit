// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies the canonical SQLite target path guard and its production boundary.</summary>
public sealed class ArgumentExceptionExtensionsTests
{
    /// <summary>Proves direct type-qualified invocation rejects null, blank, and non-file targets with exact attribution.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative.db")]
    [InlineData(":memory:")]
    [InlineData("file:/tmp/ledger.db")]
    public void ThrowIfInvalidSqliteDatabasePath_WhenInvalid_ThrowsExactParameter(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSqliteDatabasePath(value!, "databasePath"));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Proves a fully qualified ordinary path is accepted by both the guard and public target.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenAbsoluteFilePath_DoesNotThrow()
    {
        var path = Path.GetFullPath("ledger.db");

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(path));
        _ = Should.NotThrow(() => new SqliteBudgetLedgerTarget(path, new(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
    }
}
