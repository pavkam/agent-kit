// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the persisted form of the validated <see cref="ArtifactMetadata"/> an entry carries.</summary>
/// <param name="OwnerId">The retention owner text.</param>
/// <param name="MediaType">The media type.</param>
/// <param name="DeclaredLength">The exact declared byte length.</param>
/// <param name="DeclaredContentHash">The declared hash text, or <see langword="null"/> when none was declared.</param>
/// <param name="Classification">The data classification.</param>
/// <param name="Ownership">The ownership class.</param>
/// <param name="Mutability">The versioning behavior.</param>
/// <param name="RetentionPolicy">The resolved retention policy key text.</param>
/// <param name="RetentionExpiresAt">The retention expiry, or <see langword="null"/>.</param>
/// <param name="LegalHold">Whether a legal hold applies.</param>
/// <param name="ExternalResourceId">The external resource identity text, or <see langword="null"/> for content AgentKit owns.</param>
/// <param name="ExternalUri">The canonical unsigned external locator text, or <see langword="null"/> for content AgentKit owns.</param>
/// <param name="ExternalMayDelete">Whether the external owner delegated delete authority; meaningful only with an external locator.</param>
internal sealed record StoredArtifactMetadata(
    string OwnerId,
    string MediaType,
    long DeclaredLength,
    string? DeclaredContentHash,
    DataClassification Classification,
    ArtifactOwnershipKind Ownership,
    ArtifactMutability Mutability,
    string RetentionPolicy,
    DateTimeOffset? RetentionExpiresAt,
    bool LegalHold,
    string? ExternalResourceId,
    string? ExternalUri,
    bool ExternalMayDelete)
{
    /// <summary>Converts validated metadata to its persisted form.</summary>
    /// <param name="value">The non-null metadata.</param>
    /// <returns>The document.</returns>
    internal static StoredArtifactMetadata FromDomain(ArtifactMetadata value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.OwnerId.Value, value.MediaType, value.DeclaredLength, value.DeclaredContentHash?.Value, value.Classification,
            value.Ownership, value.Mutability, value.Retention.Policy.Value, value.Retention.ExpiresAt, value.Retention.LegalHold,
            value.ExternalOwnership?.ResourceId.Value, value.ExternalOwnership?.CanonicalUri.AbsoluteUri,
            value.ExternalOwnership?.AgentKitMayDelete ?? false);
    }

    /// <summary>Restores the metadata, re-running its validation.</summary>
    /// <returns>The metadata.</returns>
    internal ArtifactMetadata ToDomain() => new(
        new ArtifactOwnerId(OwnerId), MediaType, DeclaredLength, DeclaredContentHash is null ? null : new ContentHash(DeclaredContentHash),
        Classification, Ownership, Mutability, new ArtifactRetention(new ArtifactRetentionPolicyKey(RetentionPolicy), RetentionExpiresAt, LegalHold),
        ExternalResourceId is null || ExternalUri is null
            ? null
            : new ExternalArtifactOwnership(new ExternalArtifactResourceId(ExternalResourceId), new Uri(ExternalUri), ExternalMayDelete));
}
