// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentWriteResult"/> factories.</summary>
public sealed class DocumentWriteResultTests
{
    [Fact]
    public void Written_WhenValid_ReportsWrittenWithStateAndPreviousVersion()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        var result = DocumentWriteResult.Written(record, DocumentVersionState.Active, new DocumentVersion("v0"), replayed: true);

        result.IsWritten.ShouldBeTrue();
        result.Record.ShouldBe(record);
        result.State.ShouldBe(DocumentVersionState.Active);
        result.PreviousActiveVersion.ShouldBe(new DocumentVersion("v0"));
        result.Replayed.ShouldBeTrue();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Written_WhenRecordIsNullOrStateIsUndefined_Throws()
    {
        Should.Throw<ArgumentNullException>(() => DocumentWriteResult.Written(null!, DocumentVersionState.Staged, null, false)).ParamName.ShouldBe("record");
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentWriteResult.Written(MemoryTestData.Document(MemoryTestData.NewOwner()), (DocumentVersionState) 9, null, false)).ParamName.ShouldBe("state");
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoRecord()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "no");

        var result = DocumentWriteResult.Rejected(failure);

        result.IsWritten.ShouldBeFalse();
        result.Record.ShouldBeNull();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DocumentWriteResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
