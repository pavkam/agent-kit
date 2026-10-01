// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryReadResult"/> factories.</summary>
public sealed class MemoryReadResultTests
{
    [Fact]
    public void Found_WhenRecordIsSupplied_ReportsFound()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        var result = MemoryReadResult.Found(record);

        result.IsFound.ShouldBeTrue();
        result.Record.ShouldBe(record);
        result.Tombstone.ShouldBeNull();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Tombstoned_WhenTombstoneIsSupplied_CarriesNoRecord()
    {
        var tombstone = new MemoryTombstone(new MemoryId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new TenantId("t"), new VersionToken("2"), DateTimeOffset.UnixEpoch, false, 1);

        var result = MemoryReadResult.Tombstoned(tombstone);

        result.IsFound.ShouldBeFalse();
        result.Record.ShouldBeNull();
        result.Tombstone.ShouldBe(tombstone);
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesOnlyTheFailure()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "missing");

        var result = MemoryReadResult.Rejected(failure);

        result.IsFound.ShouldBeFalse();
        result.Failure.ShouldBe(failure);
        result.Tombstone.ShouldBeNull();
    }

    [Fact]
    public void Factories_WhenGivenNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => MemoryReadResult.Found(null!)).ParamName.ShouldBe("record");
        Should.Throw<ArgumentNullException>(() => MemoryReadResult.Tombstoned(null!)).ParamName.ShouldBe("tombstone");
        Should.Throw<ArgumentNullException>(() => MemoryReadResult.Rejected(null!)).ParamName.ShouldBe("failure");
    }
}
