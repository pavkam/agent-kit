// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one authorized retrieval result: data with identity, provenance, trust, score, and classification.</summary>
/// <remarks>
/// Candidates are data, never instructions. Identities and provenance survive rewriting, reranking, deduplication, trimming,
/// and later context-manifest selection, and a candidate's trust never rises above
/// <see cref="TrustClassification.UntrustedData"/> when it reaches model context.
/// </remarks>
public sealed record RetrievalCandidate
{
    /// <summary>Initializes a validated candidate.</summary>
    /// <param name="requestId">The retrieval request that produced the candidate.</param>
    /// <param name="source">The source that produced it.</param>
    /// <param name="memoryId">The durable memory it came from, or <see langword="null"/>.</param>
    /// <param name="documentId">The document it came from, or <see langword="null"/>.</param>
    /// <param name="chunkId">The chunk it came from, or <see langword="null"/>.</param>
    /// <param name="content">The candidate text.</param>
    /// <param name="provenance">The source evidence.</param>
    /// <param name="trust">The trust classification.</param>
    /// <param name="score">The finite relevance score; higher is better.</param>
    /// <param name="classification">The sensitivity.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, an enumeration is undefined, or the score is not finite.</exception>
    /// <exception cref="ArgumentException">The candidate names neither a memory nor a document, names a chunk without a document, or names both a memory and a document.</exception>
    public RetrievalCandidate(
        RetrievalRequestId requestId,
        RetrievalSourceIdentity source,
        MemoryId? memoryId,
        DocumentId? documentId,
        ChunkId? chunkId,
        CandidateContent content,
        Provenance provenance,
        TrustClassification trust,
        double score,
        DataClassification classification)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default, nameof(requestId));
        ArgumentNullException.ThrowIfNull(source);
        if (memoryId is { } memory)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(memory, default, nameof(memoryId));
        }

        if (documentId is { } document)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(document, default, nameof(documentId));
        }

        if (chunkId is { } chunk)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(chunk, default, nameof(chunkId));
        }

        if ((memoryId is null) == (documentId is null) || (chunkId is not null && documentId is null))
        {
            throw new ArgumentException("A candidate names exactly one of a memory or a document, and a chunk only with its document.", nameof(memoryId));
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentOutOfRangeException.ThrowIfUndefined(trust);
        if (!double.IsFinite(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "The score must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        RequestId = requestId;
        Source = source;
        MemoryId = memoryId;
        DocumentId = documentId;
        ChunkId = chunkId;
        Content = content;
        Provenance = provenance;
        Trust = trust;
        Score = score;
        Classification = classification;
    }

    /// <summary>Gets the retrieval request that produced the candidate.</summary>
    public RetrievalRequestId RequestId { get; }

    /// <summary>Gets the source that produced the candidate.</summary>
    public RetrievalSourceIdentity Source { get; }

    /// <summary>Gets the durable memory the candidate came from, or <see langword="null"/>.</summary>
    public MemoryId? MemoryId { get; }

    /// <summary>Gets the document the candidate came from, or <see langword="null"/>.</summary>
    public DocumentId? DocumentId { get; }

    /// <summary>Gets the chunk the candidate came from, or <see langword="null"/>.</summary>
    public ChunkId? ChunkId { get; }

    /// <summary>Gets the candidate text.</summary>
    public CandidateContent Content { get; }

    /// <summary>Gets the source evidence.</summary>
    public Provenance Provenance { get; }

    /// <summary>Gets the trust classification.</summary>
    public TrustClassification Trust { get; }

    /// <summary>Gets the relevance score; higher is better.</summary>
    public double Score { get; }

    /// <summary>Gets the sensitivity.</summary>
    public DataClassification Classification { get; }

    /// <summary>Creates the candidate with a different score and the same identity, content, and provenance.</summary>
    /// <param name="score">The replacement finite score.</param>
    /// <returns>A copy carrying <paramref name="score"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score"/> is not finite.</exception>
    public RetrievalCandidate WithScore(double score) =>
        new(RequestId, Source, MemoryId, DocumentId, ChunkId, Content, Provenance, Trust, score, Classification);
}
