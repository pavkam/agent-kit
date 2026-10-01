// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a document store to publish one complete versioned chunk set for a document.</summary>
/// <remarks>
/// <para>
/// A source update creates a complete versioned chunk set. The store keeps the set atomically and, when
/// <see cref="Activate"/> is set, switches the document's active-version pointer to it in the same step, so retrieval never
/// observes a mixed set. Without activation the version is staged until a separate activation. Publishing the same version
/// again with identical content and chunks replays; any difference is an idempotency conflict.
/// </para>
/// <para>The chunk set must belong to the record's document and version, share one chunker, and have contiguous zero-based ordinals and unique identities.</para>
/// </remarks>
public sealed record DocumentWriteRequest
{
    /// <summary>The largest number of chunks one version may declare.</summary>
    public const int MaximumChunks = 10_000;

    /// <summary>Initializes a validated write request.</summary>
    /// <param name="record">The version's record.</param>
    /// <param name="chunks">The complete chunk set.</param>
    /// <param name="activate">Whether to switch the active-version pointer to this version atomically.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="at">The publication instant, taken from the injected clock.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The chunk set is default, empty, or inconsistent with the record, the key is blank, or the grant lacks captured authorization.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The chunk set exceeds <see cref="MaximumChunks"/>.</exception>
    public DocumentWriteRequest(
        DocumentRecord record,
        ImmutableArray<DocumentChunk> chunks,
        bool activate,
        IdempotencyKey idempotencyKey,
        DateTimeOffset at,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfDefaultOrEmpty(chunks, nameof(chunks));
        ArgumentException.ThrowIfContainsNull(chunks, nameof(chunks));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunks.Length, MaximumChunks, nameof(chunks));
        var ids = new HashSet<ChunkId>();
        for (var index = 0; index < chunks.Length; index++)
        {
            var chunk = chunks[index];
            if (chunk.DocumentId != record.Id
                || chunk.Version != record.Version
                || chunk.Chunker != chunks[0].Chunker
                || chunk.Ordinal != index
                || !ids.Add(chunk.Id))
            {
                throw new ArgumentException(
                    "The chunk set must belong to the record's document and version, share one chunker, and have contiguous ordinals and unique identities.",
                    nameof(chunks));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Record = record;
        Chunks = chunks;
        Activate = activate;
        IdempotencyKey = idempotencyKey;
        At = at;
        Grant = grant;
    }

    /// <summary>Gets the version's record.</summary>
    public DocumentRecord Record { get; }

    /// <summary>Gets the complete chunk set.</summary>
    public ImmutableArray<DocumentChunk> Chunks { get; }

    /// <summary>Gets a value indicating whether the active-version pointer switches to this version atomically.</summary>
    public bool Activate { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the publication instant.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
