// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryDeleteCommand"/> constraints.</summary>
public sealed class MemoryDeleteCommandTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new MemoryId(Guid.NewGuid());

        var command = new MemoryDeleteCommand(owner.Context, id, new VersionToken("3"), MemoryDeleteMode.Purge, new IdempotencyKey("k"));

        command.Context.ShouldBe(owner.Context);
        command.Id.ShouldBe(id);
        command.ExpectedVersion.ShouldBe(new VersionToken("3"));
        command.Mode.ShouldBe(MemoryDeleteMode.Purge);
    }

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryDeleteCommand(null!, new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, new IdempotencyKey("k"))).ParamName.ShouldBe("context");

    [Fact]
    public void Constructor_WhenIdIsDefaultOrModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryDeleteCommand(owner.Context, default, null, MemoryDeleteMode.Tombstone, new IdempotencyKey("k"))).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryDeleteCommand(owner.Context, new MemoryId(Guid.NewGuid()), null, (MemoryDeleteMode) 9, new IdempotencyKey("k"))).ParamName.ShouldBe("mode");
    }

    [Fact]
    public void Constructor_WhenVersionOrKeyIsDefault_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new MemoryDeleteCommand(owner.Context, new MemoryId(Guid.NewGuid()), default(VersionToken), MemoryDeleteMode.Tombstone, new IdempotencyKey("k"))).ParamName.ShouldBe("expectedVersion");
        Should.Throw<ArgumentNullException>(() => new MemoryDeleteCommand(owner.Context, new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, default)).ParamName.ShouldBe("idempotencyKey");
    }
}
