// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="VectorUpsertRequest"/> constraints.</summary>
public sealed class VectorUpsertRequestTests
{
    [Fact]
    public void Constructor_WhenBatchMatchesTheSpace_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var chunks = MemoryTestData.Chunks(MemoryTestData.Document(owner), 2);
        var records = ImmutableArray.Create(MemoryTestData.Vector(owner, chunks[0], 1, 0, 0), MemoryTestData.Vector(owner, chunks[1], 0, 1, 0));
        var space = MemoryTestData.Space();

        var request = new VectorUpsertRequest(space, records, new IdempotencyKey("k"), MemoryTestData.Grant(owner));

        request.Space.ShouldBe(space);
        request.Records.ShouldBe(records);
    }

    [Fact]
    public void Constructor_WhenSpaceIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new VectorUpsertRequest(null!, [], new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("space");
    }

    [Fact]
    public void Constructor_WhenBatchIsDefaultOrEmpty_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => new VectorUpsertRequest(MemoryTestData.Space(), default, new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("records");
        Should.Throw<ArgumentException>(() => new VectorUpsertRequest(MemoryTestData.Space(), [], new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("records");
    }

    [Fact]
    public void Constructor_WhenAVectorHasTheWrongDimension_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];

        Should.Throw<ArgumentException>(() => new VectorUpsertRequest(MemoryTestData.Space(dimensions: 3), [MemoryTestData.Vector(owner, chunk, 1, 2)], new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("records");
    }

    [Fact]
    public void Constructor_WhenAChunkRepeats_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        var record = MemoryTestData.Vector(owner, chunk, 1, 0, 0);

        Should.Throw<ArgumentException>(() => new VectorUpsertRequest(MemoryTestData.Space(), [record, record], new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("records");
    }

    [Fact]
    public void Constructor_WhenBatchExceedsTheMaximum_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        var record = MemoryTestData.Vector(owner, chunk, 1, 0, 0);

        Should.Throw<ArgumentOutOfRangeException>(() => new VectorUpsertRequest(MemoryTestData.Space(), [.. Enumerable.Repeat(record, VectorUpsertRequest.MaximumBatch + 1)], new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("records");
    }

    [Fact]
    public void Constructor_WhenKeyIsDefaultOrGrantIsInvalid_Throws()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        ImmutableArray<VectorRecord> records = [MemoryTestData.Vector(owner, chunk, 1, 0, 0)];

        Should.Throw<ArgumentNullException>(() => new VectorUpsertRequest(MemoryTestData.Space(), records, default, MemoryTestData.Grant(owner))).ParamName.ShouldBe("idempotencyKey");
        Should.Throw<ArgumentNullException>(() => new VectorUpsertRequest(MemoryTestData.Space(), records, new IdempotencyKey("k"), null!)).ParamName.ShouldBe("grant");
    }
}
