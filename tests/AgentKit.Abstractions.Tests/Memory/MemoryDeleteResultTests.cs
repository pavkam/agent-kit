// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryDeleteResult"/> factories.</summary>
public sealed class MemoryDeleteResultTests
{
    [Fact]
    public void Deleted_WhenReceiptIsSupplied_ReportsDeleted()
    {
        var receipt = new MemoryDeletionReceipt(new MemoryId(Guid.NewGuid()), true, false, ["s"], DateTimeOffset.UnixEpoch, 1);

        var result = MemoryDeleteResult.Deleted(receipt, replayed: true);

        result.IsDeleted.ShouldBeTrue();
        result.Receipt.ShouldBe(receipt);
        result.Replayed.ShouldBeTrue();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Deleted_WhenReceiptIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryDeleteResult.Deleted(null!, false)).ParamName.ShouldBe("receipt");

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoReceipt()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "missing");

        var result = MemoryDeleteResult.Rejected(failure);

        result.IsDeleted.ShouldBeFalse();
        result.Receipt.ShouldBeNull();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryDeleteResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
