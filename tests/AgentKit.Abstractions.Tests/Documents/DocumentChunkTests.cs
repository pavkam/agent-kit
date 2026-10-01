// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

/// <summary>Verifies <see cref="DocumentChunk"/> constraints.</summary>
public sealed class DocumentChunkTests
{
    private static readonly ChunkId _id = new(Guid.NewGuid());
    private static readonly DocumentId _document = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var chunk = new DocumentChunk(_id, _document, new DocumentVersion("v1"), new ChunkerVersion("c1"), 3, "text", new ContentHash("sha256:aa"));

        chunk.Id.ShouldBe(_id);
        chunk.DocumentId.ShouldBe(_document);
        chunk.Version.ShouldBe(new DocumentVersion("v1"));
        chunk.Chunker.ShouldBe(new ChunkerVersion("c1"));
        chunk.Ordinal.ShouldBe(3);
        chunk.Text.ShouldBe("text");
    }

    [Fact]
    public void Constructor_WhenIdentitiesAreDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentChunk(default, _document, new DocumentVersion("v"), new ChunkerVersion("c"), 0, "t", new ContentHash("h"))).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentChunk(_id, default, new DocumentVersion("v"), new ChunkerVersion("c"), 0, "t", new ContentHash("h"))).ParamName.ShouldBe("documentId");
    }

    [Fact]
    public void Constructor_WhenOrdinalIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentChunk(_id, _document, new DocumentVersion("v"), new ChunkerVersion("c"), -1, "t", new ContentHash("h"))).ParamName.ShouldBe("ordinal");

    [Fact]
    public void Constructor_WhenTextIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentChunk(_id, _document, new DocumentVersion("v"), new ChunkerVersion("c"), 0, " ", new ContentHash("h"))).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenVersionChunkerOrHashIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new DocumentChunk(_id, _document, default, new ChunkerVersion("c"), 0, "t", new ContentHash("h"))).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => new DocumentChunk(_id, _document, new DocumentVersion("v"), default, 0, "t", new ContentHash("h"))).ParamName.ShouldBe("chunker");
        Should.Throw<ArgumentNullException>(() => new DocumentChunk(_id, _document, new DocumentVersion("v"), new ChunkerVersion("c"), 0, "t", default)).ParamName.ShouldBe("hash");
    }
}
