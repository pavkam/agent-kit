// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentReadResult"/> factories.</summary>
public sealed class DocumentReadResultTests
{
    [Fact]
    public void Found_WhenChunksAreOmitted_ReportsFoundWithNoChunks()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        var result = DocumentReadResult.Found(record, DocumentVersionState.Active, default, record.Version);

        result.IsFound.ShouldBeTrue();
        result.Chunks.ShouldBeEmpty();
        result.ActiveVersion.ShouldBe(record.Version);
        result.State.ShouldBe(DocumentVersionState.Active);
        result.Tombstone.ShouldBeNull();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Found_WhenChunksAreSupplied_CarriesThemInOrder()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());
        var chunks = MemoryTestData.Chunks(record, 3);

        DocumentReadResult.Found(record, DocumentVersionState.Staged, chunks, null).Chunks.ShouldBe(chunks);
    }

    [Fact]
    public void Found_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentNullException>(() => DocumentReadResult.Found(null!, DocumentVersionState.Active, default, null)).ParamName.ShouldBe("record");
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentReadResult.Found(MemoryTestData.Document(MemoryTestData.NewOwner()), (DocumentVersionState) 9, default, null)).ParamName.ShouldBe("state");
    }

    [Fact]
    public void Tombstoned_WhenReceiptIsSupplied_CarriesNoRecord()
    {
        var receipt = new DocumentDeletionReceipt(new DocumentId(Guid.NewGuid()), true, false, [], ["s"], DateTimeOffset.UnixEpoch, 1);

        var result = DocumentReadResult.Tombstoned(receipt);

        result.IsFound.ShouldBeFalse();
        result.Tombstone.ShouldBe(receipt);
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesOnlyTheFailure()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "missing");

        DocumentReadResult.Rejected(failure).Failure.ShouldBe(failure);
    }

    [Fact]
    public void Factories_WhenGivenNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => DocumentReadResult.Tombstoned(null!)).ParamName.ShouldBe("tombstone");
        Should.Throw<ArgumentNullException>(() => DocumentReadResult.Rejected(null!)).ParamName.ShouldBe("failure");
    }
}
