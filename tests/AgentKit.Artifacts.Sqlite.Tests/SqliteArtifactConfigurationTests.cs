// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Verifies the SQLite target, settings, options, and instance-identity constraints.</summary>
public sealed class SqliteArtifactConfigurationTests
{
    [Fact]
    public void InstanceId_WhenEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void InstanceId_WhenSupplied_FormatsAsAHyphenatedGuid()
    {
        var value = Guid.NewGuid();

        new SqliteArtifactInstanceId(value).ToString().ShouldBe(value.ToString("D"));
    }

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var id = new SqliteArtifactInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "artifact.db");

        Should.Throw<ArgumentNullException>(() => new SqliteArtifactTarget(null!, id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentException>(() => new SqliteArtifactTarget(":memory:", id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactTarget(path, default, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("expectedInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactTarget(path, id, (SqliteDatabaseOpenMode) 9, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactTarget(path, id, SqliteDatabaseOpenMode.OpenExisting, (SqliteSchemaMode) 9)).ParamName.ShouldBe("schemaMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactTarget(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("schemaMode");
    }

    [Fact]
    public void Target_WhenValid_PreservesEveryValue()
    {
        var id = new SqliteArtifactInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "artifact.db");

        var target = new SqliteArtifactTarget(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        target.DatabasePath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedInstanceId.ShouldBe(id);
        target.OpenMode.ShouldBe(SqliteDatabaseOpenMode.CreateIfMissing);
        target.SchemaMode.ShouldBe(SqliteSchemaMode.ApplyKnownMigrations);
    }

    [Fact]
    public void Settings_WhenABoundIsInvalid_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactSettings(TimeSpan.FromMilliseconds(500), 10)).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactSettings(TimeSpan.FromMilliseconds(1_500), 10)).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactSettings(TimeSpan.FromDays(100_000), 10)).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteArtifactSettings(TimeSpan.FromSeconds(1), 0)).ParamName.ShouldBe("maximumRecordBytes");
    }

    [Fact]
    public void Settings_WhenDefaultsAreUsed_AreSafeFiniteBounds()
    {
        var settings = SqliteArtifactSettings.CreateDefault();
        var options = new SqliteArtifactOptions();

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        settings.MaximumRecordBytes.ShouldBe(1_048_576);
        options.LockTimeout.ShouldBe(settings.LockTimeout);
        options.MaximumRecordBytes.ShouldBe(settings.MaximumRecordBytes);
    }
}
