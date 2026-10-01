// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one deterministic, ordered slice of one document version.</summary>
/// <remarks>The identity derives from the source version, source content, and <see cref="Chunker"/>, never from random state, so re-chunking the same source with the same chunker reproduces the same chunk set and a different chunker cannot collide with it. Chunk text is untrusted data.</remarks>
public sealed record DocumentChunk
{
    /// <summary>Initializes a validated chunk.</summary>
    /// <param name="id">The deterministic chunk identity.</param>
    /// <param name="documentId">The document the chunk belongs to.</param>
    /// <param name="version">The source version the chunk was cut from.</param>
    /// <param name="chunker">The chunker that produced the chunk.</param>
    /// <param name="ordinal">The zero-based position within the version's chunk set.</param>
    /// <param name="text">The non-blank chunk text.</param>
    /// <param name="hash">The hash of <paramref name="text"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or the ordinal is negative.</exception>
    /// <exception cref="ArgumentException">A version, chunker, text, or hash is blank.</exception>
    public DocumentChunk(ChunkId id, DocumentId documentId, DocumentVersion version, ChunkerVersion chunker, int ordinal, string text, ContentHash hash)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(documentId, default, nameof(documentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(chunker.Value, nameof(chunker));
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash.Value, nameof(hash));
        Id = id;
        DocumentId = documentId;
        Version = version;
        Chunker = chunker;
        Ordinal = ordinal;
        Text = text;
        Hash = hash;
    }

    /// <summary>Gets the deterministic chunk identity.</summary>
    public ChunkId Id { get; }

    /// <summary>Gets the document the chunk belongs to.</summary>
    public DocumentId DocumentId { get; }

    /// <summary>Gets the source version the chunk was cut from.</summary>
    public DocumentVersion Version { get; }

    /// <summary>Gets the chunker that produced the chunk.</summary>
    public ChunkerVersion Chunker { get; }

    /// <summary>Gets the zero-based position within the version's chunk set.</summary>
    public int Ordinal { get; }

    /// <summary>Gets the chunk text.</summary>
    public string Text { get; }

    /// <summary>Gets the hash of <see cref="Text"/>.</summary>
    public ContentHash Hash { get; }
}
