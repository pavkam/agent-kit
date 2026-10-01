// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Cuts one document version's text into a deterministic, ordered chunk set.</summary>
/// <remarks>
/// Chunking is pure and deterministic: the same document identity, version, text, and <see cref="Version"/> always produce the
/// same chunks with the same identities. Implementations are thread-safe and perform no I/O, so the operation is synchronous
/// and bounded by the caller's text size.
/// </remarks>
public interface IDocumentChunker
{
    /// <summary>Gets the exact algorithm and configuration identity folded into every chunk identity.</summary>
    /// <value>A non-blank version that changes whenever the produced chunk set would change.</value>
    public ChunkerVersion Version { get; }

    /// <summary>Cuts a document version into chunks.</summary>
    /// <param name="documentId">The document the text belongs to.</param>
    /// <param name="version">The source version.</param>
    /// <param name="text">The non-blank source text.</param>
    /// <returns>A non-empty chunk set with contiguous zero-based ordinals and deterministic identities.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="documentId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="version"/> or <paramref name="text"/> is blank.</exception>
    public ImmutableArray<DocumentChunk> Chunk(DocumentId documentId, DocumentVersion version, string text);
}
