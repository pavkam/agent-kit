// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryWriteResult"/> factories.</summary>
public sealed class MemoryWriteResultTests
{
    [Fact]
    public void Written_WhenRecordIsSupplied_ReportsWrittenWithoutFailure()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        var result = MemoryWriteResult.Written(record, replayed: true);

        result.IsWritten.ShouldBeTrue();
        result.Record.ShouldBe(record);
        result.Replayed.ShouldBeTrue();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Written_WhenRecordIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryWriteResult.Written(null!, false)).ParamName.ShouldBe("record");

    [Fact]
    public void Rejected_WhenFailureIsSupplied_ReportsNotWrittenWithoutARecord()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "no");

        var result = MemoryWriteResult.Rejected(failure);

        result.IsWritten.ShouldBeFalse();
        result.Record.ShouldBeNull();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryWriteResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
