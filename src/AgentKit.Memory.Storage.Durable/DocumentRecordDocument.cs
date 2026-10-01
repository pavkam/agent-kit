// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="DocumentRecord"/>.</summary>
/// <param name="Id">The document identity.</param>
/// <param name="AgentId">The owning agent.</param>
/// <param name="SourceSessionId">The producing session.</param>
/// <param name="SourceRunId">The producing run.</param>
/// <param name="Tenant">The owning tenant text.</param>
/// <param name="Owner">The owning principal text.</param>
/// <param name="Shared">Whether every principal in the tenant may read the document.</param>
/// <param name="Version">The source version text.</param>
/// <param name="ContentHash">The content hash text.</param>
/// <param name="Title">The display title.</param>
/// <param name="MediaType">The media type.</param>
/// <param name="SourceArtifact">The artifact reference holding the source bytes, or <see langword="null"/>.</param>
/// <param name="MetadataExtensions">The metadata extension data.</param>
/// <param name="Classification">The sensitivity.</param>
/// <param name="Provenance">The source evidence.</param>
/// <param name="ExpiresAt">The retention expiry, or <see langword="null"/>.</param>
/// <param name="RetainUntilExplicitDeletion">Whether the document is retained until explicit deletion.</param>
internal sealed record DocumentRecordDocument(
    Guid Id,
    Guid AgentId,
    Guid SourceSessionId,
    Guid SourceRunId,
    string Tenant,
    string Owner,
    bool Shared,
    string Version,
    string ContentHash,
    string Title,
    string MediaType,
    ArtifactReferenceDocument? SourceArtifact,
    ImmutableArray<StoreExtensionDocument> MetadataExtensions,
    DataClassification Classification,
    ProvenanceDocument Provenance,
    DateTimeOffset? ExpiresAt,
    bool RetainUntilExplicitDeletion)
{
    /// <summary>Converts a record to its persisted form.</summary>
    /// <param name="value">The non-null record.</param>
    /// <returns>The document.</returns>
    internal static DocumentRecordDocument FromDomain(DocumentRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value,
            value.AgentId.Value,
            value.SourceSessionId.Value,
            value.SourceRunId.Value,
            value.TenantId.Value,
            value.Visibility.OwnerPrincipalId.Value,
            value.Visibility.SharedWithTenant,
            value.Version.Value,
            value.ContentHash.Value,
            value.Metadata.Title,
            value.Metadata.MediaType,
            value.Metadata.SourceArtifact is null ? null : ArtifactReferenceDocument.FromDomain(value.Metadata.SourceArtifact),
            StoreExtensionDocument.FromDomain(value.Metadata.Extensions),
            value.Classification,
            ProvenanceDocument.FromDomain(value.Provenance),
            value.Retention.ExpiresAt,
            value.Retention.RetainUntilExplicitDeletion);
    }

    /// <summary>Restores the record, re-running its validation.</summary>
    /// <returns>The record.</returns>
    internal DocumentRecord ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Provenance);
        return new(
            new DocumentId(Id),
            new AgentId(AgentId),
            new SessionId(SourceSessionId),
            new RunId(SourceRunId),
            new TenantId(Tenant),
            new PrincipalVisibility(new TenantId(Tenant), new PrincipalId(Owner), Shared),
            new DocumentVersion(Version),
            new ContentHash(ContentHash),
            new DocumentMetadata(Title, MediaType, SourceArtifact?.ToDomain(), StoreExtensionDocument.ToDomain(MetadataExtensions)),
            Classification,
            Provenance.ToDomain(),
            new RetentionPolicy(ExpiresAt, RetainUntilExplicitDeletion));
    }
}
