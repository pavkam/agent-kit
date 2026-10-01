// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records a memory deletion, separating logical invisibility from physical purge, without returning deleted content.</summary>
/// <remarks>
/// A receipt that is logically deleted but not physically purged names the stores whose cleanup is still pending. Already
/// transmitted data cannot be recalled, and eventual physical cleanup is never advertised as immediate erasure everywhere.
/// </remarks>
public sealed record MemoryDeletionReceipt
{
    /// <summary>Initializes a validated receipt.</summary>
    /// <param name="id">The deleted memory identity.</param>
    /// <param name="logicallyDeleted">Whether the record is authoritatively invisible to new retrieval and exposure.</param>
    /// <param name="physicallyPurged">Whether the body was physically removed from every named store.</param>
    /// <param name="pendingStores">The non-blank names of stores whose purge is still pending; empty once purged.</param>
    /// <param name="deletedAt">The instant of logical deletion.</param>
    /// <param name="generation">The store-wide deletion generation assigned at logical deletion.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default or the generation is not positive.</exception>
    /// <exception cref="ArgumentException">A purged record is not logically deleted, a purged receipt names pending stores, a pending-store name is blank, or the list is default.</exception>
    public MemoryDeletionReceipt(
        MemoryId id,
        bool logicallyDeleted,
        bool physicallyPurged,
        ImmutableArray<string> pendingStores,
        DateTimeOffset deletedAt,
        long generation)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNotEqual(!physicallyPurged || logicallyDeleted, true, nameof(physicallyPurged));
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
        PendingStores = pendingStores;
        DeletedAt = deletedAt;
        Generation = generation;
    }

    /// <summary>Gets the deleted memory identity.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets a value indicating whether the record is authoritatively invisible to new retrieval and exposure.</summary>
    public bool LogicallyDeleted { get; }

    /// <summary>Gets a value indicating whether the body was physically removed.</summary>
    public bool PhysicallyPurged { get; }

    /// <summary>Gets the names of stores whose purge is still pending.</summary>
    public ImmutableArray<string> PendingStores { get; }

    /// <summary>Gets the instant of logical deletion.</summary>
    public DateTimeOffset DeletedAt { get; }

    /// <summary>Gets the store-wide deletion generation assigned at logical deletion.</summary>
    public long Generation { get; }
}
