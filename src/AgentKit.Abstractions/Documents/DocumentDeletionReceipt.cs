// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records a document deletion, separating logical invisibility from physical purge, without returning deleted content.</summary>
/// <remarks>The receipt names the chunk identities whose vectors must be removed from every vector index and the stores whose cleanup is still pending. Already transmitted data cannot be recalled.</remarks>
public sealed record DocumentDeletionReceipt
{
    /// <summary>Initializes a validated receipt.</summary>
    /// <param name="id">The deleted document identity.</param>
    /// <param name="logicallyDeleted">Whether every version is authoritatively invisible to new retrieval and exposure.</param>
    /// <param name="physicallyPurged">Whether the stored chunks and content were physically removed.</param>
    /// <param name="chunkIds">The identities of every chunk of every version, for propagating deletion to indexes.</param>
    /// <param name="pendingStores">The non-blank names of stores whose purge is still pending; empty once purged.</param>
    /// <param name="deletedAt">The instant of logical deletion.</param>
    /// <param name="generation">The store-wide deletion generation assigned at logical deletion.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or the generation is not positive.</exception>
    /// <exception cref="ArgumentException">A list is default, a purged receipt is not logically deleted or names pending stores, or a name is blank.</exception>
    public DocumentDeletionReceipt(
        DocumentId id,
        bool logicallyDeleted,
        bool physicallyPurged,
        ImmutableArray<ChunkId> chunkIds,
        ImmutableArray<string> pendingStores,
        DateTimeOffset deletedAt,
        long generation)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNotEqual(!physicallyPurged || logicallyDeleted, true, nameof(physicallyPurged));
        ArgumentException.ThrowIfDefault(chunkIds);
        ArgumentException.ThrowIfDefault(pendingStores);
        foreach (var store in pendingStores)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(store, nameof(pendingStores));
        }

        ArgumentException.ThrowIfNotEqual(!physicallyPurged || pendingStores.IsEmpty, true, nameof(pendingStores));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);
        Id = id;
        LogicallyDeleted = logicallyDeleted;
        PhysicallyPurged = physicallyPurged;
        ChunkIds = chunkIds;
        PendingStores = pendingStores;
        DeletedAt = deletedAt;
        Generation = generation;
    }

    /// <summary>Gets the deleted document identity.</summary>
    public DocumentId Id { get; }

    /// <summary>Gets a value indicating whether every version is authoritatively invisible.</summary>
    public bool LogicallyDeleted { get; }

    /// <summary>Gets a value indicating whether the stored chunks and content were physically removed.</summary>
    public bool PhysicallyPurged { get; }

    /// <summary>Gets the identities of every chunk of every version.</summary>
    public ImmutableArray<ChunkId> ChunkIds { get; }

    /// <summary>Gets the names of stores whose purge is still pending.</summary>
    public ImmutableArray<string> PendingStores { get; }

    /// <summary>Gets the instant of logical deletion.</summary>
    public DateTimeOffset DeletedAt { get; }

    /// <summary>Gets the store-wide deletion generation assigned at logical deletion.</summary>
    public long Generation { get; }
}
