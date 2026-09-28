// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the package-owned SQLite path guard accepts only one fully qualified ordinary file path.</summary>
public sealed class ArgumentExceptionExtensionsTests
{
    /// <summary>Verifies a fully qualified ordinary path passes the guard.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsFullyQualified_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "durability.db");

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(path));
    }

    /// <summary>Verifies a null path throws the null-argument subtype with the inferred parameter name.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsNull_ThrowsWithTheInferredParameterName()
    {
        string? databasePath = null;

        var exception = Should.Throw<ArgumentNullException>(
            () => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath!));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies a blank path is refused.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsBlank_ThrowsWithTheInferredParameterName()
    {
        var databasePath = "   ";

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies a relative path is refused, because it depends on the working directory.</summary>
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsRelative_ThrowsWithTheInferredParameterName()
    {
        var databasePath = Path.Combine("local", "durability.db");

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies the SQLite memory target is refused however it is cased.</summary>
    [Theory]
    [InlineData(":memory:")]
    [InlineData(":MEMORY:")]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsAMemoryTarget_ThrowsWithTheInferredParameterName(
        string databasePath)
    {
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies a URI or substitution target is refused, because it is not an ordinary file path.</summary>
    [Theory]
    [InlineData("file:/tmp/durability.db")]
    [InlineData("|DataDirectory|durability.db")]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsNotAnOrdinaryFilePath_ThrowsWithTheInferredParameterName(
        string databasePath)
    {
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies the production call site in the target constructor uses this exact guard.</summary>
    [Fact]
    public void SqliteDurableStoreTarget_WhenDatabasePathIsAMemoryTarget_ReportsTheGuardsParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableStoreTarget(
            ":memory:",
            new SqliteDurableStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting,
            SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("databasePath");
    }
}
