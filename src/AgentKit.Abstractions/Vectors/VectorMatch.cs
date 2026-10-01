// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one scored index hit, naming the chunk and the document version it was derived from.</summary>
/// <remarks>A hit is a pointer, not authority: callers filter it against the authoritative active document version before reranking or exposure. Higher scores are better under every metric.</remarks>
public sealed record VectorMatch
{
    /// <summary>Initializes a validated match.</summary>
    /// <param name="chunkId">The matched chunk.</param>
    /// <param name="documentId">The document the chunk belongs to.</param>
    /// <param name="documentVersion">The document version the chunk was cut from.</param>
    /// <param name="score">The finite score; higher is better under every metric.</param>
    /// <param name="sourceHash">The hash of the chunk text the vector was computed from.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or the score is not finite.</exception>
    /// <exception cref="ArgumentException">A version or hash is blank.</exception>
    public VectorMatch(ChunkId chunkId, DocumentId documentId, DocumentVersion documentVersion, double score, ContentHash sourceHash)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(chunkId, default, nameof(chunkId));
        ArgumentOutOfRangeException.ThrowIfEqual(documentId, default, nameof(documentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(documentVersion.Value, nameof(documentVersion));
        if (!double.IsFinite(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "The score must be finite.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHash.Value, nameof(sourceHash));
        ChunkId = chunkId;
        DocumentId = documentId;
        DocumentVersion = documentVersion;
        Score = score;
        SourceHash = sourceHash;
    }

    /// <summary>Gets the matched chunk.</summary>
    public ChunkId ChunkId { get; }

    /// <summary>Gets the document the chunk belongs to.</summary>
    public DocumentId DocumentId { get; }

    /// <summary>Gets the document version the chunk was cut from.</summary>
    public DocumentVersion DocumentVersion { get; }

    /// <summary>Gets the score; higher is better.</summary>
    public double Score { get; }

    /// <summary>Gets the hash of the chunk text the vector was computed from.</summary>
    public ContentHash SourceHash { get; }
}
