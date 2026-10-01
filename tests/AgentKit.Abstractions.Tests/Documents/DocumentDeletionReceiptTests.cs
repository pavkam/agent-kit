// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

/// <summary>Verifies <see cref="DocumentDeletionReceipt"/> constraints that keep logical and physical deletion distinct.</summary>
public sealed class DocumentDeletionReceiptTests
{
    private static readonly DocumentId _id = new(Guid.NewGuid());
    private static readonly ChunkId _chunk = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenLogicallyDeletedWithPendingPurge_NamesChunksAndPendingStores()
    {
        var receipt = new DocumentDeletionReceipt(_id, true, false, [_chunk], ["docs"], DateTimeOffset.UnixEpoch, 4);

        receipt.ChunkIds.ShouldBe([_chunk]);
        receipt.PendingStores.ShouldBe(["docs"]);
        receipt.Generation.ShouldBe(4);
    }

    [Fact]
    public void Constructor_WhenPurged_AcceptsNoPendingStores() =>
        new DocumentDeletionReceipt(_id, true, true, [_chunk], [], DateTimeOffset.UnixEpoch, 1).PhysicallyPurged.ShouldBeTrue();

    [Fact]
    public void Constructor_WhenPurgedButNotLogicallyDeleted_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentDeletionReceipt(_id, false, true, [], [], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("physicallyPurged");

    [Fact]
    public void Constructor_WhenPurgedYetStoresRemainPending_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentDeletionReceipt(_id, true, true, [], ["docs"], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("pendingStores");

    [Fact]
    public void Constructor_WhenListsAreDefault_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new DocumentDeletionReceipt(_id, true, false, default, [], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("chunkIds");
        Should.Throw<ArgumentException>(() => new DocumentDeletionReceipt(_id, true, false, [], default, DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("pendingStores");
    }

    [Fact]
    public void Constructor_WhenAPendingStoreNameIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentDeletionReceipt(_id, true, false, [], [" "], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("pendingStores");

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentDeletionReceipt(default, true, false, [], [], DateTimeOffset.UnixEpoch, 1)).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenGenerationIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentDeletionReceipt(_id, true, false, [], [], DateTimeOffset.UnixEpoch, 0)).ParamName.ShouldBe("generation");
}
