// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Verifies the SQLite target, settings, options, and instance-identity constraints.</summary>
public sealed class SqliteMemoryConfigurationTests
{
    [Fact]
    public void InstanceId_WhenEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemoryInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void InstanceId_WhenSupplied_FormatsAsAHyphenatedGuid()
    {
        var value = Guid.NewGuid();

        new SqliteMemoryInstanceId(value).ToString().ShouldBe(value.ToString("D"));
    }

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var id = new SqliteMemoryInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "memory.db");

        Should.Throw<ArgumentNullException>(() => new SqliteMemoryTarget(null!, id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentException>(() => new SqliteMemoryTarget(":memory:", id, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemoryTarget(path, default, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("expectedInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemoryTarget(path, id, (SqliteDatabaseOpenMode) 9, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemoryTarget(path, id, SqliteDatabaseOpenMode.OpenExisting, (SqliteSchemaMode) 9)).ParamName.ShouldBe("schemaMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemoryTarget(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact)).ParamName.ShouldBe("schemaMode");
    }

    [Fact]
    public void Target_WhenValid_PreservesEveryValue()
    {
        var id = new SqliteMemoryInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "memory.db");

        var target = new SqliteMemoryTarget(path, id, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);

        target.DatabasePath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedInstanceId.ShouldBe(id);
        target.OpenMode.ShouldBe(SqliteDatabaseOpenMode.CreateIfMissing);
        target.SchemaMode.ShouldBe(SqliteSchemaMode.ApplyKnownMigrations);
    }

    [Fact]
    public void Settings_WhenABoundIsInvalid_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemorySettings(TimeSpan.FromMilliseconds(500), 1)).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemorySettings(TimeSpan.FromMilliseconds(1_500), 1)).ParamName.ShouldBe("lockTimeout");
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteMemorySettings(TimeSpan.FromSeconds(2), 0)).ParamName.ShouldBe("maximumRecordBytes");
    }

    [Fact]
    public void Options_WhenDefaulted_ExposeTheDocumentedBounds()
    {
        var options = new SqliteMemoryOptions();
        var settings = SqliteMemorySettings.CreateDefault();

        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.MaximumRecordBytes.ShouldBe(16_777_216);
        settings.LockTimeout.ShouldBe(options.LockTimeout);
        settings.MaximumRecordBytes.ShouldBe(options.MaximumRecordBytes);
    }
}
