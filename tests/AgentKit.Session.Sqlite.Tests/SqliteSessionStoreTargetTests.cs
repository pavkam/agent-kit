// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

/// <summary>Verifies the validated immutable SQLite bootstrap target.</summary>
public sealed class SqliteSessionStoreTargetTests
{
    [Fact]
    public void Constructor_WhenPathIsFullyQualified_NormalizesAndPreservesEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-target-{Guid.NewGuid():N}", "sessions.db");
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());

        var target = new SqliteSessionStoreTarget(path, instance, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        target.DatabasePath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedStoreInstanceId.ShouldBe(instance);
        target.OpenMode.ShouldBe(SqliteDatabaseOpenMode.CreateIfMissing);
        target.SchemaMode.ShouldBe(SqliteSchemaMode.ApplyKnownMigrations);
    }

    [Fact]
    public void Constructor_WhenDatabasePathIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new SqliteSessionStoreTarget(
                null!, new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("databasePath");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDatabasePathIsBlank_ThrowsExactParameter(string path) =>
        Should.Throw<ArgumentException>(() => new SqliteSessionStoreTarget(
                path, new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("databasePath");

    [Fact]
    public void Constructor_WhenDatabasePathIsRelative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteSessionStoreTarget(
            "relative/sessions.db", new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations));

        exception.ParamName.ShouldBe("databasePath");
        exception.Message.ShouldContain("fully qualified ordinary path");
    }

    [Fact]
    public void Constructor_WhenDatabasePathIsInMemorySpecialTarget_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new SqliteSessionStoreTarget(
                ":memory:", new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("databasePath");

    [Fact]
    public void Constructor_WhenDatabasePathUsesFileUriPrefix_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new SqliteSessionStoreTarget(
                "file:sessions.db", new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("databasePath");

    [Fact]
    public void Constructor_WhenDatabasePathUsesDataDirectorySubstitution_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new SqliteSessionStoreTarget(
                "|DataDirectory|sessions.db", new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("databasePath");

    [Fact]
    public void Constructor_WhenExpectedStoreInstanceIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions.db"), default, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("expectedStoreInstanceId");

    [Fact]
    public void Constructor_WhenOpenModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                (SqliteDatabaseOpenMode) 99, SqliteSchemaMode.ApplyKnownMigrations))
            .ParamName.ShouldBe("openMode");

    [Fact]
    public void Constructor_WhenSchemaModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, (SqliteSchemaMode) 99))
            .ParamName.ShouldBe("schemaMode");

    [Fact]
    public void Constructor_WhenCreateIfMissingCombinedWithValidateExact_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact))
            .ParamName.ShouldBe("schemaMode");

    [Fact]
    public void Constructor_WhenOpenExistingCombinedWithApplyKnownMigrations_Accepts()
    {
        var target = new SqliteSessionStoreTarget(
            Path.Combine(Path.GetTempPath(), "sessions.db"), new SqliteSessionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ApplyKnownMigrations);

        target.OpenMode.ShouldBe(SqliteDatabaseOpenMode.OpenExisting);
        target.SchemaMode.ShouldBe(SqliteSchemaMode.ApplyKnownMigrations);
    }

    [Fact]
    public void Equals_WhenAllFieldsMatch_IsEqual()
    {
        var path = Path.Combine(Path.GetTempPath(), "sessions.db");
        var instance = new SqliteSessionStoreInstanceId(Guid.NewGuid());
        var left = new SqliteSessionStoreTarget(path, instance, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var right = new SqliteSessionStoreTarget(path, instance, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        left.ShouldBe(right);
    }

    [Fact]
    public void Equals_WhenInstanceIdDiffers_IsNotEqual()
    {
        var path = Path.Combine(Path.GetTempPath(), "sessions.db");
        var left = new SqliteSessionStoreTarget(
            path, new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var right = new SqliteSessionStoreTarget(
            path, new SqliteSessionStoreInstanceId(Guid.NewGuid()), SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        left.ShouldNotBe(right);
    }
}
