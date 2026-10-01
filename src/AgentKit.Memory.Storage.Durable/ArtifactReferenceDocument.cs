// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of the <see cref="ArtifactReference"/> that holds a document's source bytes.</summary>
/// <param name="Id">The artifact identity.</param>
/// <param name="Version">The artifact version text.</param>
/// <param name="DirectoryId">The artifact directory text.</param>
/// <param name="ProfileKey">The artifact profile key text.</param>
/// <param name="ProfileVersion">The artifact profile version.</param>
/// <param name="Tenant">The owning tenant text.</param>
/// <param name="OwnerId">The artifact owner text.</param>
/// <param name="CreatedBy">The creating principal text.</param>
/// <param name="MediaType">The media type.</param>
/// <param name="Length">The byte length.</param>
/// <param name="ContentHash">The integrity hash text.</param>
/// <param name="VerifiedAt">The integrity verification instant.</param>
/// <param name="Classification">The artifact classification.</param>
/// <param name="Ownership">The ownership kind.</param>
/// <param name="Mutability">The mutability.</param>
/// <param name="RetentionPolicy">The retention policy key text.</param>
/// <param name="RetentionExpiresAt">The retention expiry, or <see langword="null"/>.</param>
/// <param name="LegalHold">Whether a legal hold applies.</param>
/// <param name="ExternalResourceId">The external resource identity text, or <see langword="null"/> for content AgentKit owns.</param>
/// <param name="ExternalUri">The canonical unsigned external locator text, or <see langword="null"/> for content AgentKit owns.</param>
/// <param name="ExternalMayDelete">Whether the external owner delegated delete authority; meaningful only with an external locator.</param>
/// <param name="CreatedAt">The creation instant.</param>
internal sealed record ArtifactReferenceDocument(
    Guid Id,
    string Version,
    string DirectoryId,
    string ProfileKey,
    long ProfileVersion,
    string Tenant,
    string OwnerId,
    string CreatedBy,
    string MediaType,
    long Length,
    string ContentHash,
    DateTimeOffset VerifiedAt,
    DataClassification Classification,
    ArtifactOwnershipKind Ownership,
    ArtifactMutability Mutability,
    string RetentionPolicy,
    DateTimeOffset? RetentionExpiresAt,
    bool LegalHold,
    string? ExternalResourceId,
    string? ExternalUri,
    bool ExternalMayDelete,
    DateTimeOffset CreatedAt)
{
    /// <summary>Converts an artifact reference to its persisted form.</summary>
    /// <param name="value">The non-null reference.</param>
    /// <returns>The document.</returns>
    internal static ArtifactReferenceDocument FromDomain(ArtifactReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value,
            value.Version.Value,
            value.DirectoryId.Value,
            value.ProfileKey.Value,
            value.ProfileVersion.Value,
            value.TenantId.Value,
            value.OwnerId.Value,
            value.CreatedBy.Value,
            value.MediaType,
            value.Length,
            value.Integrity.ContentHash.Value,
            value.Integrity.VerifiedAt,
            value.Classification,
            value.Ownership,
            value.Mutability,
            value.Retention.Policy.Value,
            value.Retention.ExpiresAt,
            value.Retention.LegalHold,
            value.ExternalOwnership?.ResourceId.Value,
            value.ExternalOwnership?.CanonicalUri.AbsoluteUri,
            value.ExternalOwnership?.AgentKitMayDelete ?? false,
            value.CreatedAt);
    }

    /// <summary>Restores the reference, re-running its validation.</summary>
    /// <returns>The reference.</returns>
    internal ArtifactReference ToDomain() => new(
        new ArtifactId(Id),
        new ArtifactVersion(Version),
        new ArtifactDirectoryId(DirectoryId),
        new ArtifactProfileKey(ProfileKey),
        new ArtifactProfileVersion(ProfileVersion),
        new TenantId(Tenant),
        new ArtifactOwnerId(OwnerId),
        new PrincipalId(CreatedBy),
        MediaType,
        Length,
        new ArtifactIntegrity(new ContentHash(ContentHash), VerifiedAt),
        Classification,
        Ownership,
        Mutability,
        new ArtifactRetention(new ArtifactRetentionPolicyKey(RetentionPolicy), RetentionExpiresAt, LegalHold),
        ExternalResourceId is null || ExternalUri is null
            ? null
            : new ExternalArtifactOwnership(new ExternalArtifactResourceId(ExternalResourceId), new Uri(ExternalUri), ExternalMayDelete),
        CreatedAt);
}
