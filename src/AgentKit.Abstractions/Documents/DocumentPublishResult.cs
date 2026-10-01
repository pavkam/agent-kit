// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of publishing a document version: the activated record and indexing counts, or a typed refusal.</summary>
public sealed record DocumentPublishResult
{
    private DocumentPublishResult(DocumentRecord? record, int chunkCount, int vectorsIndexed, DocumentVersion? previousActive, bool replayed, MemoryStoreFailure? failure)
    {
        Record = record;
        ChunkCount = chunkCount;
        VectorsIndexed = vectorsIndexed;
        PreviousActiveVersion = previousActive;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the activated document record, or <see langword="null"/> when refused.</summary>
    public DocumentRecord? Record { get; }

    /// <summary>Gets the number of chunks the version was split into.</summary>
    public int ChunkCount { get; }

    /// <summary>Gets the number of vectors upserted across every index.</summary>
    public int VectorsIndexed { get; }

    /// <summary>Gets the version that was active before this publication, or <see langword="null"/>.</summary>
    public DocumentVersion? PreviousActiveVersion { get; }

    /// <summary>Gets a value indicating whether the result came from an idempotent replay.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the version was published.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the version was published and activated.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsPublished => Record is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="record">The activated record.</param>
    /// <param name="chunkCount">The number of chunks.</param>
    /// <param name="vectorsIndexed">The number of vectors upserted across every index.</param>
    /// <param name="previousActive">The previously active version, or <see langword="null"/>.</param>
    /// <param name="replayed">Whether the result came from an idempotent replay.</param>
    /// <returns>A published result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public static DocumentPublishResult Published(DocumentRecord record, int chunkCount, int vectorsIndexed, DocumentVersion? previousActive, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);
        ArgumentOutOfRangeException.ThrowIfNegative(vectorsIndexed);
        return new(record, chunkCount, vectorsIndexed, previousActive, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static DocumentPublishResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, 0, 0, null, false, failure);
    }
}
