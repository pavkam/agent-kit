// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="VectorRecord"/> constraints and by-value equality.</summary>
public sealed class VectorRecordTests
{
    private static VectorRecord Create(ImmutableArray<float> vector, MemoryTestOwner? owner = null)
    {
        owner ??= MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        return new(chunk.Id, chunk.DocumentId, chunk.Version, owner.AgentId, new PrincipalVisibility(owner.Identity.TenantId, owner.Identity.PrincipalId), vector, chunk.Hash, chunk.Chunker, MemoryTestData.Now);
    }

    [Fact]
    public void Constructor_WhenValid_PreservesTheVector() =>
        Create([1f, 2f, 3f]).Vector.ShouldBe([1f, 2f, 3f]);

    [Fact]
    public void Constructor_WhenVectorIsDefaultOrEmpty_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Create(default)).ParamName.ShouldBe("vector");
        Should.Throw<ArgumentException>(() => Create([])).ParamName.ShouldBe("vector");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_WhenAComponentIsNotFinite_ThrowsArgumentException(float component) =>
        Should.Throw<ArgumentException>(() => Create([1f, component])).ParamName.ShouldBe("vector");

    [Fact]
    public void Constructor_WhenIdentitiesAreDefault_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        var visibility = new PrincipalVisibility(owner.Identity.TenantId, owner.Identity.PrincipalId);

        Should.Throw<ArgumentOutOfRangeException>(() => new VectorRecord(default, chunk.DocumentId, chunk.Version, owner.AgentId, visibility, [1f], chunk.Hash, chunk.Chunker, MemoryTestData.Now)).ParamName.ShouldBe("chunkId");
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorRecord(chunk.Id, default, chunk.Version, owner.AgentId, visibility, [1f], chunk.Hash, chunk.Chunker, MemoryTestData.Now)).ParamName.ShouldBe("documentId");
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorRecord(chunk.Id, chunk.DocumentId, chunk.Version, default, visibility, [1f], chunk.Hash, chunk.Chunker, MemoryTestData.Now)).ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenVisibilityIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];

        Should.Throw<ArgumentNullException>(() => new VectorRecord(chunk.Id, chunk.DocumentId, chunk.Version, owner.AgentId, null!, [1f], chunk.Hash, chunk.Chunker, MemoryTestData.Now)).ParamName.ShouldBe("visibility");
    }

    [Fact]
    public void Equality_WhenVectorsMatchByComponent_IsEqualAndDiffersOtherwise()
    {
        var owner = MemoryTestData.NewOwner();
        var first = Create([1f, 2f], owner);
        var same = new VectorRecord(first.ChunkId, first.DocumentId, first.DocumentVersion, first.AgentId, first.Visibility, [1f, 2f], first.SourceHash, first.Chunker, first.CreatedAt);
        var different = new VectorRecord(first.ChunkId, first.DocumentId, first.DocumentVersion, first.AgentId, first.Visibility, [1f, 3f], first.SourceHash, first.Chunker, first.CreatedAt);

        first.Equals(same).ShouldBeTrue();
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.Equals(different).ShouldBeFalse();
        first.Equals(null).ShouldBeFalse();
    }
}
