// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryWriteRequest"/> constraints.</summary>
public sealed class MemoryWriteRequestTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);
        var grant = MemoryTestData.Grant(owner);

        var request = new MemoryWriteRequest(record, new IdempotencyKey("k"), grant);

        request.Record.ShouldBe(record);
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("k"));
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenRecordIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryWriteRequest(null!, new IdempotencyKey("k"), MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("record");

    [Theory]
    [InlineData(MemoryLifecycleState.Rejected)]
    [InlineData(MemoryLifecycleState.Corrected)]
    [InlineData(MemoryLifecycleState.Deleted)]
    [InlineData(MemoryLifecycleState.Expired)]
    public void Constructor_WhenStateIsTerminal_ThrowsArgumentException(MemoryLifecycleState state)
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => new MemoryWriteRequest(MemoryTestData.Record(owner, state: state), new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("record");
    }

    [Fact]
    public void Constructor_WhenVersionIsNotTheInitialVersion_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => new MemoryWriteRequest(MemoryTestData.Record(owner, version: "2"), new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("record");
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new MemoryWriteRequest(MemoryTestData.Record(owner), default, MemoryTestData.Grant(owner))).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new MemoryWriteRequest(MemoryTestData.Record(owner), new IdempotencyKey("k"), null!)).ParamName.ShouldBe("grant");
    }
}
