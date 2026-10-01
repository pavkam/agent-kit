// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsAbsoluteAndOrdinary_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(Path.GetFullPath("evaluation.db")));

    [Fact]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsNull_ThrowsArgumentNullExceptionWithTheCallerExpression()
    {
        string? databasePath = null;

        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(databasePath!)).ParamName.ShouldBe("databasePath");
    }

    [Theory]
    [InlineData("relative.db")]
    [InlineData(":MEMORY:")]
    [InlineData("FILE:x.db")]
    [InlineData("|datadirectory|x.db")]
    public void ThrowIfInvalidSqliteDatabasePath_WhenPathIsNotOrdinary_ThrowsArgumentException(string path) =>
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfInvalidSqliteDatabasePath(path)).ParamName.ShouldBe("path");
}
