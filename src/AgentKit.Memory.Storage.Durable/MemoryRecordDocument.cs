// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="DurableMemoryRecord"/>.</summary>
/// <param name="Id">The memory identity.</param>
/// <param name="AgentId">The owning agent.</param>
/// <param name="SourceSessionId">The producing session.</param>
/// <param name="SourceRunId">The producing run.</param>
/// <param name="Namespace">The namespace text.</param>
/// <param name="Tenant">The owning tenant text.</param>
/// <param name="Owner">The owning principal text.</param>
/// <param name="Shared">Whether every principal in the tenant may read the record.</param>
/// <param name="Kind">The semantic role.</param>
/// <param name="Text">The body text.</param>
/// <param name="ContentExtensions">The body's extension data.</param>
/// <param name="Classification">The sensitivity.</param>
/// <param name="Provenance">The source evidence.</param>
/// <param name="ExpiresAt">The retention expiry, or <see langword="null"/>.</param>
/// <param name="RetainUntilExplicitDeletion">Whether the record is retained until explicit deletion.</param>
/// <param name="State">The lifecycle state.</param>
/// <param name="Version">The version token text.</param>
/// <param name="CreatedAt">The creation instant.</param>
/// <param name="UpdatedAt">The last-update instant.</param>
/// <param name="Extensions">The record's extension data.</param>
internal sealed record MemoryRecordDocument(
    Guid Id,
    Guid AgentId,
    Guid SourceSessionId,
    Guid SourceRunId,
    string Namespace,
    string Tenant,
    string Owner,
    bool Shared,
    MemoryKind Kind,
    string Text,
    ImmutableArray<StoreExtensionDocument> ContentExtensions,
    DataClassification Classification,
    ProvenanceDocument Provenance,
    DateTimeOffset? ExpiresAt,
    bool RetainUntilExplicitDeletion,
    MemoryLifecycleState State,
    string Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ImmutableArray<StoreExtensionDocument> Extensions)
{
    /// <summary>Converts a record to its persisted form.</summary>
    /// <param name="value">The non-null record.</param>
    /// <returns>The document.</returns>
    internal static MemoryRecordDocument FromDomain(DurableMemoryRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value,
            value.AgentId.Value,
            value.SourceSessionId.Value,
            value.SourceRunId.Value,
            value.Namespace.Value,
            value.TenantId.Value,
            value.Visibility.OwnerPrincipalId.Value,
            value.Visibility.SharedWithTenant,
            value.Kind,
            value.Content.Text,
            StoreExtensionDocument.FromDomain(value.Content.Extensions),
            value.Classification,
            ProvenanceDocument.FromDomain(value.Provenance),
            value.Retention.ExpiresAt,
            value.Retention.RetainUntilExplicitDeletion,
            value.State,
            value.Version.Value,
            value.CreatedAt,
            value.UpdatedAt,
            StoreExtensionDocument.FromDomain(value.Extensions));
    }

    /// <summary>Restores the record, re-running its validation.</summary>
    /// <returns>The record.</returns>
    internal DurableMemoryRecord ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Provenance);
        return new(
            new MemoryId(Id),
            new AgentId(AgentId),
            new SessionId(SourceSessionId),
            new RunId(SourceRunId),
            new MemoryNamespace(Namespace),
            new TenantId(Tenant),
            new PrincipalVisibility(new TenantId(Tenant), new PrincipalId(Owner), Shared),
            Kind,
            new MemoryContent(Text, StoreExtensionDocument.ToDomain(ContentExtensions)),
            Classification,
            Provenance.ToDomain(),
            new RetentionPolicy(ExpiresAt, RetainUntilExplicitDeletion),
            State,
            new VersionToken(Version),
            CreatedAt,
            UpdatedAt,
            StoreExtensionDocument.ToDomain(Extensions));
    }
}
