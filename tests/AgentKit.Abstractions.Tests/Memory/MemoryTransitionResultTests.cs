// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryTransitionResult"/> factories.</summary>
public sealed class MemoryTransitionResultTests
{
    [Fact]
    public void Transitioned_WhenRecordIsSupplied_ReportsTransitioned()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        var replacement = MemoryTestData.Record(owner);

        var result = MemoryTransitionResult.Transitioned(record, replacement, replayed: true);

        result.IsTransitioned.ShouldBeTrue();
        result.Record.ShouldBe(record);
        result.Replacement.ShouldBe(replacement);
        result.Replayed.ShouldBeTrue();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Transitioned_WhenRecordIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryTransitionResult.Transitioned(null!, null, false)).ParamName.ShouldBe("record");

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoRecord()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.VersionConflict, "stale");

        var result = MemoryTransitionResult.Rejected(failure);

        result.IsTransitioned.ShouldBeFalse();
        result.Record.ShouldBeNull();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Rejected_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryTransitionResult.Rejected(null!)).ParamName.ShouldBe("failure");
}
