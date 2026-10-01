// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Holds tenant-partitioned memory entries and their indexes for adapters that keep state in process memory.</summary>
/// <remarks>
/// The class is not thread-safe: the owning adapter serializes every call under its own gate. Mutations are planned first and
/// committed second so a durable adapter can write the planned entries before memory advances. State is partitioned by tenant,
/// so the same identity in two tenants is two different records.
/// </remarks>
internal sealed class MemoryStoreState: IMemoryLookup
{
    private readonly Dictionary<(TenantId Tenant, MemoryId Id), MemoryEntry> _entries = [];
    private readonly Dictionary<(TenantId Tenant, string Key), MemoryId> _creations = [];
    private long _sequence;

    /// <inheritdoc/>
    public long NextSequence => _sequence + 1;

    /// <inheritdoc/>
    public long CurrentGeneration { get; private set; }

    /// <summary>Gets the number of stored entries across every tenant.</summary>
    internal int Count => _entries.Count;

    /// <inheritdoc/>
    public MemoryEntry? FindByCreateKey(TenantId tenant, string key) =>
        _creations.TryGetValue((tenant, key), out var id) ? _entries[(tenant, id)] : null;

    /// <inheritdoc/>
    public MemoryEntry? Find(TenantId tenant, MemoryId id) => _entries.GetValueOrDefault((tenant, id));

    /// <inheritdoc/>
    public IEnumerable<MemoryEntry> Scan(TenantId tenant, AgentId agent, long afterSequence) =>
        _entries.Values
            .Where(entry => entry.Tenant == tenant && entry.Current.AgentId == agent && entry.Sequence > afterSequence)
            .OrderBy(static entry => entry.Sequence);

    /// <summary>Lists every stored entry in creation order, for compaction.</summary>
    /// <returns>One latest entry per memory, ordered by creation sequence.</returns>
    internal IReadOnlyList<MemoryEntry> Snapshot() => [.. _entries.Values.OrderBy(static entry => entry.Sequence)];

    /// <summary>Commits planned entries to memory after the adapter persisted them.</summary>
    /// <param name="entries">The planned entries.</param>
    internal void Commit(ImmutableArray<MemoryEntry> entries)
    {
        foreach (var entry in entries)
        {
            Restore(entry);
        }
    }

    /// <summary>Installs one entry, used both to commit a plan and to rebuild memory from a durable log.</summary>
    /// <param name="entry">The entry to install.</param>
    internal void Restore(MemoryEntry entry)
    {
        Debug.Assert(entry is not null, "Restoring installs an entry.");
        _entries[(entry.Tenant, entry.Current.Id)] = entry;
        _creations[(entry.Tenant, entry.CreateKey)] = entry.Current.Id;
        _sequence = Math.Max(_sequence, entry.Sequence);
        if (entry.Deletion is { } deletion)
        {
            CurrentGeneration = Math.Max(CurrentGeneration, deletion.Generation);
        }
    }
}
