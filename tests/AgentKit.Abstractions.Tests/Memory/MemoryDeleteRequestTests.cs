// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryDeleteRequest"/> constraints.</summary>
public sealed class MemoryDeleteRequestTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new MemoryId(Guid.NewGuid());

        var request = new MemoryDeleteRequest(id, new VersionToken("2"), MemoryDeleteMode.Purge, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner));

        request.Id.ShouldBe(id);
        request.ExpectedVersion.ShouldBe(new VersionToken("2"));
        request.Mode.ShouldBe(MemoryDeleteMode.Purge);
        request.At.ShouldBe(MemoryTestData.Now);
    }

    [Fact]
    public void Constructor_WhenNoVersionIsExpected_AcceptsNull()
    {
        var owner = MemoryTestData.NewOwner();

        new MemoryDeleteRequest(new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner)).ExpectedVersion.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryDeleteRequest(default, null, MemoryDeleteMode.Tombstone, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenExpectedVersionIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryDeleteRequest(new MemoryId(Guid.NewGuid()), default(VersionToken), MemoryDeleteMode.Tombstone, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("expectedVersion");

    [Fact]
    public void Constructor_WhenModeIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryDeleteRequest(new MemoryId(Guid.NewGuid()), null, (MemoryDeleteMode) 9, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("mode");

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryDeleteRequest(new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, default, MemoryTestData.Now, MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new MemoryDeleteRequest(new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, new IdempotencyKey("k"), MemoryTestData.Now, null!)).ParamName.ShouldBe("grant");
    }
}
