// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Portably identifies one committed immutable artifact version without exposing backend location.</summary>
public sealed record ArtifactReference
{
    /// <summary>Initializes a committed artifact reference.</summary>
    /// <param name="id">The logical artifact.</param>
    /// <param name="version">The immutable version.</param>
    /// <param name="directoryId">The logical directory.</param>
    /// <param name="profileKey">The captured profile.</param>
    /// <param name="profileVersion">The captured profile revision.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="ownerId">The retention owner.</param>
    /// <param name="createdBy">The creating principal.</param>
    /// <param name="mediaType">The media type.</param>
    /// <param name="length">The complete byte length.</param>
    /// <param name="integrity">The verified integrity evidence.</param>
    /// <param name="classification">The data classification.</param>
    /// <param name="ownership">The ownership class.</param>
    /// <param name="mutability">The versioning behavior.</param>
    /// <param name="retention">The resolved retention decision.</param>
    /// <param name="createdAt">The publication time.</param>
    /// <exception cref="ArgumentException">A value is blank.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty, the length is negative, or an enum is undefined.</exception>
    public ArtifactReference(
        ArtifactId id, ArtifactVersion version, ArtifactDirectoryId directoryId,
        ArtifactProfileKey profileKey, ArtifactProfileVersion profileVersion,
        TenantId tenantId, ArtifactOwnerId ownerId, PrincipalId createdBy,
        string mediaType, long length, ArtifactIntegrity integrity,
        ArtifactDataClassification classification, ArtifactOwnershipKind ownership,
        ArtifactMutability mutability, ArtifactRetention retention, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId.Value, nameof(directoryId));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfEqual(profileVersion, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId.Value, nameof(ownerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy.Value, nameof(createdBy));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(integrity);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentOutOfRangeException.ThrowIfUndefined(ownership);
        ArgumentOutOfRangeException.ThrowIfUndefined(mutability);
        ArgumentNullException.ThrowIfNull(retention);
        Id = id; Version = version; DirectoryId = directoryId; ProfileKey = profileKey;
        ProfileVersion = profileVersion; TenantId = tenantId; OwnerId = ownerId;
        CreatedBy = createdBy; MediaType = mediaType; Length = length; Integrity = integrity;
        Classification = classification; Ownership = ownership; Mutability = mutability;
        Retention = retention; CreatedAt = createdAt;
    }
    /// <summary>Gets the logical artifact.</summary>
    public ArtifactId Id { get; }
    /// <summary>Gets the immutable version.</summary>
    public ArtifactVersion Version { get; }
    /// <summary>Gets the logical directory.</summary>
    public ArtifactDirectoryId DirectoryId { get; }
    /// <summary>Gets the captured profile.</summary>
    public ArtifactProfileKey ProfileKey { get; }
    /// <summary>Gets the captured profile revision.</summary>
    public ArtifactProfileVersion ProfileVersion { get; }
    /// <summary>Gets the tenant partition.</summary>
    public TenantId TenantId { get; }
    /// <summary>Gets the retention owner.</summary>
    public ArtifactOwnerId OwnerId { get; }
    /// <summary>Gets the creating principal.</summary>
    public PrincipalId CreatedBy { get; }
    /// <summary>Gets the media type.</summary>
    public string MediaType { get; }
    /// <summary>Gets the complete byte length.</summary>
    public long Length { get; }
    /// <summary>Gets verified integrity evidence.</summary>
    public ArtifactIntegrity Integrity { get; }
    /// <summary>Gets the data classification.</summary>
    public ArtifactDataClassification Classification { get; }
    /// <summary>Gets the ownership class.</summary>
    public ArtifactOwnershipKind Ownership { get; }
    /// <summary>Gets the versioning behavior.</summary>
    public ArtifactMutability Mutability { get; }
    /// <summary>Gets the resolved retention decision.</summary>
    public ArtifactRetention Retention { get; }
    /// <summary>Gets the publication time.</summary>
    public DateTimeOffset CreatedAt { get; }
}
