// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class SqliteEvaluationStoreTargetTests
{
    private static readonly SqliteEvaluationStoreInstanceId _id = new(Guid.NewGuid());

    private static string Absolute => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "evaluation.db"));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("relative.db")]
    [InlineData(":memory:")]
    [InlineData("file:evaluation.db")]
    [InlineData("|DataDirectory|/evaluation.db")]
    public void Constructor_WhenPathIsNotOneOrdinaryAbsolutePath_ThrowsWithTheParameterName(string? path)
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteEvaluationStoreTarget(path!, _id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));

        exception.ParamName.ShouldBe("databasePath");
    }

    [Fact]
    public void Constructor_WhenIdentityOrModesAreInvalid_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreTarget(Absolute, default, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations)).ParamName.ShouldBe("expectedInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreTarget(Absolute, _id, (SqliteDatabaseOpenMode) 9, SqliteSchemaMode.ApplyKnownMigrations)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreTarget(Absolute, _id, SqliteDatabaseOpenMode.CreateIfMissing, (SqliteSchemaMode) 9)).ParamName.ShouldBe("schemaMode");
    }

    [Fact]
    public void Constructor_WhenCreationIsCombinedWithExactValidation_IsRefusedAsContradictory() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteEvaluationStoreTarget(Absolute, _id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("schemaMode");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var target = new SqliteEvaluationStoreTarget(Absolute, _id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);

        (target.DatabasePath, target.ExpectedInstanceId, target.OpenMode, target.SchemaMode).ShouldBe((Absolute, _id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
    }
}
