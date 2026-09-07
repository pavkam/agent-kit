// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares bounded artifact content and lifecycle policy before staging.</summary>
public sealed record ArtifactMetadata
{
    /// <summary>Initializes artifact metadata.</summary>
    /// <param name="ownerId">The retention owner.</param>
    /// <param name="mediaType">The non-blank media type.</param>
    /// <param name="declaredLength">The exact non-negative byte length.</param>
    /// <param name="declaredContentHash">The expected complete-content hash.</param>
    /// <param name="classification">The data classification.</param>
    /// <param name="ownership">The ownership class.</param>
    /// <param name="mutability">The versioning behavior.</param>
    /// <param name="retention">The resolved retention decision.</param>
    /// <exception cref="ArgumentException">An identity, media type, or hash is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="retention"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The length is negative or an enum is undefined.</exception>
    public ArtifactMetadata(
        ArtifactOwnerId ownerId,
        string mediaType,
        long declaredLength,
        ContentHash declaredContentHash,
        ArtifactDataClassification classification,
        ArtifactOwnershipKind ownership,
        ArtifactMutability mutability,
        ArtifactRetention retention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId.Value, nameof(ownerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentOutOfRangeException.ThrowIfNegative(declaredLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredContentHash.Value, nameof(declaredContentHash));
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentOutOfRangeException.ThrowIfUndefined(ownership);
        ArgumentOutOfRangeException.ThrowIfUndefined(mutability);
        ArgumentNullException.ThrowIfNull(retention);
        OwnerId = ownerId;
        MediaType = mediaType;
        DeclaredLength = declaredLength;
        DeclaredContentHash = declaredContentHash;
        Classification = classification;
        Ownership = ownership;
        Mutability = mutability;
        Retention = retention;
    }
    /// <summary>Gets the retention owner.</summary>
    public ArtifactOwnerId OwnerId { get; }
    /// <summary>Gets the media type.</summary>
    public string MediaType { get; }
    /// <summary>Gets the exact declared byte length.</summary>
    public long DeclaredLength { get; }
    /// <summary>Gets the expected complete-content hash.</summary>
    public ContentHash DeclaredContentHash { get; }
    /// <summary>Gets the data classification.</summary>
    public ArtifactDataClassification Classification { get; }
    /// <summary>Gets the ownership class.</summary>
    public ArtifactOwnershipKind Ownership { get; }
    /// <summary>Gets the versioning behavior.</summary>
    public ArtifactMutability Mutability { get; }
    /// <summary>Gets the resolved retention decision.</summary>
    public ArtifactRetention Retention { get; }
}
