// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;



/// <summary>Verifies SqliteSecurityGrantStoreTarget behavior and contracts.</summary>
public sealed class SqliteSecurityGrantStoreTargetTests
{
    /// <summary>Verifies fixed-target construction rejects every invalid bootstrap coordinate with its exact parameter.</summary>
    [Fact]
    public void Constructor_WhenTargetEvidenceIsInvalid_ThrowsExactArgument()
    {
        var instanceId = new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "grants.db");
        var nullPath = Should.Throw<ArgumentNullException>(() => new SqliteSecurityGrantStoreTarget(null!, instanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var blankPath = Should.Throw<ArgumentException>(() => new SqliteSecurityGrantStoreTarget(" ", instanceId, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var emptyId = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(path, default, SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact));
        var openMode = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(path, instanceId, (SqliteDatabaseOpenMode) 99, SqliteSchemaMode.ValidateExact));
        var schemaMode = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(path, instanceId, SqliteDatabaseOpenMode.OpenExisting, (SqliteSchemaMode) 99));
        var impossible = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreTarget(path, instanceId, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ValidateExact));
        var directId = Should.Throw<ArgumentOutOfRangeException>(() => new SqliteSecurityGrantStoreInstanceId(Guid.Empty));
        nullPath.ParamName.ShouldBe("databasePath");
        blankPath.GetType().ShouldBe(typeof(ArgumentException));
        blankPath.ParamName.ShouldBe("databasePath");
        emptyId.ParamName.ShouldBe("expectedStoreInstanceId");
        openMode.ParamName.ShouldBe("openMode");
        schemaMode.ParamName.ShouldBe("schemaMode");
        impossible.ParamName.ShouldBe("schemaMode");
        directId.ParamName.ShouldBe("value");
    }
}
