// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the authoritative tenant-partitioned store route for one session address.</summary>
/// <remarks>A location binds routing state only. It does not prove the selected store contains a session or authorize a store operation.</remarks>
public sealed record SessionLocation
{
    /// <summary>Initializes a committed session-location record.</summary>
    /// <param name="address">The complete non-default session address.</param>
    /// <param name="tenantId">The nonblank tenant partition owning the location.</param>
    /// <param name="storeKey">The nonblank selected store key.</param>
    /// <param name="directoryRevision">The positive committed directory revision.</param>
    /// <param name="recordedAt">The clock-derived commit time.</param>
    /// <param name="schemaVersion">The nonblank serialized schema version.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentException">A tenant, store, or schema value is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An address identity or directory revision is default.</exception>
    public SessionLocation(SessionAddress address, TenantId tenantId, SessionStoreKey storeKey,
        SessionDirectoryRevision directoryRevision, DateTimeOffset recordedAt, SchemaVersion schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfEqual(address.AgentId, default, nameof(address));
        ArgumentOutOfRangeException.ThrowIfEqual(address.SessionId, default, nameof(address));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey.Value, nameof(storeKey));
        ArgumentOutOfRangeException.ThrowIfEqual(directoryRevision, default, nameof(directoryRevision));
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion.Value, nameof(schemaVersion));
        Address = address; TenantId = tenantId; StoreKey = storeKey; DirectoryRevision = directoryRevision;
        RecordedAt = recordedAt; SchemaVersion = schemaVersion;
    }

    /// <summary>Gets the routed session address.</summary><value>The complete non-default address.</value>
    public SessionAddress Address { get; }
    /// <summary>Gets the tenant partition that owns the location.</summary><value>The nonblank tenant identifier.</value>
    public TenantId TenantId { get; }
    /// <summary>Gets the pinned selected store key.</summary><value>The nonblank key used to resolve the existing session.</value>
    public SessionStoreKey StoreKey { get; }
    /// <summary>Gets the committed directory revision.</summary><value>A positive revision of this location binding.</value>
    public SessionDirectoryRevision DirectoryRevision { get; }
    /// <summary>Gets when the location was committed.</summary><value>The clock-derived commit time.</value>
    public DateTimeOffset RecordedAt { get; }
    /// <summary>Gets the persisted location schema version.</summary><value>The nonblank durable schema version.</value>
    public SchemaVersion SchemaVersion { get; }
}
