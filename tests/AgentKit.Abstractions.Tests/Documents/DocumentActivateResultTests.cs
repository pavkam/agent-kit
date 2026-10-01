// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentActivateResult"/> factories.</summary>
public sealed class DocumentActivateResultTests
{
    [Fact]
    public void Activated_WhenRecordIsSupplied_ReportsActivated()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        var result = DocumentActivateResult.Activated(record, new DocumentVersion("v0"), replayed: true);

        result.IsActivated.ShouldBeTrue();
        result.Record.ShouldBe(record);
        result.PreviousActiveVersion.ShouldBe(new DocumentVersion("v0"));
        result.Replayed.ShouldBeTrue();
    }

    [Fact]
    public void Activated_WhenRecordIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DocumentActivateResult.Activated(null!, null, false)).ParamName.ShouldBe("record");

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoRecord()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.VersionConflict, "stale");

        var result = DocumentActivateResult.Rejected(failure);

        result.IsActivated.ShouldBeFalse();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DocumentActivateResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
