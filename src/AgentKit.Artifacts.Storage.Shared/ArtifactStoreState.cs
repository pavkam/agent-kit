// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Holds the authoritative in-memory projection of every entry for an adapter that replays durable records into memory.</summary>
/// <remarks>
/// The class is not thread-safe: the owning backend serializes every call under its own gate. Indexes are derived entirely from
/// the entries, so replaying persisted entries in order reproduces exactly the state that was acknowledged.
/// </remarks>
internal sealed class ArtifactStoreState: IArtifactEntryLookup
{
    private readonly Dictionary<TenantArtifactPreparationKey, ArtifactEntry> _entries = [];
    private readonly Dictionary<ReplayKey, TenantArtifactPreparationKey> _replay = [];
    private readonly Dictionary<TenantArtifactKey, TenantArtifactPreparationKey> _artifacts = [];
    private readonly Dictionary<(TenantId Tenant, ContentHash Hash), int> _payloads = [];

    /// <summary>Gets the number of entries held.</summary>
    internal int Count => _entries.Count;

    /// <inheritdoc/>
    public ArtifactEntry? ByPreparation(TenantId tenantId, ArtifactPreparationId preparationId) =>
        _entries.GetValueOrDefault(new TenantArtifactPreparationKey(tenantId, preparationId));

    /// <inheritdoc/>
    public ArtifactEntry? ByReplay(TenantId tenantId, IdempotencyKey idempotencyKey) =>
        _replay.TryGetValue(new ReplayKey(tenantId, "prepare", idempotencyKey.Value), out var key) ? _entries[key] : null;

    /// <inheritdoc/>
    public ArtifactEntry? ByArtifact(TenantId tenantId, ArtifactId artifactId, ArtifactVersion version) =>
        _artifacts.TryGetValue(new TenantArtifactKey(tenantId, artifactId, version), out var key) ? _entries[key] : null;

    /// <summary>Determines whether any entry in a tenant still owns a payload with the given hash.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="hash">The content hash.</param>
    /// <returns><see langword="true"/> when a prepared or live committed entry references the hash.</returns>
    internal bool HasLivePayload(TenantId tenantId, ContentHash hash) => _payloads.GetValueOrDefault((tenantId, hash)) > 0;

    /// <summary>Applies entries an adapter durably persisted, in order.</summary>
    /// <param name="upserts">The persisted entries.</param>
    internal void Commit(ImmutableArray<ArtifactEntry> upserts)
    {
        foreach (var entry in upserts)
        {
            Apply(entry);
        }
    }

    /// <summary>Applies one entry read back from durable storage during replay.</summary>
    /// <param name="entry">The recovered entry.</param>
    internal void Restore(ArtifactEntry entry) => Apply(entry);

    /// <summary>Lists every entry in a stable order for log compaction.</summary>
    /// <returns>The entries ordered by tenant then preparation.</returns>
    internal ImmutableArray<ArtifactEntry> Snapshot() =>
    [
        .. _entries.Values.OrderBy(static entry => entry.TenantId.Value, StringComparer.Ordinal).ThenBy(static entry => entry.PreparationId.Value),
    ];

    private void Apply(ArtifactEntry entry)
    {
        Debug.Assert(entry is not null, "Only validated entries are applied.");
        var key = entry.PreparationKey;
        if (_entries.TryGetValue(key, out var previous) && previous.HoldsPayload)
        {
            Adjust(previous, -1);
        }

        _entries[key] = entry;
        _ = _replay.TryAdd(entry.ReplayKey, key);
        if (entry.State == ArtifactEntryState.Finalized)
        {
            _artifacts[entry.ArtifactKey] = key;
        }

        if (entry.HoldsPayload)
        {
            Adjust(entry, 1);
        }
    }

    private void Adjust(ArtifactEntry entry, int delta)
    {
        var key = (entry.TenantId, entry.ContentHash);
        var count = _payloads.GetValueOrDefault(key) + delta;
        if (count <= 0)
        {
            _ = _payloads.Remove(key);
        }
        else
        {
            _payloads[key] = count;
        }
    }
}
