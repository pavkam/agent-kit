// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies the deterministic chunker's boundaries, identity derivation, and argument constraints.</summary>
public sealed class DeterministicTextChunkerTests
{
    private static readonly DocumentVersion _version = new("v1");

    [Fact]
    public void Chunk_WhenTextIsShort_ReturnsOneChunkWithContentDerivedHash()
    {
        var id = new DocumentId(Guid.NewGuid());

        var chunks = new DeterministicTextChunker().Chunk(id, _version, "Hello world.");

        var chunk = chunks.ShouldHaveSingleItem();
        chunk.Text.ShouldBe("Hello world.");
        chunk.Ordinal.ShouldBe(0);
        chunk.DocumentId.ShouldBe(id);
        chunk.Hash.Value.ShouldStartWith("sha256:");
        chunk.Chunker.ShouldBe(new DeterministicTextChunker().Version);
    }

    [Fact]
    public void Chunk_WhenCalledTwice_ProducesIdenticalChunkIdentities()
    {
        var id = new DocumentId(Guid.NewGuid());
        var chunker = new DeterministicTextChunker();

        var first = chunker.Chunk(id, _version, "One.\n\nTwo.");
        var second = chunker.Chunk(id, _version, "One.\n\nTwo.");

        first.Select(static chunk => chunk.Id).ShouldBe(second.Select(static chunk => chunk.Id));
    }

    [Fact]
    public void Chunk_WhenVersionOrContentChanges_ProducesDifferentIdentities()
    {
        var id = new DocumentId(Guid.NewGuid());
        var chunker = new DeterministicTextChunker();

        var original = chunker.Chunk(id, _version, "Same text.").Single().Id;

        chunker.Chunk(id, new DocumentVersion("v2"), "Same text.").Single().Id.ShouldNotBe(original);
        chunker.Chunk(id, _version, "Other text.").Single().Id.ShouldNotBe(original);
        chunker.Chunk(new DocumentId(Guid.NewGuid()), _version, "Same text.").Single().Id.ShouldNotBe(original);
    }

    [Fact]
    public void Chunk_WhenParagraphsExceedTheBound_PacksWholeParagraphsAndAssignsSequentialOrdinals()
    {
        var paragraph = new string('a', 700);
        var text = $"{paragraph}\n\n{paragraph}\n\n{paragraph}";

        var chunks = new DeterministicTextChunker().Chunk(new DocumentId(Guid.NewGuid()), _version, text);

        chunks.Length.ShouldBe(3);
        chunks.Select(static chunk => chunk.Ordinal).ShouldBe([0, 1, 2]);
        chunks.ShouldAllBe(static chunk => chunk.Text.Length <= DeterministicTextChunker.MaximumChunkCharacters);
    }

    [Fact]
    public void Chunk_WhenASingleParagraphExceedsTheBound_HardSplitsWithoutLosingText()
    {
        var text = new string('x', (DeterministicTextChunker.MaximumChunkCharacters * 2) + 10);

        var chunks = new DeterministicTextChunker().Chunk(new DocumentId(Guid.NewGuid()), _version, text);

        chunks.Length.ShouldBe(3);
        string.Concat(chunks.Select(static chunk => chunk.Text)).ShouldBe(text);
    }

    [Fact]
    public void Chunk_WhenTextIsWhitespace_ReturnsNoChunks() =>
        new DeterministicTextChunker().Chunk(new DocumentId(Guid.NewGuid()), _version, " \n\n  ").ShouldBeEmpty();

    [Fact]
    public void Chunk_WhenArgumentsAreInvalid_Throws()
    {
        var chunker = new DeterministicTextChunker();
        var id = new DocumentId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => chunker.Chunk(default, _version, "x")).ParamName.ShouldBe("documentId");
        Should.Throw<ArgumentException>(() => chunker.Chunk(id, default, "x")).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => chunker.Chunk(id, _version, null!)).ParamName.ShouldBe("text");
    }
}
