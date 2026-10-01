// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RetrievalQueryEmbedding"/> constraints.</summary>
public sealed class RetrievalQueryEmbeddingTests
{
    [Fact]
    public void Constructor_WhenVectorMatchesTheSpace_PreservesBoth()
    {
        var space = MemoryTestData.EmbeddingSpace(2);

        var embedding = new RetrievalQueryEmbedding([1f, 0f], space);

        embedding.Vector.ShouldBe([1f, 0f]);
        embedding.Space.ShouldBe(space);
    }

    [Fact]
    public void Constructor_WhenVectorIsDefaultOrEmpty_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new RetrievalQueryEmbedding(default, MemoryTestData.EmbeddingSpace())).ParamName.ShouldBe("vector");
        Should.Throw<ArgumentException>(() => new RetrievalQueryEmbedding([], MemoryTestData.EmbeddingSpace())).ParamName.ShouldBe("vector");
    }

    [Fact]
    public void Constructor_WhenSpaceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalQueryEmbedding([1f], null!)).ParamName.ShouldBe("space");

    [Fact]
    public void Constructor_WhenLengthDiffersFromTheSpaceDimensions_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalQueryEmbedding([1f, 0f], MemoryTestData.EmbeddingSpace(3))).ParamName.ShouldBe("vector");

    [Fact]
    public void Constructor_WhenAComponentIsNotFinite_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RetrievalQueryEmbedding([1f, float.PositiveInfinity], MemoryTestData.EmbeddingSpace(2))).ParamName.ShouldBe("vector");
}
