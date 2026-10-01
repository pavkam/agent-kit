// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite.Tests;

/// <summary>Verifies the canonical SQLite database-path guard.</summary>
public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsOrdinaryAndFullyQualified_DoesNotThrow()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "goals.db");

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath));
    }

    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsNull_ThrowsArgumentNullExceptionWithTheInferredParameterName()
    {
        string? databasePath = null;

        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath!)).ParamName.ShouldBe("databasePath");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/goals.db")]
    [InlineData(":memory:")]
    [InlineData("FILE:goals.db")]
    [InlineData("|DataDirectory|goals.db")]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsNotOneOrdinaryFilePath_ThrowsArgumentExceptionWithTheInferredParameterName(string databasePath) =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath)).ParamName.ShouldBe("databasePath");

    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenAnExplicitParameterNameIsSupplied_UsesIt() =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(":memory:", "custom")).ParamName.ShouldBe("custom");
}
