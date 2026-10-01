// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="VectorRecord"/>.</summary>
/// <param name="ChunkId">The chunk identity.</param>
/// <param name="DocumentId">The document identity.</param>
/// <param name="DocumentVersion">The document version text.</param>
/// <param name="AgentId">The owning agent.</param>
/// <param name="Tenant">The owning tenant text.</param>
/// <param name="Owner">The owning principal text.</param>
/// <param name="Shared">Whether every principal in the tenant may read the source.</param>
/// <param name="Vector">The vector components.</param>
/// <param name="SourceHash">The source chunk hash text.</param>
/// <param name="Chunker">The chunker version text.</param>
/// <param name="CreatedAt">The production instant.</param>
internal sealed record VectorRecordDocument(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentVersion,
    Guid AgentId,
    string Tenant,
    string Owner,
    bool Shared,
    ImmutableArray<float> Vector,
    string SourceHash,
    string Chunker,
    DateTimeOffset CreatedAt)
{
    /// <summary>Converts a vector record to its persisted form.</summary>
    /// <param name="value">The non-null record.</param>
    /// <returns>The document.</returns>
    internal static VectorRecordDocument FromDomain(VectorRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.ChunkId.Value,
            value.DocumentId.Value,
            value.DocumentVersion.Value,
            value.AgentId.Value,
            value.Visibility.TenantId.Value,
            value.Visibility.OwnerPrincipalId.Value,
            value.Visibility.SharedWithTenant,
            value.Vector,
            value.SourceHash.Value,
            value.Chunker.Value,
            value.CreatedAt);
    }

    /// <summary>Restores the vector record, re-running its validation.</summary>
    /// <returns>The record.</returns>
    internal VectorRecord ToDomain() => new(
        new ChunkId(ChunkId),
        new DocumentId(DocumentId),
        new DocumentVersion(DocumentVersion),
        new AgentId(AgentId),
        new PrincipalVisibility(new TenantId(Tenant), new PrincipalId(Owner), Shared),
        Vector,
        new ContentHash(SourceHash),
        new ChunkerVersion(Chunker),
        CreatedAt);
}
