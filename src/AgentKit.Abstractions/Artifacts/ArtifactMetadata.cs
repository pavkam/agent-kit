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
    /// <param name="declaredContentHash">The expected complete-content hash, or <see langword="null"/> when the caller declares none and the observed hash is recorded instead.</param>
    /// <param name="classification">The data classification.</param>
    /// <param name="ownership">The ownership class.</param>
    /// <param name="mutability">The versioning behavior.</param>
    /// <param name="retention">The requested retention decision, resolved by the retention policy before staging.</param>
    /// <param name="externalOwnership">The external ownership evidence; required exactly when <paramref name="ownership"/> is <see cref="ArtifactOwnershipKind.External"/>.</param>
    /// <exception cref="ArgumentException">An identity, media type, or declared hash is blank, or external ownership evidence disagrees with the ownership class or mutability.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="retention"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The length is negative or an enum is undefined.</exception>
    public ArtifactMetadata(
        ArtifactOwnerId ownerId,
        string mediaType,
        long declaredLength,
        ContentHash? declaredContentHash,
        DataClassification classification,
        ArtifactOwnershipKind ownership,
        ArtifactMutability mutability,
        ArtifactRetention retention,
        ExternalArtifactOwnership? externalOwnership)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId.Value, nameof(ownerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentOutOfRangeException.ThrowIfNegative(declaredLength);
        if (declaredContentHash is { } hash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash.Value, nameof(declaredContentHash));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        ArgumentOutOfRangeException.ThrowIfUndefined(ownership);
        ArgumentOutOfRangeException.ThrowIfUndefined(mutability);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentException.ThrowIfInvalidArtifactExternalOwnership(ownership, mutability, externalOwnership, nameof(externalOwnership));
        OwnerId = ownerId;
        MediaType = mediaType;
        DeclaredLength = declaredLength;
        DeclaredContentHash = declaredContentHash;
        Classification = classification;
        Ownership = ownership;
        Mutability = mutability;
        Retention = retention;
        ExternalOwnership = externalOwnership;
    }

    /// <summary>Gets the retention owner.</summary>
    public ArtifactOwnerId OwnerId { get; }

    /// <summary>Gets the media type.</summary>
    public string MediaType { get; }

    /// <summary>Gets the exact declared byte length.</summary>
    public long DeclaredLength { get; }

    /// <summary>Gets the expected complete-content hash, or <see langword="null"/> when none was declared.</summary>
    public ContentHash? DeclaredContentHash { get; }

    /// <summary>Gets the data classification.</summary>
    public DataClassification Classification { get; }

    /// <summary>Gets the ownership class.</summary>
    public ArtifactOwnershipKind Ownership { get; }

    /// <summary>Gets the versioning behavior.</summary>
    public ArtifactMutability Mutability { get; }

    /// <summary>Gets the requested retention decision.</summary>
    public ArtifactRetention Retention { get; }

    /// <summary>Gets the external ownership evidence, or <see langword="null"/> for content AgentKit owns.</summary>
    public ExternalArtifactOwnership? ExternalOwnership { get; }
}
