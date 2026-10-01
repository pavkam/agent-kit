// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the authoritative, content-free record of one tenant-partitioned preparation and, once published, its committed reference.</summary>
/// <remarks>
/// An entry holds no artifact bytes; an adapter keeps the payload beside it. A finalized entry that carries a deletion instant is a
/// tombstone: it keeps its reference so a stale reference can never resolve to different bytes and a deleted immutable version can
/// never be rebound.
/// </remarks>
/// <param name="TenantId">The tenant partition.</param>
/// <param name="PreparationId">The staging identity, unique within the tenant.</param>
/// <param name="ArtifactId">The reserved logical artifact.</param>
/// <param name="Version">The reserved immutable version.</param>
/// <param name="ProfileKey">The captured logical profile.</param>
/// <param name="ProfileVersion">The captured profile revision.</param>
/// <param name="CreatedBy">The creating principal.</param>
/// <param name="DirectoryId">The logical directory.</param>
/// <param name="Metadata">The validated metadata carrying the resolved retention decision.</param>
/// <param name="ContentHash">The verified complete-content hash.</param>
/// <param name="IdempotencyKey">The prepare replay key.</param>
/// <param name="CreatedAt">The staging instant.</param>
/// <param name="ExpiresAt">The staging expiry.</param>
/// <param name="State">The lifecycle state.</param>
/// <param name="Reference">The committed reference; present exactly when <paramref name="State"/> is finalized.</param>
/// <param name="DeletedAt">The tombstone instant, or <see langword="null"/> while the committed version is live.</param>
internal sealed record ArtifactEntry(
    TenantId TenantId,
    ArtifactPreparationId PreparationId,
    ArtifactId ArtifactId,
    ArtifactVersion Version,
    ArtifactProfileKey ProfileKey,
    ArtifactProfileVersion ProfileVersion,
    PrincipalId CreatedBy,
    ArtifactDirectoryId DirectoryId,
    ArtifactMetadata Metadata,
    ContentHash ContentHash,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    ArtifactEntryState State,
    ArtifactReference? Reference,
    DateTimeOffset? DeletedAt)
{
    /// <summary>Gets whether the committed version was deleted and only its tombstone remains.</summary>
    internal bool IsDeleted => DeletedAt is not null;

    /// <summary>Gets whether the entry still owns a stored payload.</summary>
    internal bool HoldsPayload => State == ArtifactEntryState.Prepared || (State == ArtifactEntryState.Finalized && DeletedAt is null);

    /// <summary>Gets the tenant-qualified preparation key.</summary>
    internal TenantArtifactPreparationKey PreparationKey => new(TenantId, PreparationId);

    /// <summary>Gets the tenant-qualified prepare replay key.</summary>
    internal ReplayKey ReplayKey => new(TenantId, "prepare", IdempotencyKey.Value);

    /// <summary>Gets the tenant-qualified immutable version key.</summary>
    internal TenantArtifactKey ArtifactKey => new(TenantId, ArtifactId, Version);

    /// <summary>Gets the staging receipt an equivalent replay returns.</summary>
    internal ArtifactStorePrepared Receipt => new(PreparationId, ArtifactId, Version, ExpiresAt);
}
