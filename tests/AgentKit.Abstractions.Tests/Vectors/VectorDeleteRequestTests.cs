// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="VectorDeleteRequest"/> constraints.</summary>
public sealed class VectorDeleteRequestTests
{
    private static readonly ChunkId _chunk = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();

        var request = new VectorDeleteRequest(MemoryTestData.Space(), [_chunk], new IdempotencyKey("k"), MemoryTestData.Grant(owner));

        request.ChunkIds.ShouldBe([_chunk]);
        request.IdempotencyKey.ShouldBe(new IdempotencyKey("k"));
    }

    [Fact]
    public void Constructor_WhenSpaceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new VectorDeleteRequest(null!, [_chunk], new IdempotencyKey("k"), MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("space");

    [Fact]
    public void Constructor_WhenChunksAreDefaultOrEmpty_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentException>(() => new VectorDeleteRequest(MemoryTestData.Space(), default, new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunkIds");
        Should.Throw<ArgumentException>(() => new VectorDeleteRequest(MemoryTestData.Space(), [], new IdempotencyKey("k"), MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunkIds");
    }

    [Fact]
    public void Constructor_WhenAChunkIdentityIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorDeleteRequest(MemoryTestData.Space(), [default], new IdempotencyKey("k"), MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("chunkIds");

    [Fact]
    public void Constructor_WhenBatchExceedsTheMaximum_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorDeleteRequest(MemoryTestData.Space(), [.. Enumerable.Repeat(_chunk, VectorUpsertRequest.MaximumBatch + 1)], new IdempotencyKey("k"), MemoryTestData.Grant(MemoryTestData.NewOwner()))).ParamName.ShouldBe("chunkIds");

    [Fact]
    public void Constructor_WhenKeyIsDefaultOrGrantIsInvalid_Throws()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new VectorDeleteRequest(MemoryTestData.Space(), [_chunk], default, MemoryTestData.Grant(owner))).ParamName.ShouldBe("idempotencyKey");
        Should.Throw<ArgumentNullException>(() => new VectorDeleteRequest(MemoryTestData.Space(), [_chunk], new IdempotencyKey("k"), null!)).ParamName.ShouldBe("grant");
    }
}
