// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryTombstone"/> constraints.</summary>
public sealed class MemoryTombstoneTests
{
    private static readonly AgentId _agent = new(Guid.NewGuid());
    private static readonly MemoryId _id = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var at = DateTimeOffset.UnixEpoch.AddDays(1);

        var tombstone = new MemoryTombstone(_id, _agent, new TenantId("t"), new VersionToken("3"), at, purged: true, generation: 7);

        tombstone.Id.ShouldBe(_id);
        tombstone.AgentId.ShouldBe(_agent);
        tombstone.TenantId.ShouldBe(new TenantId("t"));
        tombstone.Version.ShouldBe(new VersionToken("3"));
        tombstone.DeletedAt.ShouldBe(at);
        tombstone.Purged.ShouldBeTrue();
        tombstone.Generation.ShouldBe(7);
    }

    [Fact]
    public void Constructor_WhenIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryTombstone(default, _agent, new TenantId("t"), new VersionToken("1"), DateTimeOffset.UnixEpoch, false, 1)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryTombstone(_id, default, new TenantId("t"), new VersionToken("1"), DateTimeOffset.UnixEpoch, false, 1)).ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenTenantIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryTombstone(_id, _agent, default, new VersionToken("1"), DateTimeOffset.UnixEpoch, false, 1)).ParamName.ShouldBe("tenantId");

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryTombstone(_id, _agent, new TenantId("t"), default, DateTimeOffset.UnixEpoch, false, 1)).ParamName.ShouldBe("version");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenGenerationIsNotPositive_ThrowsArgumentOutOfRangeException(long generation) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryTombstone(_id, _agent, new TenantId("t"), new VersionToken("1"), DateTimeOffset.UnixEpoch, false, generation)).ParamName.ShouldBe("generation");
}
