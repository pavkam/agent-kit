// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the auditable, content-free evidence that one memory was logically deleted.</summary>
/// <remarks>A tombstone outlives stale indexes and caches. It never carries the deleted body, and a restored backup cannot resurrect a tombstoned record for retrieval.</remarks>
public sealed record MemoryTombstone
{
    /// <summary>Initializes a validated tombstone.</summary>
    /// <param name="id">The deleted memory identity.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="version">The record version at deletion.</param>
    /// <param name="deletedAt">The instant of logical deletion.</param>
    /// <param name="purged"><see langword="true"/> when the body was physically removed from the store.</param>
    /// <param name="generation">The store-wide deletion generation assigned at logical deletion.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or the generation is not positive.</exception>
    /// <exception cref="ArgumentException">The tenant or version is blank.</exception>
    public MemoryTombstone(MemoryId id, AgentId agentId, TenantId tenantId, VersionToken version, DateTimeOffset deletedAt, bool purged, long generation)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);
        Id = id;
        AgentId = agentId;
        TenantId = tenantId;
        Version = version;
        DeletedAt = deletedAt;
        Purged = purged;
        Generation = generation;
    }

    /// <summary>Gets the deleted memory identity.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the owning tenant.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the record version at deletion.</summary>
    public VersionToken Version { get; }

    /// <summary>Gets the instant of logical deletion.</summary>
    public DateTimeOffset DeletedAt { get; }

    /// <summary>Gets a value indicating whether the body was physically removed.</summary>
    public bool Purged { get; }

    /// <summary>Gets the store-wide deletion generation assigned at logical deletion.</summary>
    public long Generation { get; }
}
