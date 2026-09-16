// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionLocation behavior and contracts.</summary>
public sealed class SessionLocationTests
{
    [Fact]
    public void Constructor_WhenAddressIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocation(null!, new TenantId("tenant"), new SessionStoreKey("store"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1")));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void Constructor_WhenTenantIdIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocation(SessionsTestData.Address(), default, new SessionStoreKey("store"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1")));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenStoreKeyIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocation(SessionsTestData.Address(), new TenantId("tenant"), default, new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1")));
        exception.ParamName.ShouldBe("storeKey");
    }

    [Fact]
    public void Constructor_WhenDirectoryRevisionIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionLocation(SessionsTestData.Address(), new TenantId("tenant"), new SessionStoreKey("store"), default, DateTimeOffset.UnixEpoch, new SchemaVersion("v1")));
        exception.ParamName.ShouldBe("directoryRevision");
    }

    [Fact]
    public void Constructor_WhenSchemaVersionIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionLocation(SessionsTestData.Address(), new TenantId("tenant"), new SessionStoreKey("store"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, default));
        exception.ParamName.ShouldBe("schemaVersion");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var location = SessionsTestData.Location();
        location.Address.ShouldBe(SessionsTestData.Address());
        location.TenantId.ShouldBe(new TenantId("tenant"));
        location.StoreKey.ShouldBe(new SessionStoreKey("store"));
        location.DirectoryRevision.ShouldBe(new SessionDirectoryRevision(1));
        location.RecordedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        location.SchemaVersion.ShouldBe(new SchemaVersion("v1"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = SessionsTestData.Location();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
