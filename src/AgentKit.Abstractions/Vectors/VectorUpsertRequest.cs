// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a vector index to store or replace a batch of vectors.</summary>
/// <remarks>
/// The request names the complete vector space the vectors were produced in. The index rejects a descriptor that does not
/// match its own before touching state. A batch is applied atomically, bounded by <see cref="MaximumBatch"/>, and replaying the
/// same idempotency key with the same batch is harmless. Upserting an existing chunk replaces its vector.
/// </remarks>
public sealed record VectorUpsertRequest
{
    /// <summary>The largest number of vectors one request may carry.</summary>
    public const int MaximumBatch = 1_000;

    /// <summary>Initializes a validated upsert request.</summary>
    /// <param name="space">The complete vector space of the vectors.</param>
    /// <param name="records">The non-empty batch; every vector must have the space's dimensions and chunk identities must be unique.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The batch is default, empty, contains null, repeats a chunk, or contains a vector of the wrong dimension; the key is blank; or the grant lacks captured authorization.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The batch exceeds <see cref="MaximumBatch"/>.</exception>
    public VectorUpsertRequest(VectorSpaceDescriptor space, ImmutableArray<VectorRecord> records, IdempotencyKey idempotencyKey, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentException.ThrowIfDefaultOrEmpty(records, nameof(records));
        ArgumentException.ThrowIfContainsNull(records, nameof(records));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(records.Length, MaximumBatch, nameof(records));
        var seen = new HashSet<ChunkId>();
        foreach (var record in records)
        {
            if (record.Vector.Length != space.Dimensions || !seen.Add(record.ChunkId))
            {
                throw new ArgumentException("Every vector must have the space's dimensions and each chunk may appear once.", nameof(records));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Space = space;
        Records = records;
        IdempotencyKey = idempotencyKey;
        Grant = grant;
    }

    /// <summary>Gets the complete vector space of the vectors.</summary>
    public VectorSpaceDescriptor Space { get; }

    /// <summary>Gets the batch of vectors.</summary>
    public ImmutableArray<VectorRecord> Records { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
