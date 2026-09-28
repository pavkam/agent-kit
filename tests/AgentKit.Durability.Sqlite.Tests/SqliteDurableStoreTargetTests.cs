// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies bootstrap target configuration is validated before anything touches the filesystem.</summary>
public sealed class SqliteDurableStoreTargetTests
{
    private static readonly SqliteDurableStoreInstanceId Identity = new(Guid.NewGuid());

    /// <summary>Verifies a fully qualified path is normalized and the explicit modes are retained.</summary>
    [Fact]
    public void Constructor_WhenConfigurationIsValid_NormalizesThePathAndRetainsTheModes()
    {
        var path = Path.Combine(Path.GetTempPath(), "agentkit-durability-target", "durability.db");

        var target = new SqliteDurableStoreTarget(
            path, Identity, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        target.DatabasePath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedStoreInstanceId.ShouldBe(Identity);
        target.OpenMode.ShouldBe(SqliteDatabaseOpenMode.CreateIfMissing);
        target.SchemaMode.ShouldBe(SqliteSchemaMode.ApplyKnownMigrations);
    }

    /// <summary>Verifies a null path is refused before any path parsing happens.</summary>
    [Fact]
    public void Constructor_WhenDatabasePathIsNull_ThrowsForTheDatabasePathArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableStoreTarget(
            null!, Identity, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies a relative path is refused, because a durable target must not depend on the working directory.</summary>
    [Fact]
    public void Constructor_WhenDatabasePathIsRelative_ThrowsForTheDatabasePathArgument()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableStoreTarget(
            "durability.db", Identity, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies a SQLite memory target is refused, because it can never be a durable store.</summary>
    [Fact]
    public void Constructor_WhenDatabasePathIsAMemoryTarget_ThrowsForTheDatabasePathArgument()
    {
        var exception = Should.Throw<ArgumentException>(() => new SqliteDurableStoreTarget(
            ":memory:", Identity, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("databasePath");
    }

    /// <summary>Verifies a default identity is refused, so a store can always be told apart from another deployment.</summary>
    [Fact]
    public void Constructor_WhenExpectedStoreInstanceIdIsDefault_ThrowsForThatArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteDurableStoreTarget(
            Path.Combine(Path.GetTempPath(), "durability.db"),
            default,
            SqliteDatabaseOpenMode.OpenExisting,
            SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("expectedStoreInstanceId");
    }

    /// <summary>Verifies permitting creation while refusing schema application is rejected as contradictory.</summary>
    [Fact]
    public void Constructor_WhenCreationIsCombinedWithValidateExact_ThrowsForTheSchemaModeArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteDurableStoreTarget(
            Path.Combine(Path.GetTempPath(), "durability.db"),
            Identity,
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("schemaMode");
    }

    /// <summary>Verifies an undefined open mode is refused rather than treated as a default.</summary>
    [Fact]
    public void Constructor_WhenOpenModeIsUndefined_ThrowsForTheOpenModeArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteDurableStoreTarget(
            Path.Combine(Path.GetTempPath(), "durability.db"),
            Identity,
            (SqliteDatabaseOpenMode) 42,
            SqliteSchemaMode.ValidateExact));

        exception.ParamName.ShouldBe("openMode");
    }
}
