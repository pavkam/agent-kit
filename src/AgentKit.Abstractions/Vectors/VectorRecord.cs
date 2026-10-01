// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one stored vector, bound to the chunk it was derived from and to its owner and visibility.</summary>
/// <remarks>A vector records its source hash and chunker so a stale vector can be recognized against the authoritative active document version. Its embedding space is the space of the index that holds it, never a per-record claim.</remarks>
public sealed record VectorRecord
{
    /// <summary>Initializes a validated vector record.</summary>
    /// <param name="chunkId">The chunk the vector was derived from; it is the record's identity within the index.</param>
    /// <param name="documentId">The document the chunk belongs to.</param>
    /// <param name="documentVersion">The document version the chunk was cut from.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="visibility">The principal visibility of the source.</param>
    /// <param name="vector">The non-empty vector of finite components.</param>
    /// <param name="sourceHash">The hash of the chunk text the vector was computed from.</param>
    /// <param name="chunker">The chunker that produced the chunk.</param>
    /// <param name="createdAt">The instant the vector was produced.</param>
    /// <exception cref="ArgumentNullException"><paramref name="visibility"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentException">The vector is default, empty, or has a non-finite component, or a version, hash, or chunker is blank.</exception>
    public VectorRecord(
        ChunkId chunkId,
        DocumentId documentId,
        DocumentVersion documentVersion,
        AgentId agentId,
        PrincipalVisibility visibility,
        ImmutableArray<float> vector,
        ContentHash sourceHash,
        ChunkerVersion chunker,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(chunkId, default, nameof(chunkId));
        ArgumentOutOfRangeException.ThrowIfEqual(documentId, default, nameof(documentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(documentVersion.Value, nameof(documentVersion));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentException.ThrowIfDefaultOrEmpty(vector, nameof(vector));
        foreach (var component in vector)
        {
            if (!float.IsFinite(component))
            {
                throw new ArgumentException("Vector components must be finite.", nameof(vector));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHash.Value, nameof(sourceHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(chunker.Value, nameof(chunker));
        ChunkId = chunkId;
        DocumentId = documentId;
        DocumentVersion = documentVersion;
        AgentId = agentId;
        Visibility = visibility;
        Vector = vector;
        SourceHash = sourceHash;
        Chunker = chunker;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the chunk the vector was derived from.</summary>
    public ChunkId ChunkId { get; }

    /// <summary>Gets the document the chunk belongs to.</summary>
    public DocumentId DocumentId { get; }

    /// <summary>Gets the document version the chunk was cut from.</summary>
    public DocumentVersion DocumentVersion { get; }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the principal visibility of the source.</summary>
    public PrincipalVisibility Visibility { get; }

    /// <summary>Gets the vector components.</summary>
    public ImmutableArray<float> Vector { get; }

    /// <summary>Gets the hash of the chunk text the vector was computed from.</summary>
    public ContentHash SourceHash { get; }

    /// <summary>Gets the chunker that produced the chunk.</summary>
    public ChunkerVersion Chunker { get; }

    /// <summary>Gets the instant the vector was produced.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Determines whether another record is identical, comparing vector components by value.</summary>
    /// <param name="other">The record to compare.</param>
    /// <returns><see langword="true"/> when every field, including each vector component, is equal.</returns>
    public bool Equals(VectorRecord? other) =>
        other is not null
        && ChunkId == other.ChunkId
        && DocumentId == other.DocumentId
        && DocumentVersion == other.DocumentVersion
        && AgentId == other.AgentId
        && Visibility == other.Visibility
        && Vector.AsSpan().SequenceEqual(other.Vector.AsSpan())
        && SourceHash == other.SourceHash
        && Chunker == other.Chunker
        && CreatedAt == other.CreatedAt;

    /// <summary>Returns a hash code consistent with <see cref="Equals(VectorRecord?)"/>.</summary>
    /// <returns>A hash over every field including the vector components.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ChunkId);
        hash.Add(DocumentId);
        hash.Add(DocumentVersion);
        hash.Add(AgentId);
        hash.Add(Visibility);
        foreach (var component in Vector)
        {
            hash.Add(component);
        }

        hash.Add(SourceHash);
        hash.Add(Chunker);
        hash.Add(CreatedAt);
        return hash.ToHashCode();
    }
}
