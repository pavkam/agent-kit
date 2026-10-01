// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Vectors;

/// <summary>Verifies <see cref="VectorMatch"/> constraints.</summary>
public sealed class VectorMatchTests
{
    private static readonly ChunkId _chunk = new(Guid.NewGuid());
    private static readonly DocumentId _document = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var match = new VectorMatch(_chunk, _document, new DocumentVersion("v1"), 0.5, new ContentHash("h"));

        match.ChunkId.ShouldBe(_chunk);
        match.DocumentId.ShouldBe(_document);
        match.DocumentVersion.ShouldBe(new DocumentVersion("v1"));
        match.Score.ShouldBe(0.5);
        match.SourceHash.ShouldBe(new ContentHash("h"));
    }

    [Fact]
    public void Constructor_WhenIdentitiesAreDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorMatch(default, _document, new DocumentVersion("v"), 1, new ContentHash("h"))).ParamName.ShouldBe("chunkId");
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorMatch(_chunk, default, new DocumentVersion("v"), 1, new ContentHash("h"))).ParamName.ShouldBe("documentId");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WhenScoreIsNotFinite_ThrowsArgumentOutOfRangeException(double score) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new VectorMatch(_chunk, _document, new DocumentVersion("v"), score, new ContentHash("h"))).ParamName.ShouldBe("score");

    [Fact]
    public void Constructor_WhenVersionOrHashIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new VectorMatch(_chunk, _document, default, 1, new ContentHash("h"))).ParamName.ShouldBe("documentVersion");
        Should.Throw<ArgumentNullException>(() => new VectorMatch(_chunk, _document, new DocumentVersion("v"), 1, default)).ParamName.ShouldBe("sourceHash");
    }
}
