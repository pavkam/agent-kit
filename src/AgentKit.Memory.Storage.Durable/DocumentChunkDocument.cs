// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="DocumentChunk"/>.</summary>
/// <param name="Id">The chunk identity.</param>
/// <param name="DocumentId">The document identity.</param>
/// <param name="Version">The source version text.</param>
/// <param name="Chunker">The chunker version text.</param>
/// <param name="Ordinal">The zero-based ordinal.</param>
/// <param name="Text">The chunk text.</param>
/// <param name="Hash">The chunk text hash.</param>
internal sealed record DocumentChunkDocument(Guid Id, Guid DocumentId, string Version, string Chunker, int Ordinal, string Text, string Hash)
{
    /// <summary>Converts a chunk to its persisted form.</summary>
    /// <param name="value">The non-null chunk.</param>
    /// <returns>The document.</returns>
    internal static DocumentChunkDocument FromDomain(DocumentChunk value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Id.Value, value.DocumentId.Value, value.Version.Value, value.Chunker.Value, value.Ordinal, value.Text, value.Hash.Value);
    }

    /// <summary>Restores the chunk, re-running its validation.</summary>
    /// <returns>The chunk.</returns>
    internal DocumentChunk ToDomain() =>
        new(new ChunkId(Id), new DocumentId(DocumentId), new DocumentVersion(Version), new ChunkerVersion(Chunker), Ordinal, Text, new ContentHash(Hash));
}
