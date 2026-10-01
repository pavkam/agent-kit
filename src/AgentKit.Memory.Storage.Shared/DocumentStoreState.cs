// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Holds tenant-partitioned document entries for adapters that keep state in process memory.</summary>
/// <remarks>The class is not thread-safe: the owning adapter serializes every call under its own gate. Mutations are planned first and committed second so a durable adapter can write the planned entry before memory advances.</remarks>
internal sealed class DocumentStoreState: IDocumentLookup
{
    private readonly Dictionary<(TenantId Tenant, DocumentId Id), DocumentEntry> _entries = [];
    private long _sequence;

    /// <inheritdoc/>
    public long NextSequence => _sequence + 1;

    /// <inheritdoc/>
    public long CurrentGeneration { get; private set; }

    /// <inheritdoc/>
    public DocumentEntry? Find(TenantId tenant, DocumentId id) => _entries.GetValueOrDefault((tenant, id));

    /// <summary>Lists every stored entry in creation order, for compaction.</summary>
    /// <returns>One latest entry per document, ordered by creation sequence.</returns>
    internal IReadOnlyList<DocumentEntry> Snapshot() => [.. _entries.Values.OrderBy(static entry => entry.Sequence)];

    /// <summary>Commits a planned entry to memory after the adapter persisted it.</summary>
    /// <param name="entry">The planned entry.</param>
    internal void Commit(DocumentEntry entry) => Restore(entry);

    /// <summary>Installs one entry, used both to commit a plan and to rebuild memory from a durable log.</summary>
    /// <param name="entry">The entry to install.</param>
    internal void Restore(DocumentEntry entry)
    {
        Debug.Assert(entry is not null, "Restoring installs an entry.");
        _entries[(entry.Tenant, entry.Id)] = entry;
        _sequence = Math.Max(_sequence, entry.Sequence);
        if (entry.Deletion is { } deletion)
        {
            CurrentGeneration = Math.Max(CurrentGeneration, deletion.Generation);
        }
    }
}
