// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryDeletionReceipt"/> constraints that keep logical and physical deletion distinct.</summary>
public sealed class MemoryDeletionReceiptTests
{
    private static readonly MemoryId _id = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenLogicallyDeletedWithPendingPurge_NamesThePendingStores()
    {
        var receipt = new MemoryDeletionReceipt(_id, true, false, ["store-a"], DateTimeOffset.UnixEpoch, 3);

        receipt.LogicallyDeleted.ShouldBeTrue();
        receipt.PhysicallyPurged.ShouldBeFalse();
        receipt.PendingStores.ShouldBe(["store-a"]);
        receipt.Generation.ShouldBe(3);
    }

    [Fact]
    public void Constructor_WhenPurged_AcceptsNoPendingStores() =>
        new MemoryDeletionReceipt(_id, true, true, [], DateTimeOffset.UnixEpoch, 1).PhysicallyPurged.ShouldBeTrue();

    [Fact]
    public void Constructor_WhenPurgedButNotLogicallyDeleted_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryDeletionReceipt(_id, false, true, [], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("physicallyPurged");

    [Fact]
    public void Constructor_WhenPurgedYetStoresRemainPending_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryDeletionReceipt(_id, true, true, ["store-a"], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("pendingStores");

    [Fact]
    public void Constructor_WhenPendingStoresAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryDeletionReceipt(_id, true, false, default, DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("pendingStores");

    [Fact]
    public void Constructor_WhenAPendingStoreNameIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryDeletionReceipt(_id, true, false, [" "], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("pendingStores");

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryDeletionReceipt(default, true, false, [], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenGenerationIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryDeletionReceipt(_id, true, false, [], DateTimeOffset.UnixEpoch, 0)).ParamName.ShouldBe("generation");
}
