// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

/// <summary>Verifies <see cref="DocumentDeleteResult"/> factories.</summary>
public sealed class DocumentDeleteResultTests
{
    [Fact]
    public void Deleted_WhenReceiptIsSupplied_ReportsDeleted()
    {
        var receipt = new DocumentDeletionReceipt(new DocumentId(Guid.NewGuid()), true, false, [], ["s"], DateTimeOffset.UnixEpoch, 1);

        var result = DocumentDeleteResult.Deleted(receipt, replayed: true);

        result.IsDeleted.ShouldBeTrue();
        result.Receipt.ShouldBe(receipt);
        result.Replayed.ShouldBeTrue();
    }

    [Fact]
    public void Deleted_WhenReceiptIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DocumentDeleteResult.Deleted(null!, false)).ParamName.ShouldBe("receipt");

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoReceipt()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "missing");

        var result = DocumentDeleteResult.Rejected(failure);

        result.IsDeleted.ShouldBeFalse();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DocumentDeleteResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
